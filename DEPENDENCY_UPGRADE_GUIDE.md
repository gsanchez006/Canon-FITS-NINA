# Dependency Upgrade Guide

This guide explains how to upgrade CFitsio and Canon EDSDK when new versions are released.

## Project Structure

All dependencies are organized in the `dependencies/` folder with version numbers:

```
dependencies/
├── cfitsio-4.6.3/
│   └── x64/
│       ├── cfitsio.dll (Windows-native, built with MSVC)
│       └── zlib.dll
├── edsdk-13.19.0/
│   └── x64/
│       ├── EDSDK.dll
│       ├── EdsImage.dll
│       ├── DPP4Lib/ (Canon RAW processing library)
│       └── IHL/ (Canon Image Handling Library)
└── CSharpFITS_v1.1.dll
```

## Upgrading CFitsio

### 1. Build New CFitsio Version (Windows-Native with MSVC)

**Prerequisites:**
- Visual Studio 2022 Build Tools or Visual Studio 2022
- CMake 3.15 or later
- NuGet CLI (for zlib dependency)

**Steps:**

1. **Download CFitsio source** from https://heasarc.gsfc.nasa.gov/fitsio/fitsio.html
   ```powershell
   # Extract to cfitsio-X.Y.Z/ folder (e.g., cfitsio-4.7.0/)
   ```

2. **Install zlib via NuGet** (Windows-native MSVC build):
   ```powershell
   .\nuget.exe install zlib-msvc-x64 -OutputDirectory packages
   ```

3. **Configure CFitsio with CMake** (use MSVC, not GCC):
   ```powershell
   # Create build directory
   mkdir cfitsio-X.Y.Z\build-native
   cd cfitsio-X.Y.Z\build-native
   
   # Configure with Visual Studio generator
   cmake .. -G "Visual Studio 17 2022" -A x64 `
     -DBUILD_SHARED_LIBS=ON `
     -DUSE_PTHREADS=OFF `
     -DCFITSIO_USE_CURL=OFF `
     -DCFITSIO_USE_BZIP2=OFF `
     -DZLIB_INCLUDE_DIR="..\..\packages\zlib-msvc-x64.1.2.11.8900\build\native\include" `
     -DZLIB_LIBRARY="..\..\packages\zlib-msvc-x64.1.2.11.8900\build\native\lib_release\zlib.lib"
   ```

4. **Build CFitsio**:
   ```powershell
   cmake --build . --config Release
   ```

5. **Copy binaries to dependencies folder**:
   ```powershell
   # Create new version folder
   mkdir dependencies\cfitsio-X.Y.Z\x64
   
   # Copy CFitsio DLL
   copy cfitsio-X.Y.Z\build-native\Release\cfitsio.dll dependencies\cfitsio-X.Y.Z\x64\
   
   # Copy zlib DLL (from NuGet package)
   copy packages\zlib-msvc-x64.1.2.11.8900\build\native\bin_release\zlib.dll dependencies\cfitsio-X.Y.Z\x64\
   ```

6. **Update project file** (`NINA.Plugin.Canon.EDSDK.csproj`):
   ```xml
   <!-- Change version number in Include path -->
   <None Include="dependencies\cfitsio-X.Y.Z\x64\*.dll" .../>
   ```

7. **Test build**:
   ```powershell
   .\build.ps1
   ```

## Upgrading Canon EDSDK

### 1. Download New EDSDK

1. Download latest EDSDK from Canon Developer Portal
2. Extract to temporary folder (e.g., `EDSDK_vX.Y.Z_Raw_Win`)

### 2. Copy Required Files

```powershell
# Create new EDSDK version folder
mkdir dependencies\edsdk-X.Y.Z\x64

# Copy core DLLs
copy EDSDK_vX.Y.Z_Raw_Win\EDSDK_64\Dll\EDSDK.dll dependencies\edsdk-X.Y.Z\x64\
copy EDSDK_vX.Y.Z_Raw_Win\EDSDK_64\Dll\EdsImage.dll dependencies\edsdk-X.Y.Z\x64\

# Copy DPP4Lib folder (RAW processing)
xcopy EDSDK_vX.Y.Z_Raw_Win\EDSDK_64\Dll\DPP4Lib dependencies\edsdk-X.Y.Z\x64\DPP4Lib\ /E /I

# Copy IHL folder (Image Handling Library)
xcopy EDSDK_vX.Y.Z_Raw_Win\EDSDK_64\Dll\IHL dependencies\edsdk-X.Y.Z\x64\IHL\ /E /I
```

### 3. Update Project File

Edit `NINA.Plugin.Canon.EDSDK.csproj` and change version numbers:

```xml
<!-- Update EDSDK core DLLs -->
<None Include="dependencies\edsdk-X.Y.Z\x64\*.dll" Link="%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" />

<!-- Update DPP4Lib path -->
<None Include="dependencies\edsdk-X.Y.Z\x64\DPP4Lib\**\*.*">
  <Link>DPP4Lib\%(RecursiveDir)%(Filename)%(Extension)</Link>
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>

<!-- Update IHL path -->
<None Include="dependencies\edsdk-X.Y.Z\x64\IHL\**\*.*">
  <Link>IHL\%(RecursiveDir)%(Filename)%(Extension)</Link>
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

### 4. Test Build

```powershell
.\build.ps1
```

## Verification Checklist

After upgrading any dependency:

- [ ] Build completes without errors (`.\build.ps1`)
- [ ] All dependencies verified in output:
  - [ ] cfitsio.dll
  - [ ] zlib.dll
  - [ ] EDSDK.dll
  - [ ] EdsImage.dll
  - [ ] CSharpFITS_v1.1.dll
  - [ ] DPP4Lib folder with all subfolders
  - [ ] IHL folder with all DLLs
- [ ] Package ZIP created successfully
- [ ] Extract ZIP and verify all files present
- [ ] Install plugin to NINA test environment
- [ ] Test with actual Canon camera
- [ ] Verify FITS files are created correctly
- [ ] Check FITS file opens in astronomy software (PixInsight, MaxIm DL, DS9, etc.)

## Important Notes

### Windows-Native Only
- **All dependencies must be Windows-native** (built with MSVC, not GCC)
- Do not use MSYS2/MinGW versions of libraries
- CFitsio must be built with Visual Studio, not GCC
- This ensures no dependency on GCC runtime DLLs (libgcc, libstdc++, libwinpthread)

### Version Numbers in Paths
- Always include version numbers in folder names
- Example: `cfitsio-4.6.3`, `edsdk-13.19.0`
- Makes it easy to track which versions are being used
- Simplifies testing multiple versions

### Canon DPP4Lib and IHL Are Required
- DPP4Lib contains Canon's RAW processing libraries (includes CR2, CR3, CRW support)
- IHL contains Canon's Image Handling Library for metadata
- Both folders must be copied with complete subfolder structures
- Missing these will cause plugin failures with Canon cameras

### Testing
- Always test with actual Canon camera hardware
- Verify both CSharpFITS and CFitsio engines work
- Test with different compression modes (RICE, GZIP, HCOMPRESS, None)
- Check FITS headers contain all expected metadata

## Troubleshooting

### Build Fails After Upgrade
1. Clean build directory: `dotnet clean`
2. Check all paths in .csproj are correct
3. Verify DLL files exist in dependencies folder
4. Look for typos in version numbers

### Missing Dependencies in Package
1. Run build again
2. Check build.ps1 output for verification warnings
3. Extract package ZIP and manually verify contents

### Plugin Won't Load in NINA
1. Check all DLLs are present in plugin directory
2. Verify DPP4Lib and IHL folders copied correctly
3. Check NINA log for error messages
4. Ensure no missing Windows dependencies (use Dependency Walker)

### CFitsio Build Errors
- Ensure using Visual Studio generator, not MinGW/MSYS2
- Verify zlib paths point to correct NuGet package
- Check CMake output for configuration errors
- Make sure Visual Studio 2022 Build Tools are installed

## Support

For issues or questions:
- Check NINA log: `%LOCALAPPDATA%\NINA\Logs\`
- Review build output carefully
- Verify all dependency versions match project file
- Test with minimal setup (no plugins except this one)
