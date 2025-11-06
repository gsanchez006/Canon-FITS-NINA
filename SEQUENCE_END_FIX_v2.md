# Sequence End Fix - Last Image Not Converting

## Issue Identified
When running long sequences of shots, the **last shot was not being converted** to FITS format. Analysis of the production log revealed the root cause:

### Problem Analysis
1. **File Mismatch During Bursts**: The original implementation used timestamp-based file matching where each conversion task independently searched for "the next Canon RAW file saved near when my event fired"
2. **Race Condition**: During burst sequences with slow I/O:
   - Conversion task #32 queued at 18:37:54
   - But the CR3 file for #32 didn't save until 18:37:59 (5-second delay)
   - Conversion task #33 queued at 18:37:54 and started looking for files
   - Task #33 found and claimed the file that should have gone to #32
   - Task #32 never found its file and failed silently

### Evidence from Logs
```
2025-11-05T18:37:54.7889|INFO|CanonEDSDKPlugin.cs|ImageSaveMediator_BeforeImageSaved|220|📸 Canon camera detected, queuing FITS conversion #32
...
(5 seconds pass)
...
2025-11-05T18:37:59.4518|INFO|BaseImageData.cs|SaveToDisk|346|Saved image to ...0001.cr3
2025-11-05T18:37:59.4521|INFO|CanonEDSDKPlugin.cs|ImageSaveMediator_BeforeImageSaved|220|📸 Canon camera detected, queuing FITS conversion #33
2025-11-05T18:38:00.5571|INFO|CanonEDSDKPlugin.cs|ImageSaveMediator_BeforeImageSaved|296|   [#33] Found Canon RAW .CR3: ...0001.cr3
```

Notice: #33 found the file, but #32 never did. #32 was never completed (no "FITS file saved successfully" log entry).

## Solution Implemented

### Queue-Based Sequential Processing
Replaced the parallel timestamp-matching approach with a **strict FIFO queue system**:

1. **Single Processing Queue**: All image data is added to a `ConcurrentQueue<PendingImageData>` in the exact order BeforeImageSaved events fire
2. **Sequential File Matching**: A background task processes the queue one item at a time, always grabbing the **oldest unprocessed RAW file**
3. **Guaranteed Order**: Conversion #N will always match to File #N because:
   - Images are queued in order 1, 2, 3...
   - Files are processed in order from oldest to newest
   - Each file is atomically claimed to prevent double-processing

### Key Changes

#### New Data Structure
```csharp
internal class PendingImageData
{
    public IImageData ImageData { get; set; }
    public DateTime QueuedAt { get; set; }
    public int ConversionNumber { get; set; }
}
```

#### Queue Infrastructure
```csharp
// FIFO queue for sequential processing
private static readonly ConcurrentQueue<PendingImageData> imageQueue = new ConcurrentQueue<PendingImageData>();

// Semaphore to signal when new items added
private static readonly SemaphoreSlim queueSemaphore = new SemaphoreSlim(0);

// Background processing task
private Task queueProcessingTask;
private CancellationTokenSource queueCancellationSource;
```

#### BeforeImageSaved (Simplified)
Now just queues the image data instead of spawning independent tasks:
```csharp
var pendingImage = new PendingImageData
{
    ImageData = e.Image,
    QueuedAt = DateTime.Now,
    ConversionNumber = conversionNumber
};

imageQueue.Enqueue(pendingImage);
queueSemaphore.Release();  // Signal processor
```

#### ProcessQueueAsync (New Method)
Background task that:
1. Waits for queue items (via semaphore)
2. Dequeues next item
3. Waits for the **oldest unprocessed RAW file**:
   ```csharp
   .OrderBy(fi => fi.LastWriteTime)  // FIFO order - oldest first
   .FirstOrDefault();
   ```
4. Processes conversion (still allows 3 concurrent conversions via semaphore)
5. Repeats until cancelled

### Benefits

1. ✅ **Guaranteed Order**: Files are always matched to conversions in FIFO order
2. ✅ **No Missed Files**: Even if files save slowly, they'll eventually be processed
3. ✅ **Handles Sequence End**: The queue processor continues running until all queued conversions complete
4. ✅ **Concurrent Processing**: Still allows up to 3 conversions to run concurrently for performance
5. ✅ **Teardown Safety**: Updated Teardown() method waits for queue to drain before plugin unloads

## Testing Recommendations

Test with:
1. **Long sequences** (30+ images) to verify last image converts
2. **Burst sequences** with fast capture rates to stress-test the queue
3. **Multiple filter changes** to ensure queue handles sequence transitions
4. **Sequence interruption** to verify teardown behavior

## Files Modified

- `CanonEDSDKPlugin.cs`: Complete rewrite of conversion queueing and processing logic

## Version

This fix will be included in version 1.0.5
