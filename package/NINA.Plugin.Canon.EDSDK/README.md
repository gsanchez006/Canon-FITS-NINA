# Canon RAW to FITS Converter - NINA Plugin

**Version 1.0.0** | A professional FITS converter plugin for NINA (Nighttime Imaging 'N' Astronomy)

![NINA Canon FITS Converter](nina_canon_fits.png)

## Overview

This plugin automatically converts Canon RAW files (CR2, CR3, CRW) to FITS format using NINA's in-memory image data. It preserves full 14/16-bit dynamic range without demosaicing or processing, ideal for scientific astrophotography workflows.

## Features

### Dual FITS Engines
- **CSharpFITS**: Pure C# implementation, cross-platform compatible, used by NINA
- **NASA CFitsio**: Native library with advanced compression support (RICE, GZIP, HCOMPRESS)

### Compression Support (CFitsio Engine)
- **RICE** (Recommended): 30-50% file size reduction, lossless, fast
- **GZIP Level 1**: Fast compression, moderate reduction
- **GZIP Level 2**: Better compression, higher CPU usage
- **HCOMPRESS**: Lossless mode with 16x16 square tiles
- **None**: Uncompressed FITS files

### Full NINA Metadata Preservation
Writes 70+ FITS headers including:
- Camera settings (gain, offset, temperature, binning)
- Telescope data (RA, DEC, focal length, airmass)
- Filter wheel information
- Focuser position and temperature
- Rotator angle
- Weather data (temperature, humidity, wind, sky quality)
- Observer location (latitude, longitude, elevation)
- Target coordinates and position angle
- Modified Julian Date (MJD-OBS, MJD-AVG)

## Installation

### Method 1: Automatic Installation (Recommended)
1. Download `NINA.Plugin.Canon.EDSDK.zip` from the latest release
2. Extract to `%LOCALAPPDATA%\NINA\Plugins\3.0.0\NINA.Plugin.Canon.EDSDK\`
3. Restart NINA

### Method 2: Using Install Script
1. Run `install.ps1` (PowerShell script included in package)
2. Restart NINA

### Method 3: Manual Installation
1. Extract ZIP file contents
2. Copy all files to: `C:\Users\<YourName>\AppData\Local\NINA\Plugins\3.0.0\NINA.Plugin.Canon.EDSDK\`
3. Ensure the following DLLs are present:
   - `NINA.Plugin.Canon.EDSDK.dll`
   - `CSharpFITS_v1.1.dll`
   - `cfitsio.dll`
   - `EDSDK.dll`
   - `EdsImage.dll`
   - `DPP4Lib\` folder (Canon image processing libraries)
   - `IHL\` folder (Canon image handling libraries)
4. Restart NINA

## Configuration

Access plugin settings in NINA: **Options → Plugins → Canon RAW to FITS Converter**

### Settings

#### Enable Auto-Conversion
Toggle automatic conversion of Canon RAW files to FITS during image capture.

#### FITS Engine
Choose between:
- **CSharpFITS (NINA Standard)**: No compression, guaranteed compatibility
- **NASA CFitsio**: High performance with compression options

#### Compression Method (CFitsio only)
Select compression algorithm when using CFitsio engine:
- **RICE (Recommended)**: Best balance of speed and compression
- **GZIP Level 1/2**: Good compression, slower
- **HCOMPRESS**: Lossless with 16x16 tiles
- **None**: Uncompressed (largest files)

#### Delete Original RAW Files
Automatically delete Canon RAW files after successful FITS conversion (use with caution).

## Technical Details

### Supported Canon Formats
- **CR2**: Canon RAW version 2 (EOS cameras up to ~2018)
- **CR3**: Canon RAW version 3 (EOS R, RP, and newer mirrorless)
- **CRW**: Original Canon RAW format (older cameras)

### FITS File Format
- **BITPIX**: 16 (16-bit signed integer with BZERO=32768 for unsigned interpretation)
- **NAXIS**: 2 (2D image array)
- **BZERO**: 32768 (offset for unsigned 16-bit data: 0-65535 range)
- **BSCALE**: 1 (no scaling applied)

### Compression Performance (Canon EOS R100, 6024×4020 images)
| Method | File Size | Reduction | Speed |
|--------|-----------|-----------|-------|
| RICE | 29.90 MB | 35.3% | Fast |
| GZIP Level 2 | 30.36 MB | 34.3% | Medium |
| HCOMPRESS | 32.62 MB | 29.4% | Medium |
| None | 46.19 MB | 0% | Fastest |

### Data Preservation
- **No demosaicing**: Raw Bayer pattern preserved
- **No tone curves**: Linear data as captured
- **Full bit depth**: 14-bit or 16-bit depending on camera
- **No scaling**: Pixel values as recorded by sensor

## Requirements

### System Requirements
- **Operating System**: Windows 10/11 (64-bit)
- **NINA Version**: 3.0 or later
- **.NET**: 8.0 (included with NINA 3.0+)
- **Canon Camera**: Any Canon EOS camera supported by NINA

### Dependencies (Included)
- **Canon EDSDK 13.19.0**: Canon camera SDK libraries
- **CSharpFITS v1.1**: Pure C# FITS library
- **CFitsio 4.6.3**: NASA FITS library (custom build with GCC 15.2.0)

## Usage Workflow

1. Connect Canon camera to NINA
2. Enable auto-conversion in plugin settings
3. Select FITS engine (CSharpFITS or CFitsio)
4. Choose compression method (if using CFitsio)
5. Take images normally in NINA
6. Plugin automatically:
   - Detects Canon camera
   - Converts from NINA's in-memory data
   - Saves FITS file with same filename (.fits extension)
   - Preserves all NINA metadata in FITS headers
   - Optionally deletes original CR3 file

## Troubleshooting

### Plugin Not Loading
- Check NINA log: `%LOCALAPPDATA%\NINA\Logs\`
- Verify all DLLs present in plugin directory
- Ensure no file permission issues
- Try reinstalling plugin

### FITS Files Not Created
- Enable auto-conversion in plugin settings
- Check camera is detected as Canon brand
- Verify NINA is saving files to disk (output directory configured)
- Review NINA log for error messages

### CSharpFITS Engine Fails
- Plugin will automatically fall back to CSharpFITS if CFitsio fails
- Check NINA log for specific error messages
- Verify `CSharpFITS_v1.1.dll` exists in plugin directory

### CFitsio Engine Fails
- Plugin will fall back to CSharpFITS automatically
- Check that `cfitsio.dll` exists in plugin directory
- Verify Visual C++ Redistributables installed

### Compression Not Working
- Compression only available with CFitsio engine
- Select "NASA CFitsio" engine in settings
- Choose compression method other than "None"

## Development & Building

### Build Requirements
- **Visual Studio 2022** or **VS Code** with C# extension
- **.NET 8.0 SDK**
- **Canon EDSDK 13.19.0** (place in `../EDSDK_v13.19.0_Raw_Win/`)

### Building from Source
```batch
cd NINA.Plugin.Canon.EDSDK.CFitsio
build.bat
```

This will:
1. Clean previous builds
2. Restore NuGet packages
3. Build Release configuration
4. Copy Canon EDSDK DLLs
5. Create distribution ZIP package

### Installing Development Build
```powershell
.\install.ps1
```

## License

This project includes:
- **Plugin Code**: MIT License
- **Canon EDSDK**: Canon Inc. proprietary license (redistribution allowed for EDSDK applications)
- **CSharpFITS**: BSD-style license
- **CFitsio**: NASA public domain

## Credits

- **Plugin Development**: Gus Sanchez
- **NINA**: Nighttime Imaging 'N' Astronomy team
- **Canon EDSDK**: Canon Inc.
- **CSharpFITS**: Thomas McGlynn (NASA/GSFC)
- **CFitsio**: NASA/HEASARC

## Support

For issues, questions, or feature requests:
- GitHub Issues: https://github.com/gsanchez006/Canon-FITS-NINA
- NINA Forums: https://nighttime-imaging.eu/

## Changelog

### Version 1.0.0 (2025-10-21)
- Initial release
- Dual FITS engine support (CSharpFITS and CFitsio)
- Full NINA metadata preservation (70+ headers)
- Compression support: RICE, GZIP (L1/L2), HCOMPRESS
- Automatic Canon camera detection
- In-memory conversion (no disk I/O for RAW processing)
- Support for CR2, CR3, CRW formats
- Burst mode support with duplicate detection
- Retry logic for file locking scenarios
- Comprehensive error handling with fallback
