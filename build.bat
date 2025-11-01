@echo off
setlocal enabledelayedexpansion

echo ============================================
echo Building NINA.Plugin.Canon.EDSDK
echo ============================================

set PROJECT_FILE=NINA.Plugin.Canon.EDSDK.csproj

echo.
echo [1/5] Cleaning previous builds...
dotnet clean "%PROJECT_FILE%" --configuration Release
if errorlevel 1 (
    echo ERROR: Clean failed
    pause
    exit /b 1
)

echo.
echo [2/5] Restoring NuGet packages...
dotnet restore "%PROJECT_FILE%"
if errorlevel 1 (
    echo ERROR: Restore failed
    pause
    exit /b 1
)

echo.
echo [3/5] Building Release configuration...
dotnet build "%PROJECT_FILE%" --configuration Release --no-restore
if errorlevel 1 (
    echo ERROR: Build failed
    pause
    exit /b 1
)

echo.
echo [4/5] Copying dependencies...
set OUTPUT_DIR=bin\Release\net8.0-windows

if not exist "!OUTPUT_DIR!" (
    echo ERROR: Output directory not found: !OUTPUT_DIR!
    pause
    exit /b 1
)

REM The .csproj file automatically copies:
REM - lib\*.dll to output directory
REM - EDSDK\**\*.dll to output directory
REM
REM This includes:
REM - lib\CSharpFITS_v1.1.dll
REM - lib\EDSDK.dll  
REM - lib\EdsImage.dll
REM - EDSDK\DPP4Lib\**\*.dll
REM - EDSDK\IHL\**\*.dll
REM
REM CFitsio is built separately and copied here:

REM Copy cfitsio.dll if it exists in build directory
if exist "build\cfitsio.dll" (
    copy /Y "build\cfitsio.dll" "!OUTPUT_DIR!\" >nul
    echo   - Copied cfitsio.dll from build\
) else if exist "cfitsio-4.6.3\cfitsio.dll" (
    copy /Y "cfitsio-4.6.3\cfitsio.dll" "!OUTPUT_DIR!\" >nul
    echo   - Copied cfitsio.dll from cfitsio-4.6.3\
) else (
    echo   - WARNING: cfitsio.dll not found
)

REM Verify required files are present
echo.
echo Verifying dependencies:
if exist "!OUTPUT_DIR!\CSharpFITS_v1.1.dll" (
    echo   - CSharpFITS_v1.1.dll: OK
) else (
    echo   - CSharpFITS_v1.1.dll: MISSING
)

if exist "!OUTPUT_DIR!\EDSDK.dll" (
    echo   - EDSDK.dll: OK
) else (
    echo   - EDSDK.dll: MISSING
)

if exist "!OUTPUT_DIR!\EdsImage.dll" (
    echo   - EdsImage.dll: OK
) else (
    echo   - EdsImage.dll: MISSING
)

if exist "!OUTPUT_DIR!\cfitsio.dll" (
    echo   - cfitsio.dll: OK
) else (
    echo   - cfitsio.dll: MISSING
)

if exist "!OUTPUT_DIR!\EDSDK\DPP4Lib" (
    echo   - EDSDK\DPP4Lib: OK
) else (
    echo   - EDSDK\DPP4Lib: MISSING
)

if exist "!OUTPUT_DIR!\EDSDK\IHL" (
    echo   - EDSDK\IHL: OK
) else (
    echo   - EDSDK\IHL: MISSING
)

echo.
echo [5/5] Creating distribution package...
set PACKAGE_DIR=package
set PACKAGE_NAME=NINA.Plugin.Canon.EDSDK

if exist "!PACKAGE_DIR!" (
    rmdir /S /Q "!PACKAGE_DIR!"
)
mkdir "!PACKAGE_DIR!"
mkdir "!PACKAGE_DIR!\!PACKAGE_NAME!"

REM Copy main plugin DLL
copy /Y "!OUTPUT_DIR!\NINA.Plugin.Canon.EDSDK.dll" "!PACKAGE_DIR!\!PACKAGE_NAME!\" >nul

REM Copy all dependencies from build output
copy /Y "!OUTPUT_DIR!\cfitsio.dll" "!PACKAGE_DIR!\!PACKAGE_NAME!\" >nul 2>nul
copy /Y "!OUTPUT_DIR!\CSharpFITS_v1.1.dll" "!PACKAGE_DIR!\!PACKAGE_NAME!\" >nul 2>nul
copy /Y "!OUTPUT_DIR!\EDSDK.dll" "!PACKAGE_DIR!\!PACKAGE_NAME!\" >nul 2>nul
copy /Y "!OUTPUT_DIR!\EdsImage.dll" "!PACKAGE_DIR!\!PACKAGE_NAME!\" >nul 2>nul

REM Copy EDSDK folders if they exist
if exist "!OUTPUT_DIR!\EDSDK\DPP4Lib" (
    xcopy /E /I /Y "!OUTPUT_DIR!\EDSDK\DPP4Lib" "!PACKAGE_DIR!\!PACKAGE_NAME!\EDSDK\DPP4Lib\" >nul
)

if exist "!OUTPUT_DIR!\EDSDK\IHL" (
    xcopy /E /I /Y "!OUTPUT_DIR!\EDSDK\IHL" "!PACKAGE_DIR!\!PACKAGE_NAME!\EDSDK\IHL\" >nul
)

REM Also copy standalone folders if they exist (backward compatibility)
if exist "!OUTPUT_DIR!\DPP4Lib" (
    xcopy /E /I /Y "!OUTPUT_DIR!\DPP4Lib" "!PACKAGE_DIR!\!PACKAGE_NAME!\DPP4Lib\" >nul
)

if exist "!OUTPUT_DIR!\IHL" (
    xcopy /E /I /Y "!OUTPUT_DIR!\IHL" "!PACKAGE_DIR!\!PACKAGE_NAME!\IHL\" >nul
)

REM Copy documentation
copy /Y "README.md" "!PACKAGE_DIR!\!PACKAGE_NAME!\" >nul 2>nul
copy /Y "LICENSE" "!PACKAGE_DIR!\!PACKAGE_NAME!\" >nul 2>nul

echo   - Creating ZIP archive...
powershell -Command "Compress-Archive -Path '!PACKAGE_DIR!\!PACKAGE_NAME!\*' -DestinationPath '!PACKAGE_DIR!\!PACKAGE_NAME!.zip' -Force"

echo.
echo ============================================
echo BUILD SUCCESSFUL!
echo ============================================
echo.
echo Plugin DLL: !OUTPUT_DIR!\NINA.Plugin.Canon.EDSDK.dll
echo Package: !PACKAGE_DIR!\!PACKAGE_NAME!.zip
echo.
echo To install:
echo 1. Run: install.ps1
echo    OR
echo 2. Extract !PACKAGE_NAME!.zip
echo 3. Copy contents to: %%LOCALAPPDATA%%\NINA\Plugins\3.0.0\NINA.Plugin.Canon.EDSDK
echo 4. Restart NINA
echo.
pause
