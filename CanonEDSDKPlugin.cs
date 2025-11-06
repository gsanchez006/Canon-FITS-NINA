using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Image.Interfaces;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.Plugin.Canon.EDSDK.Properties;
using NINA.Plugin.Canon.EDSDK.Services;
using NINA.WPF.Base.Mediator;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NINA.Plugin.Canon.EDSDK
{
    /// <summary>
    /// Tracks information about a pending FITS conversion task
    /// </summary>
    internal class TaskInfo
    {
        public DateTime CreatedAt { get; set; }
        public string FileName { get; set; }
        public string Status { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// Stores image data temporarily between BeforeImageSaved and AfterFinalizeImageSaved
    /// </summary>
    internal class PendingImageData
    {
        public IImageData ImageData { get; set; }
        public DateTime QueuedAt { get; set; }
        public int ConversionNumber { get; set; }
    }

    /// <summary>
    /// Main plugin class that implements the NINA plugin interface
    /// This plugin automatically converts all Canon RAW files (CR2, CR3, CRW, and newer formats) to FITS format
    /// using NINA's in-memory image data. Preserves full 14/16-bit dynamic range without demosaicing or processing.
    /// </summary>
    [Export(typeof(IPluginManifest))]
    public class CanonEDSDKPlugin : PluginBase, INotifyPropertyChanged
    {
        private readonly IPluginOptionsAccessor pluginSettings;
        private readonly IProfileService profileService;
        private readonly IImageSaveMediator imageSaveMediator;
        private RawToFitsConverter rawConverter;
        
        // Semaphore to limit concurrent conversions (prevents resource exhaustion during burst sequences)
        private static readonly SemaphoreSlim conversionSemaphore = new SemaphoreSlim(3, 3);
        
        // Track processed Canon RAW files (CR3, CR2, CRW) to prevent duplicate conversions
        private static readonly ConcurrentDictionary<string, bool> processedFiles = new ConcurrentDictionary<string, bool>();
        
        // Track all pending conversion tasks with detailed info to ensure they complete
        // Using ConcurrentDictionary allows us to remove completed tasks and track status
        private static readonly ConcurrentDictionary<Task, TaskInfo> pendingConversions = new ConcurrentDictionary<Task, TaskInfo>();
        
        // Queue to hold image data in STRICT order for sequential processing
        // This ensures conversion #N always gets file #N (prevents file mismatch during bursts)
        private static readonly ConcurrentQueue<PendingImageData> imageQueue = new ConcurrentQueue<PendingImageData>();
        
        // Semaphore to signal when new items are added to the queue
        private static readonly SemaphoreSlim queueSemaphore = new SemaphoreSlim(0);
        
        // Processing task that runs continuously
        private Task queueProcessingTask;
        private CancellationTokenSource queueCancellationSource;
        
        // Counter for total conversions to help with diagnostics
        private static int totalConversionsQueued = 0;

        [ImportingConstructor]
        public CanonEDSDKPlugin(IProfileService profileService, IOptionsVM options, IImageSaveMediator imageSaveMediator)
        {
            if (Properties.Settings.Default.UpdateSettings)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Properties.Settings.Default);
            }

            this.pluginSettings = new PluginOptionsAccessor(profileService, Guid.Parse(this.Identifier));
            this.profileService = profileService;
            this.imageSaveMediator = imageSaveMediator;

            // Initialize the RAW to FITS converter
            rawConverter = new RawToFitsConverter();

            // Hook into image saving events
            // BeforeImageSaved is called before the file is written, giving us access to the image data
            this.imageSaveMediator.BeforeImageSaved += ImageSaveMediator_BeforeImageSaved;

            // React to profile changes
            profileService.ProfileChanged += ProfileService_ProfileChanged;

            // NOTE: File watcher disabled - we now convert directly from in-memory data in BeforeImageSaved
            // This approach works with all Canon RAW formats (CR2, CR3, CRW, and future formats) without
            // requiring external RAW processing libraries or Canon EDSDK.
            // InitializeFileWatcher();

            // Start the queue processing task
            queueCancellationSource = new CancellationTokenSource();
            queueProcessingTask = Task.Run(() => ProcessQueueAsync(queueCancellationSource.Token));

            Logger.Info("Canon RAW to FITS Converter loaded successfully - will convert from in-memory data");
        }

        public override async Task Teardown()
        {
            // Cleanup resources
            imageSaveMediator.BeforeImageSaved -= ImageSaveMediator_BeforeImageSaved;
            profileService.ProfileChanged -= ProfileService_ProfileChanged;

            // Signal the queue processor to stop accepting new items
            Logger.Info("Sequence ended, stopping queue processor...");
            queueCancellationSource?.Cancel();

            // Give a brief moment for any final tasks to be queued
            // This handles the race condition where the last image's BeforeImageSaved
            // event is still being processed when sequence ends
            if (!imageQueue.IsEmpty)
            {
                Logger.Info($"Waiting for {imageQueue.Count} queued conversions to be processed...");
                await Task.Delay(2000); // 2 second grace period
            }

            // Wait for the queue processing task to complete
            if (queueProcessingTask != null)
            {
                try
                {
                    await Task.WhenAny(queueProcessingTask, Task.Delay(TimeSpan.FromMinutes(2)));
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Queue processing task threw exception: {ex.Message}");
                }
            }

            // Wait for all pending conversions to complete
            if (!pendingConversions.IsEmpty)
            {
                var pendingTasks = pendingConversions.Keys.ToList();
                var pendingCount = pendingTasks.Count;
                
                Logger.Info($"Waiting for {pendingCount} pending FITS conversion(s) to complete...");
                
                // Log details of pending conversions
                foreach (var kvp in pendingConversions)
                {
                    var info = kvp.Value;
                    var elapsed = (DateTime.Now - info.CreatedAt).TotalSeconds;
                    Logger.Info($"  - {info.FileName}: {info.Status} (queued {elapsed:F1}s ago)");
                }
                
                try
                {
                    // Wait up to 2 minutes for all conversions to finish
                    var timeout = TimeSpan.FromMinutes(2);
                    var waitTask = Task.WhenAll(pendingTasks);
                    var completedTask = await Task.WhenAny(waitTask, Task.Delay(timeout));
                    
                    if (completedTask == waitTask)
                    {
                        // All tasks completed successfully
                        Logger.Info($"All {pendingCount} pending conversion(s) completed successfully");
                    }
                    else
                    {
                        // Timeout occurred
                        var remaining = pendingConversions.Count;
                        Logger.Warning($"Teardown timeout after {timeout.TotalSeconds}s: {remaining} conversion(s) may not have completed");
                        
                        // Log which ones are still pending
                        foreach (var kvp in pendingConversions)
                        {
                            var info = kvp.Value;
                            Logger.Warning($"  - Still pending: {info.FileName} ({info.Status})");
                        }
                    }
                }
                catch (TimeoutException)
                {
                    var remaining = pendingConversions.Count;
                    Logger.Warning($"Teardown timeout: {remaining} conversion(s) may not have completed");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error waiting for pending conversions: {ex.Message}");
                }
            }
            else
            {
                Logger.Info("No pending conversions at teardown");
            }

            Logger.Info($"Canon RAW to FITS Converter teardown complete (Total conversions queued: {totalConversionsQueued})");
            await base.Teardown();
        }

        private void ProfileService_ProfileChanged(object sender, EventArgs e)
        {
            RaisePropertyChanged(nameof(AutoConvertEnabled));
            RaisePropertyChanged(nameof(OutputDirectory));
            RaisePropertyChanged(nameof(DeleteOriginalFiles));
            RaisePropertyChanged(nameof(FitsEngineIndex));
            RaisePropertyChanged(nameof(CFitsioCompressionType));
            
            // File watcher no longer used - converting from in-memory data
        }

        /// <summary>
        /// Event handler for when an image is being saved
        /// BeforeImageSaved gives us access to the image data in memory
        /// This is MUCH better than trying to process CR3 files from disk!
        /// We let NINA save the CR3, and we save a FITS file from the same in-memory data.
        /// </summary>
        private async Task ImageSaveMediator_BeforeImageSaved(object sender, BeforeImageSavedEventArgs e)
        {
            try
            {
                if (!AutoConvertEnabled)
                    return;

                // Check if camera name contains "Canon" 
                string cameraName = e.Image.MetaData.Camera?.Name ?? "";
                if (!cameraName.Contains("Canon", StringComparison.OrdinalIgnoreCase))
                    return;

                // Check if NINA is configured to save files to disk
                // If the file path is empty or the pattern would result in an empty filename, skip conversion
                var fileSettings = profileService.ActiveProfile.ImageFileSettings;
                if (string.IsNullOrEmpty(fileSettings?.FilePath))
                {
                    Logger.Info($"📸 Canon camera detected but NINA has no output directory configured - skipping FITS conversion");
                    Logger.Info($"   Configure an image save directory in NINA to use FITS conversion");
                    return;
                }

                // Increment counter and log
                var conversionNumber = Interlocked.Increment(ref totalConversionsQueued);
                
                Logger.Info($"📸 Canon camera detected, queuing FITS conversion #{conversionNumber}");
                Logger.Info($"   Camera: {cameraName}");
                Logger.Info($"   Exposure started: {e.Image.MetaData.Image.ExposureStart:HH:mm:ss.fff}");

                // Add to the FIFO queue for sequential processing
                // This ensures conversions are processed in the exact order images are saved
                var pendingImage = new PendingImageData
                {
                    ImageData = e.Image,
                    QueuedAt = DateTime.Now,
                    ConversionNumber = conversionNumber
                };
                
                imageQueue.Enqueue(pendingImage);
                
                // Signal the queue processor that a new item is available
                queueSemaphore.Release();
                
                Logger.Debug($"   [#{conversionNumber}] Added to processing queue (Queue size: {imageQueue.Count})");
                
                // Return immediately - processing happens asynchronously
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in BeforeImageSaved handler: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Background task that processes the conversion queue in strict FIFO order
        /// This ensures conversion #N always gets file #N, preventing file mismatch during bursts
        /// </summary>
        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            Logger.Info("Queue processor started");
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for a new item to be added to the queue (or cancellation)
                    await queueSemaphore.WaitAsync(cancellationToken);
                    
                    // Try to dequeue the next item
                    if (imageQueue.TryDequeue(out var pendingImage))
                    {
                        var conversionNumber = pendingImage.ConversionNumber;
                        var imageData = pendingImage.ImageData;
                        var queuedAt = pendingImage.QueuedAt;
                        
                        Logger.Debug($"   [#{conversionNumber}] Dequeued for processing (waited {(DateTime.Now - queuedAt).TotalMilliseconds:F0}ms in queue)");
                        
                        // Create task info for tracking
                        var taskInfo = new TaskInfo
                        {
                            CreatedAt = queuedAt,
                            FileName = $"Conversion #{conversionNumber}",
                            Status = "Processing"
                        };

                        // Process this conversion (wait for it to complete before moving to next)
                        var conversionTask = Task.Run(async () =>
                        {
                            try
                            {
                                // Limit concurrent conversions to prevent resource exhaustion
                                await conversionSemaphore.WaitAsync(cancellationToken);
                                
                                var startWait = DateTime.Now;
                                var eventTime = queuedAt;
                                var fileSettings = profileService.ActiveProfile.ImageFileSettings;
                                string baseDir = fileSettings.FilePath;
                                
                                Logger.Debug($"   [#{conversionNumber}] Started polling for Canon RAW file");
                                
                                // Poll for the Canon RAW file (CR3, CR2, or CRW)
                                FileInfo rawFile = null;
                                int attempts = 0;

                                // Check every 1000ms for up to 60 seconds
                                for (int i = 0; i < 60 && rawFile == null; i++)
                                {
                                    await Task.Delay(1000, cancellationToken);
                                    attempts++;
                                    
                                    // Look for the NEXT unprocessed Canon RAW file
                                    var rawPatterns = new[] { "*.cr3", "*.cr2", "*.crw" };
                                    var foundFiles = rawPatterns
                                        .SelectMany(pattern => Directory.GetFiles(baseDir, pattern, SearchOption.AllDirectories))
                                        .Select(f => new FileInfo(f))
                                        .Where(fi => Math.Abs((fi.LastWriteTime - eventTime).TotalSeconds) < 15)
                                        .Where(fi => !processedFiles.ContainsKey(fi.FullName))  // Skip already processed files
                                        .OrderBy(fi => fi.LastWriteTime)  // Get OLDEST unprocessed file (FIFO order)
                                        .FirstOrDefault();
                                    
                                    if (foundFiles != null && (DateTime.Now - foundFiles.LastWriteTime).TotalMilliseconds > 500)
                                    {
                                        // File exists and has been stable for 500ms
                                        // Atomically claim this file
                                        if (processedFiles.TryAdd(foundFiles.FullName, true))
                                        {
                                            rawFile = foundFiles;
                                            taskInfo.FileName = Path.GetFileNameWithoutExtension(foundFiles.Name);
                                            
                                            var waitTime = (DateTime.Now - startWait).TotalMilliseconds;
                                            var fileExt = Path.GetExtension(foundFiles.Name).ToUpper();
                                            Logger.Debug($"   [#{conversionNumber}] Found {fileExt} after {attempts} attempts ({waitTime:F0}ms)");
                                        }
                                    }
                                }

                                if (rawFile != null)
                                {
                                    string rawDir = Path.GetDirectoryName(rawFile.FullName);
                                    string rawName = Path.GetFileNameWithoutExtension(rawFile.Name);
                                    string rawExt = Path.GetExtension(rawFile.Name).ToUpper();
                                    string fitsPath = Path.Combine(rawDir, rawName + ".fits");

                                    Logger.Info($"   [#{conversionNumber}] Found Canon RAW {rawExt}: {rawFile.FullName}");
                                    Logger.Info($"   [#{conversionNumber}] Saving FITS: {fitsPath}");

                                    taskInfo.Status = "Converting";
                                    
                                    // Use selected FITS engine and compression type
                                    bool useCfitsio = (FitsEngineIndex == 1);
                                    int compressionType = GetCFitsioCompressionConstant();
                                    await rawConverter.ConvertImageDataToFitsAsync(imageData, rawDir, rawFile.FullName, DeleteOriginalFiles, useCfitsio, compressionType);
                                    
                                    taskInfo.Status = "Completed";
                                    taskInfo.CompletedAt = DateTime.Now;
                                    var totalTime = (taskInfo.CompletedAt.Value - taskInfo.CreatedAt).TotalSeconds;
                                    
                                    Logger.Info($"✅ [#{conversionNumber}] FITS file saved successfully ({totalTime:F1}s total)");
                                }
                                else
                                {
                                    var totalWait = (DateTime.Now - startWait).TotalSeconds;
                                    taskInfo.Status = "Failed - RAW file not found";
                                    taskInfo.CompletedAt = DateTime.Now;
                                    Logger.Warning($"[#{conversionNumber}] Could not find recently saved Canon RAW file (CR3/CR2/CRW) after {attempts} attempts ({totalWait:F1}s)");
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                taskInfo.Status = "Cancelled";
                                taskInfo.CompletedAt = DateTime.Now;
                                Logger.Info($"[#{conversionNumber}] Conversion cancelled");
                            }
                            catch (Exception ex)
                            {
                                taskInfo.Status = $"Failed - {ex.Message}";
                                taskInfo.CompletedAt = DateTime.Now;
                                Logger.Error($"[#{conversionNumber}] Error in FITS conversion: {ex.Message}", ex);
                            }
                            finally
                            {
                                // Release the semaphore to allow next conversion
                                conversionSemaphore.Release();
                            }
                        });
                        
                        // Add to pending conversions for tracking
                        if (pendingConversions.TryAdd(conversionTask, taskInfo))
                        {
                            // Set up continuation to remove from dictionary when complete
                            _ = conversionTask.ContinueWith(t =>
                            {
                                TaskInfo removedInfo;
                                if (pendingConversions.TryRemove(t, out removedInfo))
                                {
                                    var elapsed = removedInfo.CompletedAt.HasValue 
                                        ? (removedInfo.CompletedAt.Value - removedInfo.CreatedAt).TotalSeconds 
                                        : -1;
                                    Logger.Debug($"   [#{conversionNumber}] Task removed from pending conversions (Status: {removedInfo.Status}, Remaining: {pendingConversions.Count})");
                                }
                            }, TaskScheduler.Default);
                        }
                        
                        // Don't wait for conversion to complete - process next item in queue
                        // This allows conversions to run concurrently (up to semaphore limit)
                        // but they'll always grab files in FIFO order
                    }
                }
                catch (OperationCanceledException)
                {
                    Logger.Info("Queue processor cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error in queue processor: {ex.Message}", ex);
                }
            }
            
            Logger.Info("Queue processor stopped");
        }

        /// <summary>
        #region Plugin Settings

        public bool AutoConvertEnabled
        {
            get => pluginSettings.GetValueBoolean(nameof(AutoConvertEnabled), true);
            set
            {
                pluginSettings.SetValueBoolean(nameof(AutoConvertEnabled), value);
                RaisePropertyChanged();
                
                Logger.Info($"Auto-convert {(value ? "enabled" : "disabled")}");
            }
        }

        public string OutputDirectory
        {
            get => pluginSettings.GetValueString(nameof(OutputDirectory), string.Empty);
            set
            {
                pluginSettings.SetValueString(nameof(OutputDirectory), value);
                RaisePropertyChanged();
            }
        }

        public bool DeleteOriginalFiles
        {
            get => pluginSettings.GetValueBoolean(nameof(DeleteOriginalFiles), false);
            set
            {
                pluginSettings.SetValueBoolean(nameof(DeleteOriginalFiles), value);
                RaisePropertyChanged();
            }
        }

        public int FitsEngineIndex
        {
            get => pluginSettings.GetValueInt32(nameof(FitsEngineIndex), 0);
            set
            {
                pluginSettings.SetValueInt32(nameof(FitsEngineIndex), value);
                RaisePropertyChanged();
                Logger.Info($"FITS engine changed to: {(value == 0 ? "CSharpFITS (NINA Standard)" : "NASA CFitsio")}");
            }
        }

        /// <summary>
        /// CFitsio compression type (only used when FitsEngineIndex == 1)
        /// 0 = RICE (11), 1 = GZIP_1 (21), 2 = GZIP_2 (22), 3 = HCOMPRESS_1 (41), 4 = NOCOMPRESS (-1)
        /// </summary>
        public int CFitsioCompressionType
        {
            get => pluginSettings.GetValueInt32(nameof(CFitsioCompressionType), 0);
            set
            {
                pluginSettings.SetValueInt32(nameof(CFitsioCompressionType), value);
                RaisePropertyChanged();
                string[] compressionNames = { "RICE", "GZIP Level 1", "GZIP Level 2", "HCOMPRESS", "None" };
                if (value >= 0 && value < compressionNames.Length)
                {
                    Logger.Info($"CFitsio compression method changed to: {compressionNames[value]}");
                }
            }
        }

        /// <summary>
        /// Convert compression index to CFitsio constant
        /// </summary>
        public int GetCFitsioCompressionConstant()
        {
            switch (CFitsioCompressionType)
            {
                case 0: return 11;  // RICE_1
                case 1: return 21;  // GZIP_1
                case 2: return 22;  // GZIP_2
                case 3: return 41;  // HCOMPRESS_1
                case 4: return -1;  // NOCOMPRESS
                default: return 11; // Default to RICE
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
