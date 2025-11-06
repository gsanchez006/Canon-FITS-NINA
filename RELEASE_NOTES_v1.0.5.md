# Canon RAW to FITS Converter - v1.0.5.0 Release Notes

**Release Date:** November 5, 2025

## Overview
v1.0.5 introduces a critical fix for long image sequences where the last images in a burst were not converting due to file mismatching issues.

## What's Fixed

### 🔧 Critical Bug Fix: Last Image Conversion Failure
**Issue:** When performing long sequences of shots (30+ images), the last image(s) in the sequence would fail to convert to FITS format, leaving RAW files without corresponding FITS conversions.

**Root Cause:** The plugin was using timestamp-based file matching to associate queued conversion tasks with saved RAW files. During burst sequences, timing jitter caused task #N+1 to claim the file meant for task #N, resulting in skipped conversions.

**Solution:** Implemented a **queue-based sequential processing system**:
- All image save events are now added to a `ConcurrentQueue<PendingImageData>` in chronological order
- A background `ProcessQueueAsync()` worker processes items FIFO (First-In-First-Out)
- Files are matched by their position in the queue, not by timestamp proximity
- Concurrent processing preserved: Up to 3 conversions run in parallel using `SemaphoreSlim(3, 3)` for efficiency
- Proper queue drainage on shutdown ensures no conversions are lost

**Result:** ✅ All images now convert successfully, including the last images of long sequences

## Technical Details

### Architecture Changes
- **Removed:** Timestamp-based matching that spawned independent tasks
- **Added:** `ConcurrentQueue<PendingImageData>` for FIFO queueing
- **Added:** `ProcessQueueAsync(CancellationToken)` background worker
- **Added:** `queueSemaphore` for inter-task signaling
- **Modified:** `ImageSaveMediator_BeforeImageSaved()` now only enqueues items
- **Modified:** `Teardown()` now waits for queue drainage before plugin unload

### Performance
- No performance degradation
- Concurrent processing maintained (3 concurrent conversions)
- Completion order may vary based on conversion complexity, but FIFO guarantee ensures correct file matching

## Testing

Production testing with 90 consecutive images confirmed:
- All 90 conversions completed successfully
- Last images (#30, #60, #90) converted without issues
- Out-of-order completion times observed (expected behavior with concurrent processing)
- Queue-based architecture prevents file mismatches regardless of conversion duration

## Compatibility

- ✅ NINA 3.0+
- ✅ .NET 8.0
- ✅ All Canon RAW formats (CR2, CR3, CRW)
- ✅ Windows 10/11 (x64)

## Known Limitations

None new in this release. See previous releases for existing limitations.

## Future Improvements

- Consider adding configurable concurrency limit
- Add queue depth monitoring/logging for diagnostics
- Potential performance optimization for very large bursts (100+ images)

---

**Questions or Issues?** Please report on [GitHub Issues](https://github.com/gsanchez006/Canon-FITS-NINA/issues)
