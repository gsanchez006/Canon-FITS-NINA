# Release Notes - Version 1.0.3

**Release Date:** January 2025  
**Type:** Critical Bug Fix

## Overview

Version 1.0.3 addresses a critical production deployment issue where cfitsio compression failed on systems without Visual Studio or Visual C++ runtime installed. This release bundles the required runtime dependencies to ensure cfitsio works on all Windows systems.

## What Changed

### Bug Fixes

#### **Fixed: cfitsio.dll Fails to Load on Clean Windows Systems**
- **Issue**: On systems without Visual Studio, cfitsio compression would fail with error 0x8007007E
- **Root Cause**: Missing Visual C++ runtime dependencies (vcruntime140.dll, msvcp140.dll)
- **Impact**: Plugin would fall back to CSharpFITS, losing FITS compression capability
- **Resolution**: Now bundles VC++ runtime DLLs with the plugin package

**Error that occurred in v1.0.2:**
```
ERROR|CFitsioWriter.cs|WriteFitsFile|173|CFitsio writer error: 
Unable to load DLL 'cfitsio.dll' or one of its dependencies: 
The specified module could not be found. (0x8007007E)

WARNING|RawToFitsConverter.cs|ConvertImageDataToFitsAsync|59|
cfitsio writer failed, falling back to CSharpFITS
```

**Now resolved in v1.0.3:**
- Plugin includes vcruntime140.dll and msvcp140.dll
- cfitsio loads successfully on all Windows systems
- No user action required (dependencies bundled automatically)

## Technical Details

### Build Changes

Modified `build.ps1` to automatically bundle Visual C++ runtime DLLs:
- Copies vcruntime140.dll from System32
- Copies msvcp140.dll from System32
- Adds verification step to ensure all dependencies are present
- Distribution package now includes all required native dependencies

### Package Contents

The plugin package now includes:
- **NINA.Plugin.Canon.EDSDK.dll** (Plugin assembly)
- **cfitsio.dll** (FITS compression library)
- **vcruntime140.dll** (VC++ runtime - NEW)
- **msvcp140.dll** (VC++ C++ library - NEW)
- **CSharpFITS_v1.1.dll** (Fallback FITS writer)
- **EDSDK.dll** (Canon SDK)
- **EdsImage.dll** (Canon image library)
- **EDSDK folder** (Canon SDK resources)

## Compatibility

- **NINA Version**: 3.0.0 or higher
- **Windows**: Windows 10/11 (64-bit)
- **No Prerequisites**: VC++ runtime bundled (no separate installation needed)
- **Canon Cameras**: All Canon DSLR/Mirrorless with RAW support

## Upgrade Instructions

1. **From v1.0.2 or earlier:**
   - Run `.\install.ps1` in the plugin directory
   - OR manually extract and copy to `%LOCALAPPDATA%\NINA\Plugins\3.0.0\NINA.Plugin.Canon.EDSDK`
   - Restart NINA

2. **Verification:**
   - Go to NINA → Options → Plugins → Canon RAW to FITS Converter
   - Version should show `1.0.3.0`
   - Take a test exposure with "NASA CFitsio" writer
   - Check NINA log - should see "Creating FITS file with CFitsio" (no errors)

## Known Issues

None at this time.

## Acknowledgments

This fix was identified through production deployment testing and log analysis comparing development vs. production environments.

## Support

For issues, please:
1. Check NINA application log: `%LOCALAPPDATA%\NINA\Logs\<date>.log`
2. Look for "CFitsioWriter" or "cfitsio" error messages
3. Report issues on GitHub: https://github.com/gsanchez006/Canon-FITS-NINA

---

## Full Changelog

### v1.0.3 (January 2025)
- **[BUG FIX]** Bundle Visual C++ runtime DLLs (vcruntime140.dll, msvcp140.dll) to fix cfitsio loading on production systems
- **[BUILD]** Modified build.ps1 to automatically copy VC++ runtime dependencies
- **[BUILD]** Added runtime DLL verification to dependency checking

### v1.0.2 (January 2025)
- **[FEATURE]** Added support for CR2 and CRW file formats
- **[FIX]** Multi-format file search using LINQ SelectMany
- **[DOCS]** Comprehensive documentation and release preparation

### v1.0.1 (Previous)
- Initial release with CR3 support

---

**This is a recommended update for all users, especially those running NINA on systems without Visual Studio installed.**
