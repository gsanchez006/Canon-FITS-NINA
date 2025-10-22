# NINA Canon EDSDK Plugin - Quick Install Script
# This script installs the plugin directly to NINA's plugins folder

$ErrorActionPreference = "Stop"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "NINA Canon EDSDK Plugin - Installer" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Source and destination paths
$sourcePath = Join-Path $PSScriptRoot "package\NINA.Plugin.Canon.EDSDK"
$ninaPluginsPath = "$env:LOCALAPPDATA\NINA\Plugins\3.0.0"
$pluginInstallPath = "$ninaPluginsPath\NINA.Plugin.Canon.EDSDK"

# Check if source exists
if (-not (Test-Path $sourcePath)) {
    Write-Host "ERROR: Source package not found!" -ForegroundColor Red
    Write-Host "Expected: $sourcePath" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Run build.bat first to create the package." -ForegroundColor Yellow
    pause
    exit 1
}

# Check if NINA is running
$ninaProcess = Get-Process -Name "NINA" -ErrorAction SilentlyContinue
if ($ninaProcess) {
    Write-Host "WARNING: NINA is currently running!" -ForegroundColor Yellow
    Write-Host "Please close NINA before continuing." -ForegroundColor Yellow
    Write-Host ""
    $response = Read-Host "Continue anyway? (y/n)"
    if ($response -ne 'y') {
        Write-Host "Installation cancelled." -ForegroundColor Yellow
        pause
        exit 0
    }
}

# Create plugins directory if it doesn't exist
Write-Host "[1/3] Creating plugins directory..." -ForegroundColor Green
New-Item -ItemType Directory -Force -Path $ninaPluginsPath | Out-Null

# Remove old installation
if (Test-Path $pluginInstallPath) {
    Write-Host "[2/3] Removing old installation..." -ForegroundColor Green
    Remove-Item -Path $pluginInstallPath -Recurse -Force
}

# Copy new installation
Write-Host "[3/3] Installing plugin..." -ForegroundColor Green
Copy-Item -Path $sourcePath -Destination $ninaPluginsPath -Recurse -Force

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "INSTALLATION COMPLETE!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Installed to: $pluginInstallPath" -ForegroundColor White
Write-Host ""

# Verify installation
$dllExists = Test-Path "$pluginInstallPath\NINA.Plugin.Canon.EDSDK.dll"
$edsdkExists = Test-Path "$pluginInstallPath\EDSDK.dll"
$dpp4Exists = Test-Path "$pluginInstallPath\DPP4Lib"
$ihlExists = Test-Path "$pluginInstallPath\IHL"

Write-Host "Installation Verification:" -ForegroundColor Cyan
Write-Host "  Plugin DLL:    $(if($dllExists){'✓'}else{'✗'})" -ForegroundColor $(if($dllExists){'Green'}else{'Red'})
Write-Host "  EDSDK DLL:     $(if($edsdkExists){'✓'}else{'✗'})" -ForegroundColor $(if($edsdkExists){'Green'}else{'Red'})
Write-Host "  DPP4Lib:       $(if($dpp4Exists){'✓'}else{'✗'})" -ForegroundColor $(if($dpp4Exists){'Green'}else{'Red'})
Write-Host "  IHL:           $(if($ihlExists){'✓'}else{'✗'})" -ForegroundColor $(if($ihlExists){'Green'}else{'Red'})
Write-Host ""

if ($dllExists -and $edsdkExists -and $dpp4Exists -and $ihlExists) {
    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host "  1. Start NINA" -ForegroundColor White
    Write-Host "  2. Go to: Options → Plugins" -ForegroundColor White
    Write-Host "  3. Look for: Canon EDSDK to FITS Converter" -ForegroundColor White
    Write-Host "  4. Click on it to see settings" -ForegroundColor White
    Write-Host ""
    Write-Host "You should see 4 settings:" -ForegroundColor Cyan
    Write-Host "  • Enable Auto-Conversion" -ForegroundColor White
    Write-Host "  • FITS Output Directory" -ForegroundColor White
    Write-Host "  • Delete Original RAW Files" -ForegroundColor White
    Write-Host "  • Preserve All Metadata" -ForegroundColor White
} else {
    Write-Host "WARNING: Some files are missing!" -ForegroundColor Red
    Write-Host "The plugin may not work correctly." -ForegroundColor Yellow
}

Write-Host ""
pause
