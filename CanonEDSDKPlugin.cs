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

            Logger.Info("Canon RAW to FITS Converter loaded successfully - will convert from in-memory data");
        }

        public override async Task Teardown()
        {
            // Cleanup resources
            imageSaveMediator.BeforeImageSaved -= ImageSaveMediator_BeforeImageSaved;
            profileService.ProfileChanged -= ProfileService_ProfileChanged;

            // Give a brief moment for any final tasks to be queued
            // This handles the race condition where the last image's BeforeImageSaved
            // event is still being processed when sequence ends
            if (!pendingConversions.IsEmpty)
            {
                Logger.Info("Sequence ended, waiting for any final conversions to be queued...");
                await Task.Delay(2000); // 2 second grace period
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

                // Store image and timestamp - we'll save after NINA creates the CR3
                var imageData = e.Image;
                // Use the image's exposure start time from metadata (matches NINA's filename timestamp)
                var exposureStart = imageData.MetaData.Image.ExposureStart;
                var eventTime = DateTime.Now;  // When BeforeImageSaved fires (close to when CR3 will be saved)
                
                // Increment counter and log
                var conversionNumber = Interlocked.Increment(ref totalConversionsQueued);
                
                Logger.Info($"📸 Canon camera detected, queuing FITS conversion #{conversionNumber}");
                Logger.Info($"   Camera: {cameraName}");
                Logger.Info($"   Exposure started: {exposureStart:HH:mm:ss.fff}");

                // Create task info for tracking (before creating the task)
                var taskInfo = new TaskInfo
                {
                    CreatedAt = DateTime.Now,
                    FileName = $"Conversion #{conversionNumber}",
                    Status = "Queued"
                };

                // Wait for NINA to save the CR3 file, then save FITS in same directory
                // Track this task to ensure it completes even at end of sequence
                var conversionTask = Task.Run(async () =>
                {
                    try
                    {
                        taskInfo.Status = "Waiting for semaphore";
                        
                        // Limit concurrent conversions to prevent resource exhaustion
                        await conversionSemaphore.WaitAsync();
                        
                        taskInfo.Status = "Processing";
                        
                        var startWait = DateTime.Now;
                        Logger.Debug($"   [Burst #{conversionNumber}] Started polling for Canon RAW file at {startWait:HH:mm:ss.fff}");
                        
                        // Poll for the Canon RAW file (CR3, CR2, or CRW) instead of fixed delay
                        string baseDir = fileSettings.FilePath;
                        FileInfo rawFile = null;
                        int attempts = 0;

                        // Check every 1000ms for up to 60 seconds (handles delayed I/O after many large files)
                        for (int i = 0; i < 60 && rawFile == null; i++)
                        {
                            await Task.Delay(1000);
                            attempts++;
                            
                            // Look for Canon RAW files (CR3, CR2, CRW) saved near when this event fired (within +/- 15 seconds)
                            // Use eventTime (not exposureStart) so long exposures work correctly
                            var rawPatterns = new[] { "*.cr3", "*.cr2", "*.crw" };
                            var foundFiles = rawPatterns
                                .SelectMany(pattern => Directory.GetFiles(baseDir, pattern, SearchOption.AllDirectories))
                                .Select(f => new FileInfo(f))
                                .Where(fi => Math.Abs((fi.LastWriteTime - eventTime).TotalSeconds) < 15)
                                .Where(fi => !processedFiles.ContainsKey(fi.FullName))  // Skip already processed files
                                .OrderBy(fi => Math.Abs((fi.LastWriteTime - eventTime).TotalSeconds))
                                .FirstOrDefault();
                            
                            if (foundFiles != null && (DateTime.Now - foundFiles.LastWriteTime).TotalMilliseconds > 500)
                            {
                                // File exists and has been stable for 500ms (wait for complete write)
                                // Atomically claim this file to prevent duplicate processing
                                var timeDelta = Math.Abs((foundFiles.LastWriteTime - eventTime).TotalSeconds);
                                if (timeDelta < 12.0)  // Must be within 12 seconds to match
                                {
                                    if (processedFiles.TryAdd(foundFiles.FullName, true))
                                    {
                                        rawFile = foundFiles;
                                        taskInfo.FileName = Path.GetFileNameWithoutExtension(foundFiles.Name);
                                    }
                                }
                                var waitTime = (DateTime.Now - startWait).TotalMilliseconds;
                                var fileExt = Path.GetExtension(foundFiles.Name).ToUpper();
                                Logger.Debug($"   [Burst #{conversionNumber}] Found {fileExt} after {attempts} attempts ({waitTime:F0}ms), timestamp delta: {Math.Abs((foundFiles.LastWriteTime - eventTime).TotalMilliseconds):F0}ms");
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
                    catch (Exception ex)
                    {
                        taskInfo.Status = $"Failed - {ex.Message}";
                        taskInfo.CompletedAt = DateTime.Now;
                        Logger.Error($"[#{conversionNumber}] Error in delayed FITS save: {ex.Message}", ex);
                    }
                    finally
                    {
                        // Release the semaphore to allow next conversion
                        conversionSemaphore.Release();
                    }
                });
                
                // CRITICAL: Add to pending conversions BEFORE returning from event handler
                // This ensures the task is tracked even if NINA immediately proceeds to teardown
                if (pendingConversions.TryAdd(conversionTask, taskInfo))
                {
                    Logger.Debug($"   [#{conversionNumber}] Task registered in pending conversions (Total pending: {pendingConversions.Count})");
                    
                    // Set up continuation to remove from dictionary when complete
                    // We intentionally don't await this - it's a fire-and-forget cleanup task
                    _ = conversionTask.ContinueWith(t =>
                    {
                        TaskInfo removedInfo;
                        if (pendingConversions.TryRemove(t, out removedInfo))
                        {
                            var elapsed = removedInfo.CompletedAt.HasValue 
                                ? (removedInfo.CompletedAt.Value - removedInfo.CreatedAt).TotalSeconds 
                                : -1;
                            Logger.Debug($"   [#{conversionNumber}] Task removed from pending conversions (Status: {removedInfo.Status}, Elapsed: {elapsed:F1}s, Remaining: {pendingConversions.Count})");
                        }
                    }, TaskScheduler.Default);
                }
                else
                {
                    Logger.Warning($"   [#{conversionNumber}] Failed to register task in pending conversions!");
                }
                
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in BeforeImageSaved handler: {ex.Message}", ex);
            }
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
