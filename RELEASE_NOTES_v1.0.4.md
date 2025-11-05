# Release Notes - Version 1.0.4

**Release Date:** November 2025  
**Type:** Enhancement

## Overview

Version 1.0.4 adds enhanced logging for the auto-delete raw file feature, making it easier to track which Canon RAW files are being deleted after FITS conversion.

## What Changed

### Enhancements

#### **Enhanced: File Deletion Logging**
- **Improvement**: Auto-delete function now logs the full file path when deleting Canon RAW files
- **Benefit**: Users can now see exactly which files were deleted in the NINA log
- **Log Examples**:
  - `Deleted original Canon RAW file: C:\path\to\image.cr2`
  - `Delete attempt 1 failed for C:\path\to\image.cr2, retrying...`
  - `Could not delete original file C:\path\to\image.cr2 after 5 attempts: [error]`

### Support

- Works with all Canon RAW formats: CR3, CR2, CRW
- Provides clear visibility into file deletion operations
- Helps troubleshoot permission issues or file lock problems

## Compatibility

- **NINA Version**: 3.0.0 or higher
- **Windows**: Windows 10/11 (64-bit)
- **Canon Cameras**: All Canon DSLR/Mirrorless with RAW support

## Upgrade Instructions

1. **From v1.0.3 or earlier:**
   - Run `.\install.ps1` in the plugin directory
   - OR manually extract and copy to `%LOCALAPPDATA%\NINA\Plugins\3.0.0\NINA.Plugin.Canon.EDSDK`
   - Restart NINA

2. **Verification:**
   - Go to NINA → Options → Plugins → Canon RAW to FITS Converter
   - Version should show `1.0.4.0`
   - Take a test exposure with auto-delete enabled
   - Check NINA log for detailed file deletion messages

## Known Issues

None at this time.

## Full Changelog

### v1.0.4 (November 2025)
- **[ENHANCEMENT]** Add full file path to raw file deletion logging
- **[ENHANCEMENT]** Improved visibility into auto-delete operations
- **[ENHANCEMENT]** Better troubleshooting information in NINA logs

### v1.0.3 (January 2025)
- **[BUG FIX]** Add missing zlib1.dll dependency for cfitsio compression
- **[BUG FIX]** Bundle all GCC runtime DLLs required by cfitsio.dll

### v1.0.2 (January 2025)
- **[FEATURE]** Added support for CR2 and CRW file formats
- **[FIX]** Multi-format file search using LINQ SelectMany

### v1.0.1 (Previous)
- Initial release with CR3 support

---

**This is a minor enhancement release with improved logging for better troubleshooting.**
