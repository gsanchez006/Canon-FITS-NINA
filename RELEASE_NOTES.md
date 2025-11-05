# Canon EDSDK to FITS Converter - CFitsio Edition

## Version 1.0.3 - January 2025 (CURRENT)

### 🔧 Critical Bug Fix Release

**Fixed: cfitsio.dll Loading Failure on Production Systems**
- Bundles Visual C++ runtime dependencies (vcruntime140.dll, msvcp140.dll)
- Fixes error 0x8007007E on systems without Visual Studio installed
- No user action required - runtime DLLs included automatically
- See [RELEASE_NOTES_v1.0.3.md](RELEASE_NOTES_v1.0.3.md) for details

---

## Version 1.0.2 - January 2025

### 🎯 Multi-Format Support Enhancement

**Added: CR2 and CRW File Format Support**
- Extended detection to support older Canon RAW formats (CR2, CRW)
- Improved file search using LINQ SelectMany for multiple extensions
- Comprehensive documentation and release preparation
- See [RELEASE_NOTES_v1.0.2.md](RELEASE_NOTES_v1.0.2.md) for details

---

## Version 1.0.0 - October 2025

### Release Date: October 22, 2025

## Overview
A professional NINA plugin that converts Canon RAW images (CR2, CR3) to FITS format using NASA's CFitsio 4.6.3 library with optional compression support. Preserves full 14/16-bit dynamic range without demosaicing or processing.

## Key Features

### ✨ Image Conversion
- **Direct In-Memory Conversion**: Converts from NINA's image data without disk I/O
- **Full 14/16-bit Preservation**: No bit depth loss or demosaicing
- **No Processing**: Raw Bayer pattern data preserved exactly as captured
- **Canon RAW Support**: Works with CR2, CR3, CRW formats

### 🔧 Compression Options
- **RICE Compression**: Lossless, 50-60% file size reduction
- **GZIP Level 1 & 2**: Zlib compression, variable compression ratio
- **HCOMPRESS**: Rice/hcompress algorithm, lossless
- **Uncompressed Option**: For maximum compatibility

### 📊 FITS File Quality
- **FITS 4.0 Standard Compliant**: Compressed image extensions (ZTILE format)
- **Full Metadata Preservation**: All NINA and camera metadata included
- **Bayer Pattern Keywords**: BAYERPAT, XBAYROFF, YBAYROFF properly set
- **Primary HDU Information**: Dimension and compatibility comments

### 🚀 Burst Sequence Support
- **Handles High-Speed Sequences**: 30+ images at 0.017s exposures
- **Semaphore-Based Throttling**: 3 concurrent conversions (customizable)
- **Pending Task Tracking**: Ensures all images convert before shutdown
- **Atomic File Processing**: Prevents duplicate conversions

### 🎯 Professional Quality
- **Production Ready**: Thoroughly tested with Canon EOS R100, RP cameras
- **Error Handling**: Comprehensive logging for troubleshooting
- **Clean UI**: Integrated into NINA Options with clear settings
- **Flexible Configuration**: Per-profile settings support

## What's Included

### Plugin Files
- `CanonEDSDKPlugin.cs` - Main plugin with event handling and task management
- `Services/CFitsioWriter.cs` - FITS file writer with compression support
- `Services/RawToFitsConverter.cs` - In-memory conversion service
- `Native/CFitsioNative.cs` - P/Invoke wrapper for cfitsio.dll
- `Options.xaml` - Configuration UI

### Supporting Files
- `README.md` - Complete user guide
- `CODE_REVIEW_SUMMARY.md` - Technical documentation
- `build.bat` - Automated build script
- `install.ps1` - Installation script

### Libraries
- CFitsio 4.6.3 complete source (Windows pre-built)
- All necessary dependencies included in package

## Installation

### Quick Start
1. Download `NINA.Plugin.Canon.EDSDK.zip` from releases
2. Run `install.ps1` script (or manually extract to NINA plugins directory)
3. Restart NINA
4. Go to Options → Plugins → Canon EDSDK to configure

### Requirements
- NINA 3.0 or later
- .NET 8.0 (included with NINA 3.0+)
- Canon camera connected to NINA
- 200+ MB free disk space

### Configuration
**Available Settings:**
- Enable/Disable Auto-Conversion
- FITS Output Directory
- Delete Original RAW Files option
- FITS Engine (CSharpFITS or CFitsio)
- Compression Algorithm (RICE, GZIP, HCOMPRESS, None)

## Technical Improvements in v1.0.0

### Burst Sequence Fix
- ✅ Fixed last image in sequences not converting
- ✅ Added pending task tracking with ConcurrentBag
- ✅ Teardown waits up to 2 minutes for all conversions
- ✅ Tested with 30-image burst sequences

### FITS Compatibility Fix
- ✅ Added informational COMMENT keywords to primary HDU
- ✅ Includes dimension information (e.g., "6024 x 4020 pixels")
- ✅ Explains compressed file structure
- ✅ Helps users understand FITS 4.0 format

### Compression Enhancements
- ✅ HDU navigation support (`fits_movabs_hdu`)
- ✅ Proper tile dimension handling
- ✅ HCOMPRESS parameter optimization
- ✅ Compression ratio reporting in logs

## Performance Benchmarks

### File Size Reduction (30-image test)
- Original RAW images: 46.19 MB each (1,380 MB total)
- RICE Compressed FITS: 20.96 MB each (629 MB total)
- **Compression: 54.6% reduction** ✅
- GZIP Level 1: ~45% reduction
- Uncompressed: Original size

### Conversion Speed
- Average: 2-3 images per second
- Semaphore limit: 3 concurrent (prevents resource exhaustion)
- Full burst sequence (30 images): ~12-15 seconds

### Memory Usage
- Per-image peak: ~300 MB (6024×4020 pixels)
- Semaphore throttling prevents heap overflow
- Safe for systems with 4+ GB RAM

## FITS Output Quality

### Verified Features
- ✅ 2D monochrome Bayer pattern data (not debayered)
- ✅ Proper NAXIS keywords (2, NAXIS1=6024, NAXIS2=4020)
- ✅ BAYERPAT = RGGB (or camera-specific pattern)
- ✅ BZERO = 32768, BSCALE = 1 for unsigned interpretation
- ✅ Full metadata preservation
- ✅ Compression support verified with astropy/FITS tools

### Tested With
- Canon EOS R100
- Canon EOS RP
- NINA 3.2.0.3012 and later
- Python/Astropy FITS validation

## Compatibility

### Supported Cameras
- Any Canon EOS camera with RAW support
- Tested: EOS R100, EOS RP
- CR2, CR3, CRW file formats

### Compatible Software
- **Excellent**: PixInsight, SIRIL, SAO DS9, Astropy
- **Good**: Most FITS 4.0 compliant software
- **Limited**: Older software that doesn't support compressed extensions

### Fallback Options
- If your software doesn't support compressed FITS:
  1. Use "No Compression" mode for maximum compatibility
  2. Switch to CSharpFITS engine (uncompressed)
  3. Update to FITS 4.0 compliant software

## Known Limitations

1. **Software Compatibility**: Older software may not read compressed FITS
   - Solution: Use uncompressed mode or upgrade software

2. **File Size on Disk**: Large packages (84 MB) may take time to download
   - Solution: Git LFS configured for future versions

3. **Processing Time**: 30-image sequences take 12-15 seconds
   - Note: This is async, doesn't block NINA UI

## Troubleshooting

### Last Image Not Converting
- **Fixed in v1.0.0**: Pending task tracking ensures all images process
- Check NINA logs for "Waiting for X pending conversions"

### "Cannot Read Dimensions" Error
- **Fixed in v1.0.0**: Added compatibility comments to primary HDU
- Software may need update to support FITS 4.0
- Try uncompressed mode if problem persists

### File Size Larger Than Expected
- This is normal - verify compression is enabled in plugin settings
- RICE compression achieves 50-60% reduction consistently

## Support & Documentation

- **README.md**: User guide and configuration
- **CODE_REVIEW_SUMMARY.md**: Technical deep-dive
- **COMPRESSED_FITS_FIX.md**: Compressed FITS explanation
- **NINA.Plugin.Canon.EDSDK.zip**: Complete distribution package

## License

GNU General Public License v3.0 - See LICENSE file for details

## Credits

- **NASA CFitsio Library**: https://heasarc.gsfc.nasa.gov/fitsio/
- **CSharpFITS Library**: For fallback FITS writing
- **NINA Project**: https://nighttime-imaging.eu/
- **Canon EDSDK**: Canon's SDK for camera control

## Version History

### v1.0.0 (October 22, 2025) - Initial Release
- ✨ Full CFitsio compression support
- ✨ Burst sequence handling with pending task tracking
- ✨ FITS 4.0 primary HDU compatibility enhancements
- ✨ Complete documentation and code review
- ✨ Production-ready distribution package

---

**Ready to Use:** Download, install, and enjoy automated Canon RAW to FITS conversion! 🚀
