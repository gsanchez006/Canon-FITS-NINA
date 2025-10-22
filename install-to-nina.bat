@echo off
setlocal enabledelayedexpansion

echo ============================================
echo Installing Canon EDSDK RAW Processing DLLs
echo to NINA's Canon folder
echo ============================================
echo.

REM Check if running as administrator
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script must be run as Administrator!
    echo Right-click and select "Run as administrator"
    pause
    exit /b 1
)

set NINA_CANON_DIR=C:\Program Files\N.I.N.A. - Nighttime Imaging 'N' Astronomy\External\x64\Canon
set PLUGIN_DIR=%LOCALAPPDATA%\NINA\Plugins\3.0.0\NINA.Plugin.Canon.EDSDK
set SOURCE_DIR=bin\Release\net8.0-windows

echo Checking paths...
if not exist "!NINA_CANON_DIR!" (
    echo ERROR: NINA Canon directory not found!
    echo Expected: !NINA_CANON_DIR!
    pause
    exit /b 1
)

if not exist "!SOURCE_DIR!" (
    echo ERROR: Build output directory not found!
    echo Please run build.bat first
    pause
    exit /b 1
)

echo   - NINA Canon folder: !NINA_CANON_DIR!
echo   - Plugin folder: !PLUGIN_DIR!
echo.

echo [1/3] Installing RAW processing DLLs to NINA's Canon folder...

REM Copy EdsImage.dll to NINA's Canon folder
copy /Y "!SOURCE_DIR!\EdsImage.dll" "!NINA_CANON_DIR!\" >nul
if errorlevel 1 (
    echo ERROR: Failed to copy EdsImage.dll
    pause
    exit /b 1
)
echo   - Copied EdsImage.dll

REM Copy DPP4Lib folder to NINA's Canon folder
if exist "!SOURCE_DIR!\DPP4Lib" (
    xcopy /E /I /Y "!SOURCE_DIR!\DPP4Lib" "!NINA_CANON_DIR!\DPP4Lib\" >nul
    if errorlevel 1 (
        echo ERROR: Failed to copy DPP4Lib folder
        pause
        exit /b 1
    )
    echo   - Copied DPP4Lib folder (25 DLLs)
)

REM Copy IHL folder to NINA's Canon folder
if exist "!SOURCE_DIR!\IHL" (
    xcopy /E /I /Y "!SOURCE_DIR!\IHL" "!NINA_CANON_DIR!\IHL\" >nul
    if errorlevel 1 (
        echo ERROR: Failed to copy IHL folder
        pause
        exit /b 1
    )
    echo   - Copied IHL folder (13 DLLs)
)

echo.
echo [2/3] Installing plugin DLL to NINA plugins folder...

REM Create plugin directory if it doesn't exist
if not exist "!PLUGIN_DIR!" (
    mkdir "!PLUGIN_DIR!"
)

REM Copy plugin DLL
copy /Y "!SOURCE_DIR!\NINA.Plugin.Canon.EDSDK.dll" "!PLUGIN_DIR!\" >nul
if errorlevel 1 (
    echo ERROR: Failed to copy plugin DLL
    pause
    exit /b 1
)
echo   - Copied NINA.Plugin.Canon.EDSDK.dll

echo.
echo [3/3] Verifying installation...

if exist "!NINA_CANON_DIR!\EDSDK.dll" (
    echo   [OK] EDSDK.dll (from NINA)
) else (
    echo   [MISSING] EDSDK.dll
)

if exist "!NINA_CANON_DIR!\EdsImage.dll" (
    echo   [OK] EdsImage.dll (RAW processing)
) else (
    echo   [MISSING] EdsImage.dll
)

if exist "!NINA_CANON_DIR!\DPP4Lib" (
    echo   [OK] DPP4Lib folder (25 DLLs)
) else (
    echo   [MISSING] DPP4Lib folder
)

if exist "!NINA_CANON_DIR!\IHL" (
    echo   [OK] IHL folder (13 DLLs)
) else (
    echo   [MISSING] IHL folder
)

if exist "!PLUGIN_DIR!\NINA.Plugin.Canon.EDSDK.dll" (
    echo   [OK] Plugin DLL
) else (
    echo   [MISSING] Plugin DLL
)

echo.
echo ============================================
echo INSTALLATION SUCCESSFUL!
echo ============================================
echo.
echo Canon EDSDK RAW processing libraries have been installed to:
echo   !NINA_CANON_DIR!
echo.
echo Plugin has been installed to:
echo   !PLUGIN_DIR!
echo.
echo IMPORTANT: 
echo 1. Restart NINA for changes to take effect
echo 2. The plugin will use NINA's EDSDK.dll (already initialized)
echo 3. RAW processing DLLs are now available for file conversion
echo.
pause
