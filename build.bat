@echo off
setlocal enabledelayedexpansion

echo ============================================
echo Building NINA.Plugin.Canon.EDSDK
echo ============================================

echo.
echo [1/5] Cleaning previous builds...
dotnet clean --configuration Release
if errorlevel 1 (
    echo ERROR: Clean failed
    pause
    exit /b 1
)

echo.
echo [2/5] Restoring NuGet packages...
dotnet restore
if errorlevel 1 (
    echo ERROR: Restore failed
    pause
    exit /b 1
)

echo.
echo [3/5] Building Release configuration...
dotnet build --configuration Release --no-restore
if errorlevel 1 (
    echo ERROR: Build failed
    pause
    exit /b 1
)

echo.
echo [4/5] Copying Canon EDSDK DLLs...
set OUTPUT_DIR=bin\Release\net8.0-windows
set EDSDK_DIR=..\EDSDK_v13.19.0_Raw_Win\EDSDK_64\Dll

if not exist "!OUTPUT_DIR!" (
    echo ERROR: Output directory not found
    pause
    exit /b 1
)

if not exist "!EDSDK_DIR!" (
    echo ERROR: EDSDK directory not found
    pause
    exit /b 1
)

copy /Y "!EDSDK_DIR!\EDSDK.dll" "!OUTPUT_DIR!\" >nul
echo   - Copied EDSDK.dll

copy /Y "!EDSDK_DIR!\EdsImage.dll" "!OUTPUT_DIR!\" >nul
echo   - Copied EdsImage.dll

REM Copy cfitsio.dll if it exists
if exist "cfitsio-4.6.3\cfitsio.dll" (
    copy /Y "cfitsio-4.6.3\cfitsio.dll" "!OUTPUT_DIR!\" >nul
    echo   - Copied cfitsio.dll
)

REM Copy CSharpFITS library if it exists
if exist "lib\CSharpFITS_v1.1.dll" (
    copy /Y "lib\CSharpFITS_v1.1.dll" "!OUTPUT_DIR!\" >nul
    echo   - Copied CSharpFITS_v1.1.dll
)

if exist "!EDSDK_DIR!\DPP4Lib" (
    xcopy /E /I /Y "!EDSDK_DIR!\DPP4Lib" "!OUTPUT_DIR!\DPP4Lib\" >nul
    echo   - Copied DPP4Lib folder
)

if exist "!EDSDK_DIR!\IHL" (
    xcopy /E /I /Y "!EDSDK_DIR!\IHL" "!OUTPUT_DIR!\IHL\" >nul
    echo   - Copied IHL folder
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

copy /Y "!OUTPUT_DIR!\NINA.Plugin.Canon.EDSDK.dll" "!PACKAGE_DIR!\!PACKAGE_NAME!\" >nul
copy /Y "!OUTPUT_DIR!\EDSDK.dll" "!PACKAGE_DIR!\!PACKAGE_NAME!\" >nul
copy /Y "!OUTPUT_DIR!\EdsImage.dll" "!PACKAGE_DIR!\!PACKAGE_NAME!\" >nul

REM Copy cfitsio.dll to package if it exists
if exist "!OUTPUT_DIR!\cfitsio.dll" (
    copy /Y "!OUTPUT_DIR!\cfitsio.dll" "!PACKAGE_DIR!\!PACKAGE_NAME!\" >nul
)

REM Copy CSharpFITS library to package if it exists
if exist "!OUTPUT_DIR!\CSharpFITS_v1.1.dll" (
    copy /Y "!OUTPUT_DIR!\CSharpFITS_v1.1.dll" "!PACKAGE_DIR!\!PACKAGE_NAME!\" >nul
)

if exist "!OUTPUT_DIR!\DPP4Lib" (
    xcopy /E /I /Y "!OUTPUT_DIR!\DPP4Lib" "!PACKAGE_DIR!\!PACKAGE_NAME!\DPP4Lib\" >nul
)
if exist "!OUTPUT_DIR!\IHL" (
    xcopy /E /I /Y "!OUTPUT_DIR!\IHL" "!PACKAGE_DIR!\!PACKAGE_NAME!\IHL\" >nul
)

copy /Y "README.md" "!PACKAGE_DIR!\!PACKAGE_NAME!\" >nul 2>nul
copy /Y "LICENSE.txt" "!PACKAGE_DIR!\!PACKAGE_NAME!\" >nul 2>nul

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
