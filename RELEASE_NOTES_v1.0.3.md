# Release Notes - Version 1.0.3

**Release Date:** January 2025  
**Type:** Critical Bug Fix

## Overview

Version 1.0.3 addresses a critical production deployment issue where cfitsio compression failed on systems without Visual Studio or Visual C++ runtime installed. This release bundles the required runtime dependencies to ensure cfitsio works on all Windows systems.

## What Changed

### Bug Fixes

#### **Fixed: cfitsio.dll Fails to Load on Production Systems**
- **Issue**: cfitsio compression would fail with error 0x8007007E on production systems
- **Root Cause**: Missing GCC runtime dependencies - cfitsio.dll was built with GCC (MSYS2/UCRT64), not MSVC
- **Missing Dependency**: zlib1.dll (compression library that cfitsio requires)
- **Resolution**: Now bundles all required GCC runtime DLLs with the plugin package

**Error that occurred in v1.0.2:**
```
ERROR|CFitsioWriter.cs|WriteFitsFile|173|CFitsio writer error: 
Unable to load DLL 'cfitsio.dll' or one of its dependencies: 
The specified module could not be found. (0x8007007E)
```

**Now resolved in v1.0.3:**
- Plugin includes all GCC/UCRT64 runtime DLLs
- zlib1.dll (compression) now bundled
- cfitsio loads successfully on all Windows systems
- No user action required (dependencies bundled automatically)

## Technical Details

### Build Changes

Modified `build.ps1` to automatically bundle GCC runtime DLLs from MSYS2/UCRT64:
- Copies libgcc_s_seh-1.dll (GCC runtime)
- Copies libstdc++-6.dll (C++ standard library)
- Copies libwinpthread-1.dll (POSIX threads)
- Copies zlib1.dll (compression library)
- Adds verification step to ensure all dependencies are present
- Distribution package now includes all required native dependencies

### Package Contents

The plugin package now includes:
- **NINA.Plugin.Canon.EDSDK.dll** (Plugin assembly)
- **cfitsio.dll** (FITS compression library)
- **GCC Runtime DLLs** (NEW):
  - libgcc_s_seh-1.dll (GCC runtime)
  - libstdc++-6.dll (C++ standard library)
  - libwinpthread-1.dll (POSIX threads)
  - zlib1.dll (compression library)
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
- **[BUG FIX]** Add missing zlib1.dll dependency for cfitsio compression
- **[BUG FIX]** Bundle all GCC runtime DLLs (libgcc_s_seh-1.dll, libstdc++-6.dll, libwinpthread-1.dll) required by cfitsio.dll
- **[BUILD]** Modified build.ps1 to copy GCC runtime dependencies from MSYS2/UCRT64
- **[BUILD]** Added runtime DLL verification to dependency checking

### v1.0.2 (January 2025)
- **[FEATURE]** Added support for CR2 and CRW file formats
- **[FIX]** Multi-format file search using LINQ SelectMany
- **[DOCS]** Comprehensive documentation and release preparation

### v1.0.1 (Previous)
- Initial release with CR3 support

---

**This is a recommended update for all users, especially those running NINA on systems without Visual Studio installed.**
