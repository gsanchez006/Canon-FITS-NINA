# Project Restructure Summary - Windows-Native Canon FITS Plugin

**Date:** November 7, 2025  
**Plugin Version:** 1.0.5+  
**Major Changes:** Complete restructure for Windows-native dependencies and easy upgrades

---

## Overview

This project has been completely restructured to:
1. Use **only Windows-native dependencies** (no GCC/MSYS2)
2. Include **complete Canon EDSDK** with DPP4Lib and IHL
3. Make **dependency upgrades simple** with versioned folder structure
4. Be **fully self-contained** with all required DLLs

---

## Major Changes

### 1. New Dependency Structure

**Before:**
```
lib/
├── cfitsio.dll
├── zlib.dll
├── CSharpFITS_v1.1.dll
├── EDSDK.dll
└── EdsImage.dll

EDSDK/  (empty or incomplete)
```

**After:**
```
dependencies/
├── cfitsio-4.6.3/
│   └── x64/
│       ├── cfitsio.dll (MSVC build)
│       └── zlib.dll (NuGet zlib-msvc-x64)
├── edsdk-13.19.0/
│   └── x64/
│       ├── EDSDK.dll
│       ├── EdsImage.dll
│       ├── DPP4Lib/ (31 DLLs + subfolders)
│       └── IHL/ (13 DLLs)
└── CSharpFITS_v1.1.dll
```

**Benefits:**
- Version numbers in folder names for easy tracking
- All dependencies in one place
- Simple to upgrade (just copy new version folder)
- Clear separation by library type

### 2. Windows-Native Dependencies

**Removed GCC/MSYS2 Dependencies:**
- ❌ libgcc_s_seh-1.dll
- ❌ libstdc++-6.dll
- ❌ libwinpthread-1.dll
- ❌ zlib1.dll (GCC version)

**Now Using Windows-Native (MSVC):**
- ✅ cfitsio.dll - Built with Visual Studio 2022
- ✅ zlib.dll - NuGet zlib-msvc-x64 package
- ✅ All Canon EDSDK DLLs - Native Windows

**No external runtime dependencies required!**

### 3. Complete Canon EDSDK Integration

**New in This Version:**
- **DPP4Lib** (31 DLLs) - Canon RAW processing library
  - Supports CR2, CR3, CRW formats
  - Includes color profiles (ICC files)
  - Camera-specific processing models
  - CUDA acceleration support
- **IHL** (13 DLLs) - Canon Image Handling Library
  - Metadata parsing
  - EXIF/IPTC/XMP support
  - Color management

**Package Size:**
- Before: ~6 DLLs
- After: 134+ files (plugin DLL + all dependencies)

### 4. Updated Build System

**Changes to `NINA.Plugin.Canon.EDSDK.csproj`:**
- Excluded EDSDK raw distribution folder from compilation
- Updated Include paths to use `dependencies/` structure
- Proper LinkBase for maintaining folder structures (DPP4Lib, IHL)
- All DLLs automatically copied to output directory

**Changes to `build.ps1`:**
- Simplified dependency verification
- Removed GCC runtime copying logic
- Clear Windows-native dependency comments
- Better error handling for missing dependencies

**Build Output:**
- Plugin DLL + 5 core DLLs in root
- DPP4Lib/ folder with complete structure
- IHL/ folder with all DLLs
- README.md and LICENSE

### 5. Documentation Cleanup

**Created:**
- `DEPENDENCY_UPGRADE_GUIDE.md` - Comprehensive upgrade instructions

**Archived to `docs/archive/`:**
- BUILD_SCRIPTS_FIX.md
- CFITSIO_DLL_FIX.md
- CODE_REVIEW_SUMMARY.md
- CR2_CRW_SUPPORT_FIX.md
- EDSDK_INTEGRATION_COMPLETE.md
- FIX_SUMMARY.md
- GITHUB_RELEASE_v1.0.2.md
- RELEASE_v1.0.2_* (5 files)
- SEQUENCE_END_FIX* (2 files)
- VERSION_1.0.3_SUMMARY.md
- VISUAL_FIX_GUIDE.md
- v1.0.5_RELEASE_SUMMARY.md

**Kept in Root:**
- README.md (updated for Windows-native)
- DEPENDENCY_UPGRADE_GUIDE.md (new)
- CFITSIO_BZERO_BUG_ANALYSIS.md (important bug fix documentation)
- RELEASE_NOTES*.md (version history)
- LICENSE

---

## Build Instructions

### Quick Build

```powershell
.\build.ps1
```

Output: `package\NINA.Plugin.Canon.EDSDK.zip`

### Install to NINA

```powershell
.\install.ps1
```

Or manually extract ZIP to:
```
%LOCALAPPDATA%\NINA\Plugins\3.0.0\NINA.Plugin.Canon.EDSDK\
```

---

## Dependency Upgrade Process

See `DEPENDENCY_UPGRADE_GUIDE.md` for complete instructions.

**Quick Summary:**

### Upgrading CFitsio:
1. Build new version with Visual Studio + CMake
2. Get zlib from NuGet (zlib-msvc-x64)
3. Copy DLLs to `dependencies/cfitsio-X.Y.Z/x64/`
4. Update `.csproj` Include paths
5. Build and test

### Upgrading Canon EDSDK:
1. Download new EDSDK from Canon
2. Copy `EDSDK_64/Dll/` contents to `dependencies/edsdk-X.Y.Z/x64/`
3. Update `.csproj` Include paths
4. Build and test

---

## Package Contents

The distribution ZIP now contains **134 files**:

**Root (6 core DLLs + plugin):**
- NINA.Plugin.Canon.EDSDK.dll
- cfitsio.dll
- zlib.dll
- EDSDK.dll
- EdsImage.dll
- CSharpFITS_v1.1.dll

**DPP4Lib/ (31 DLLs + ICC profiles + models):**
- Canon RAW processing engine
- CR2/CR3/CRW decoders
- Color profiles for various camera models
- CUDA GPU acceleration libraries

**IHL/ (13 DLLs):**
- Image metadata handling
- EXIF/IPTC/XMP parsing
- Color management

**Documentation:**
- README.md
- LICENSE

---

## Testing Results

✅ **Build Status:** SUCCESS  
✅ **All Dependencies Verified:** Yes  
✅ **Package Created:** Yes (134 files)  
✅ **Windows-Native:** 100% (no GCC dependencies)  
✅ **Self-Contained:** Yes (all DLLs included)

**Dependency Verification:**
```
- cfitsio.dll: OK
- zlib.dll: OK
- EDSDK.dll: OK
- EdsImage.dll: OK
- CSharpFITS_v1.1.dll: OK
- DPP4Lib folder: OK (31 DLLs + subfolders)
- IHL folder: OK (13 DLLs)
```

---

## Breaking Changes

⚠️ **Old Plugins Will Not Work**

If upgrading from version 1.0.5 or earlier:
1. Completely uninstall old plugin
2. Delete old plugin folder
3. Install new version fresh
4. Configure settings again

---

## Known Issues

None. All Windows dependencies are now native (MSVC built).

---

## Future Improvements

- Automate CFitsio builds with CI/CD
- Create version update checker
- Add dependency validation on plugin load
- Create installer package

---

## Version History

- **v1.0.5+**: Complete restructure with Windows-native dependencies
- **v1.0.5**: Added BZERO/BSCALE bug fix (USHORT_IMG)
- **v1.0.4**: GCC runtime dependencies bundled
- **v1.0.3**: CFitsio compression support
- **v1.0.2**: Initial CFitsio integration
- **v1.0.1**: CSharpFITS engine
- **v1.0.0**: Initial release

---

## Credits

- **NASA CFitsio**: https://heasarc.gsfc.nasa.gov/fitsio/
- **Canon EDSDK**: Canon Inc.
- **CSharpFITS**: https://github.com/csharpfits
- **NINA**: https://nighttime-imaging.eu/
- **zlib**: Mark Adler & Jean-loup Gailly (via NuGet zlib-msvc-x64)

---

## License

See LICENSE file for details.

---

## Support

For issues or questions:
1. Check `DEPENDENCY_UPGRADE_GUIDE.md` for upgrade instructions
2. Review `CFITSIO_BZERO_BUG_ANALYSIS.md` for FITS format details
3. Check NINA logs: `%LOCALAPPDATA%\NINA\Logs\`
4. Review build output for errors
5. Verify all DLLs present in plugin directory
