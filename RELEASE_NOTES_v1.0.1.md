# Release Notes - Version 1.0.1

**Release Date:** November 1, 2025

## 🎉 What's New in v1.0.1

This release includes critical bug fixes for sequence handling and makes the project fully self-contained.

### 🐛 Critical Bug Fixes

#### **Fixed: Last File in Burst Sequences Not Converting**
- **Issue:** In rare cases with 30+ very short exposure shots, the last file would not create a FITS file
- **Root Cause:** Race condition between NINA sequence completion and plugin's async task processing
- **Solution:** 
  - Improved task tracking with `ConcurrentDictionary` (replaces `ConcurrentBag`)
  - Tasks now registered BEFORE event handler returns
  - Added 2-second grace period in teardown for final tasks to queue
  - Comprehensive diagnostics with task numbering and status tracking
  - Automatic task cleanup on completion

**Impact:** Last file in burst sequences will now ALWAYS be processed ✅

### 📦 Project Self-Contained

#### **EDSDK Files Now Included**
- All Canon EDSDK files now included in repository
- Project is fully self-contained - no external dependencies
- Organized structure:
  - `lib/` - EDSDK.dll, EdsImage.dll, CSharpFITS_v1.1.dll
  - `EDSDK/` - DPP4Lib and IHL folders

### 🔧 Build Script Improvements

#### **Fixed Build Scripts**
- **build.bat** - Fixed MSBuild error, now specifies project file explicitly
- **build.ps1** - New PowerShell version with color-coded output
- Both scripts now verify all dependencies with OK/MISSING status
- Removed external dependency references
- All files now sourced from project directory only

#### **Dependency Verification**
Build now shows clear status for all components:
```
Verifying dependencies:
  - CSharpFITS_v1.1.dll: OK
  - EDSDK.dll: OK
  - EdsImage.dll: OK
  - cfitsio.dll: OK
  - EDSDK\DPP4Lib: OK
  - EDSDK\IHL: OK
```

### 📊 Technical Improvements

#### **Enhanced Task Tracking**
- Added `TaskInfo` class to track:
  - Creation time
  - Filename
  - Status (Queued → Waiting → Processing → Converting → Completed)
  - Completion time
- Each conversion numbered for log correlation
- Total conversions counter for verification

#### **Improved Teardown Logic**
- 2-second grace period before checking pending tasks
- Detailed logging of pending conversions (filename, status, elapsed time)
- Better timeout handling with specific task reporting
- Clear summary: "Total conversions queued: N"

#### **Comprehensive Diagnostics**
Enhanced logging throughout:
- Task queued (with filename and number)
- Task started processing
- Task completed/failed with timing
- Current pending count at various stages
- Task lifecycle status updates

### 📝 Code Changes Summary

**Files Modified:**
- `CanonEDSDKPlugin.cs` - Main plugin (~220 lines changed)
  - Added `TaskInfo` class
  - Improved task tracking with `ConcurrentDictionary`
  - Enhanced `Teardown()` method
  - Rewrote `BeforeImageSaved()` handler
- `Properties\AssemblyInfo.cs` - Version updated to 1.0.1
- `build.bat` - Fixed and improved
- `build.ps1` - Created new PowerShell version

**Files Added:**
- `lib\EDSDK.dll` (1.6 MB)
- `lib\EdsImage.dll` (1.1 MB)
- `EDSDK\DPP4Lib\*` (~20 files, ~15 MB)
- `EDSDK\IHL\*` (~5 files, ~2 MB)
- `SEQUENCE_END_FIX.md` - Technical documentation
- `VISUAL_FIX_GUIDE.md` - Visual guide to the fix
- `FIX_SUMMARY.md` - Quick reference
- `BUILD_SCRIPTS_FIX.md` - Build script documentation
- `EDSDK_INTEGRATION_COMPLETE.md` - Integration documentation

### 🎯 Testing Recommendations

1. **Burst Sequence Test** (Primary Issue)
   - Configure very short exposures (0.017s - 0.1s)
   - Run sequence of 30+ images
   - Verify all CR3 files have corresponding FITS files
   - Check log shows all conversions completed

2. **Log Verification**
   - Each image shows "Queuing FITS conversion #N"
   - All tasks show "Task removed from pending conversions"
   - Final log shows correct total count

3. **Stress Test**
   - Run sequence of 50+ images
   - Verify no memory growth
   - Verify all images convert successfully

### 📦 Distribution Package

**Package Size:** ~25 MB (increased from ~5 MB due to EDSDK files)

**Includes:**
- Main plugin DLL
- CSharpFITS library
- CFitsio library  
- EDSDK DLLs (EDSDK.dll, EdsImage.dll)
- Canon DPP4 libraries (20+ files)
- Canon IHL libraries
- Documentation (README, LICENSE)

### 🔄 Upgrade Instructions

1. Download `NINA.Plugin.Canon.EDSDK.zip` from this release
2. Close NINA
3. Extract ZIP and copy to: `%LOCALAPPDATA%\NINA\Plugins\3.0.0\NINA.Plugin.Canon.EDSDK\`
4. Restart NINA

Or use the install script:
```powershell
.\install.ps1
```

### ⚠️ Breaking Changes

**None** - This release is fully backward compatible with v1.0.0

### 🐛 Known Issues

- Build warnings about .NET Framework compatibility are expected (NINA packages)
- Warning about async method without await (line 189) is expected and correct

### 📚 Documentation

New documentation added:
- [Sequence End Fix](SEQUENCE_END_FIX.md) - Detailed technical documentation
- [Visual Fix Guide](VISUAL_FIX_GUIDE.md) - Visual diagrams of the fix
- [Build Scripts Fix](BUILD_SCRIPTS_FIX.md) - Build system documentation
- [EDSDK Integration](EDSDK_INTEGRATION_COMPLETE.md) - Integration details

### 🙏 Acknowledgments

Thanks to all users who reported the burst sequence issue. Your detailed reports were instrumental in identifying and fixing this race condition.

---

## Previous Versions

### Version 1.0.0 (October 22, 2025)
- Initial release
- Dual FITS engine support (CSharpFITS and CFitsio)
- Full NINA metadata preservation (70+ headers)
- Compression support: RICE, GZIP, HCOMPRESS
- Automatic Canon camera detection
- In-memory conversion
- Support for CR2, CR3, CRW formats

---

**Full Changelog:** https://github.com/gsanchez006/Canon-FITS-NINA/compare/v1.0.0...v1.0.1
