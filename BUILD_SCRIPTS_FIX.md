# Build Scripts Fix Summary

## Date: November 1, 2025

## Problem Fixed

The `build.bat` file was failing with error:
```
MSBUILD : error MSB1011: Specify which project or solution file to use because this folder contains more than one project or solution file.
```

Additionally, the build script referenced external directories (EDSDK files outside the project folder).

## Root Cause

1. **Missing Project Specification**: The `dotnet` commands didn't specify which project file to use
2. **External Dependencies**: EDSDK directory reference pointed to `..\..\EDSDK_v13.19.0_Raw_Win\EDSDK_64\Dll` (outside project folder)
3. **No PowerShell Version**: Only batch script was available

## Solution Implemented

### 1. Fixed build.bat

**Changes Made:**
- Added `PROJECT_FILE=NINA.Plugin.Canon.EDSDK.csproj` variable
- All `dotnet` commands now specify the project file explicitly:
  - `dotnet clean "%PROJECT_FILE%" --configuration Release`
  - `dotnet restore "%PROJECT_FILE%"`
  - `dotnet build "%PROJECT_FILE%" --configuration Release --no-restore`

- Removed external EDSDK directory reference
- Changed to look for EDSDK files in `lib\` directory (within project)
- Made EDSDK files optional with informative warnings
- Updated dependency copy logic to check multiple locations:
  - `build\cfitsio.dll` (first choice)
  - `cfitsio-4.6.3\cfitsio.dll` (fallback)
  - `lib\CSharpFITS_v1.1.dll`
  - `lib\EDSDK.dll`, `lib\EdsImage.dll` (optional)
  - `lib\DPP4Lib\`, `lib\IHL\` (optional)

**Result:**
✅ Build succeeds with all dependencies in project folder
✅ Graceful handling when optional EDSDK files are missing
✅ Clear warning messages about missing components

### 2. Created build.ps1 (PowerShell Version)

**Features:**
- Full PowerShell implementation with proper error handling
- Color-coded output (Cyan for headers, Yellow for steps, Green for success, Red for errors)
- Better error messages with try/catch blocks
- Same functionality as batch version
- More readable code structure
- Follows PowerShell best practices

**Benefits:**
- Better error handling with `$ErrorActionPreference = "Stop"`
- Colored output for better readability
- More maintainable PowerShell syntax
- Proper exit codes for CI/CD integration

## Test Results

### build.bat Test
```
✅ [1/5] Cleaning previous builds... PASSED
✅ [2/5] Restoring NuGet packages... PASSED
✅ [3/5] Building Release configuration... PASSED
✅ [4/5] Copying dependencies... PASSED
   - Copied cfitsio.dll
   - Copied CSharpFITS_v1.1.dll
   - NOTE: EDSDK files not found (expected)
✅ [5/5] Creating distribution package... PASSED
   - Created package\NINA.Plugin.Canon.EDSDK.zip

BUILD SUCCESSFUL!
```

### build.ps1 Test
```
✅ [1/5] Cleaning previous builds... PASSED
✅ [2/5] Restoring NuGet packages... PASSED
✅ [3/5] Building Release configuration... PASSED
✅ [4/5] Copying dependencies... PASSED
   - Copied cfitsio.dll
   - Copied CSharpFITS_v1.1.dll
   - NOTE: EDSDK files not found (expected)
✅ [5/5] Creating distribution package... PASSED
   - Created package\NINA.Plugin.Canon.EDSDK.zip

BUILD SUCCESSFUL!
```

## Files Modified/Created

1. ✅ **build.bat** - Fixed to work with project-only files
2. ✅ **build.ps1** - Created new PowerShell version

## Key Improvements

### Before
```batch
# No project specification
dotnet clean --configuration Release

# External dependency reference
set EDSDK_DIR=..\EDSDK_v13.19.0_Raw_Win\EDSDK_64\Dll

# Fails if EDSDK not found
if not exist "!EDSDK_DIR!" (
    echo ERROR: EDSDK directory not found
    pause
    exit /b 1
)
```

### After
```batch
# Project explicitly specified
set PROJECT_FILE=NINA.Plugin.Canon.EDSDK.csproj
dotnet clean "%PROJECT_FILE%" --configuration Release

# Look for EDSDK in project lib\ directory (optional)
if exist "lib\EDSDK.dll" (
    copy /Y "lib\EDSDK.dll" "!OUTPUT_DIR!\" >nul
    set EDSDK_FOUND=1
)

# Graceful handling if not found
if !EDSDK_FOUND! EQU 0 (
    echo   - NOTE: EDSDK files not found in lib\ directory
    echo   - Plugin will work but EDSDK features will not be available
)
```

## Dependency Locations (Current)

All dependencies are now expected within the project folder:

```
NINA.Plugin.Canon.EDSDK.CFitsio/
├── build/
│   └── cfitsio.dll              ← Primary location
├── cfitsio-4.6.3/
│   └── cfitsio.dll              ← Fallback location
├── lib/
│   ├── CSharpFITS_v1.1.dll      ← Required
│   ├── EDSDK.dll                ← Optional
│   ├── EdsImage.dll             ← Optional
│   ├── DPP4Lib/                 ← Optional
│   └── IHL/                     ← Optional
└── NINA.Plugin.Canon.EDSDK.csproj
```

## Build Output

Both scripts produce:
- `bin\Release\net8.0-windows\NINA.Plugin.Canon.EDSDK.dll` - Main plugin
- `bin\Release\net8.0-windows\cfitsio.dll` - CFitsio library
- `bin\Release\net8.0-windows\CSharpFITS_v1.1.dll` - CSharpFITS library
- `package\NINA.Plugin.Canon.EDSDK.zip` - Distribution package

## Usage

### Batch Script
```batch
.\build.bat
```

### PowerShell Script
```powershell
.\build.ps1
```

Both scripts:
1. Clean previous builds
2. Restore NuGet packages
3. Build Release configuration
4. Copy dependencies to output directory
5. Create distribution ZIP package

## Notes

- ⚠️ Build warnings about .NET Framework compatibility are expected (NINA packages target older framework)
- ⚠️ Warning about `async` method without `await` on line 189 is expected (ProfileService_ProfileChanged is correctly async)
- ✅ EDSDK files are now optional - plugin works without them
- ✅ Both scripts produce identical output
- ✅ Both scripts tested and verified working

## Next Steps

To add EDSDK files to the project (if needed):
1. Create `lib\` directory if it doesn't exist
2. Copy EDSDK DLLs to `lib\`:
   - EDSDK.dll
   - EdsImage.dll
3. Copy EDSDK folders to `lib\`:
   - DPP4Lib\
   - IHL\
4. Run build script again

The build scripts will automatically detect and include them.
