# Release Notes - Version 1.0.2

**Release Date:** November 4, 2025

## 🎉 What's New in v1.0.2

This release adds support for older Canon RAW formats, enabling the plugin to work with all Canon EOS cameras from 2003 onwards.

### ✨ New Features

#### **Full Canon RAW Format Support**
The plugin now correctly handles **all Canon RAW formats**:
- **CR3**: Canon RAW version 3 (EOS R, RP, R5, R6, R7, R8, R10, R50, R100, etc.)
- **CR2**: Canon RAW version 2 (EOS 5D/6D/7D series, 80D, 90D, etc.)
- **CRW**: Original Canon RAW format (EOS 10D, 20D, 30D, 300D, 350D, 400D, etc.)

**Before v1.0.2:** Only CR3 files were recognized  
**After v1.0.2:** All Canon RAW formats are recognized ✅

### 🐛 Bug Fixes

#### **Fixed: CR2/CRW Files Not Being Converted**
- **Issue:** Plugin only searched for `.cr3` files, causing CR2 and CRW files to be ignored
- **Solution:** Updated file search to handle all Canon RAW formats simultaneously
- **Impact:** Older Canon cameras now fully supported

#### **Improved RAW File Detection**
- Multi-format search using LINQ SelectMany for efficiency
- Better error messages showing which format was expected but not found
- Logs now display the actual RAW format detected (.CR3/.CR2/.CRW)

### 📝 Code Changes

#### **CanonEDSDKPlugin.cs**
- Replaced hardcoded CR3-only search with multi-format pattern array
- Variable renaming for clarity:
  - `cr3Files` → `rawFile`
  - `cr3Dir` → `rawDir`
  - Added file extension tracking for accurate logging
- Enhanced logging to show detected RAW format
- Updated comments to reflect multi-format support

**Line 257-263 (Core Fix):**
```csharp
// OLD (v1.0.1): Only searched for CR3
var foundFiles = Directory.GetFiles(baseDir, "*.cr3", SearchOption.AllDirectories)

// NEW (v1.0.2): Searches for all Canon RAW formats
var rawPatterns = new[] { "*.cr3", "*.cr2", "*.crw" };
var foundFiles = rawPatterns
    .SelectMany(pattern => Directory.GetFiles(baseDir, pattern, SearchOption.AllDirectories))
```

#### **Services/RawToFitsConverter.cs**
- Updated documentation strings to reference "Canon RAW files" instead of CR3 specifically
- Improved comments for clarity

### 📊 Technical Details

#### **What This Means**
✅ CR3 support: **Already worked**  
❌ CR2 support: **Was broken** - Now fixed  
❌ CRW support: **Was broken** - Now fixed  

#### **How It Works**
1. Creates array of RAW format patterns: `["*.cr3", "*.cr2", "*.crw"]`
2. Uses LINQ `SelectMany` to search for all patterns simultaneously
3. Returns first match found (ordered by timestamp proximity)
4. Same atomic processing prevents duplicates across all formats

#### **Performance**
- **Minimal overhead**: LINQ deferred execution stops at first match
- **Cached**: Already-processed files tracked to avoid re-scanning
- **Async**: File polling continues in background without blocking NINA

### 🧪 Testing Recommendations

**Test with CR2 files:**
1. Connect Canon EOS 6D/5D/80D or similar (produces CR2)
2. Enable plugin and capture test image
3. Verify FITS file created with log message: `"Found Canon RAW .CR2: ..."`

**Test with CRW files:**
1. Connect Canon EOS 20D/10D or similar (produces CRW)
2. Enable plugin and capture test image
3. Verify FITS file created with log message: `"Found Canon RAW .CRW: ..."`

**Test with CR3 files:**
1. Verify existing CR3 support still works (EOS R, RP, R5, R6, etc.)
2. Should see: `"Found Canon RAW .CR3: ..."`

**Burst mode testing:**
- Test with 30+ images to ensure all formats handle race conditions

### 📋 Sample Log Output

```
📸 Canon camera detected, queuing FITS conversion #1
   Camera: Canon EOS 80D
   [Burst #1] Started polling for Canon RAW file at 21:45:30.123
   [Burst #1] Found .CR2 after 2 attempts (2000ms)
   [#1] Found Canon RAW .CR2: C:\Images\Light_001.CR2
   [#1] Saving FITS: C:\Images\Light_001.fits
✅ [#1] FITS file saved successfully (3.2s total)
```

### 🔄 Compatibility

**100% Backward Compatible**
- All existing CR3 functionality unchanged
- No API changes
- All plugin settings work identically
- Fully compatible with NINA 3.0.0+

**Camera Support Matrix**

| Camera Series | RAW Format | Status |
|---------------|-----------|--------|
| EOS R, RP, R5, R6, R7, R8, R10, R50, R100 | CR3 | ✅ Fully Supported |
| EOS 5D/5Ds, 6D/6D II, 7D/7D II, 1D/1Dx | CR2 | ✅ Fully Supported |
| EOS 80D, 90D, 77D, M50, M100, M200 | CR2 | ✅ Fully Supported |
| EOS 10D, 20D, 30D, 300D, 350D, 400D | CRW | ✅ Fully Supported |
| PowerShot Pro, G-series | CRW/CR2 | ✅ Fully Supported |

### 📦 Installation

See README.md for installation instructions. No changes required - same installation process as v1.0.1.

### 🎯 What's Next?

- Continued support for new Canon RAW formats as they're released
- Performance optimizations based on user feedback
- Additional FITS header metadata options

---

**Questions or Issues?** Report on GitHub: https://github.com/gsanchez006/Canon-FITS-NINA/issues
