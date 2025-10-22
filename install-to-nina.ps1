# PowerShell script to install Canon RAW processing DLLs to NINA's Canon folder
# Run this as Administrator

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Installing Canon EDSDK RAW Processing DLLs" -ForegroundColor Cyan
Write-Host "to NINA's Canon folder" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Check if running as administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "ERROR: This script must be run as Administrator!" -ForegroundColor Red
    Write-Host "Right-click PowerShell and select 'Run as Administrator', then run this script again." -ForegroundColor Yellow
    Write-Host ""
    pause
    exit 1
}

$ninaCanonDir = "C:\Program Files\N.I.N.A. - Nighttime Imaging 'N' Astronomy\External\x64\Canon"
$pluginDir = "$env:LOCALAPPDATA\NINA\Plugins\3.0.0\NINA.Plugin.Canon.EDSDK"
$sourceDir = "bin\Release\net8.0-windows"

Write-Host "Checking paths..." -ForegroundColor Yellow

if (-not (Test-Path $ninaCanonDir)) {
    Write-Host "ERROR: NINA Canon directory not found!" -ForegroundColor Red
    Write-Host "Expected: $ninaCanonDir" -ForegroundColor Gray
    Write-Host ""
    pause
    exit 1
}

if (-not (Test-Path $sourceDir)) {
    Write-Host "ERROR: Build output directory not found!" -ForegroundColor Red
    Write-Host "Please run build.bat first" -ForegroundColor Yellow
    Write-Host ""
    pause
    exit 1
}

Write-Host "   NINA Canon folder: $ninaCanonDir" -ForegroundColor Gray
Write-Host "   Plugin folder: $pluginDir" -ForegroundColor Gray
Write-Host ""

Write-Host "[1/3] Installing RAW processing DLLs to NINA's Canon folder..." -ForegroundColor Cyan
Write-Host ""

# Copy EdsImage.dll to NINA's Canon folder
try {
    Copy-Item "$sourceDir\EdsImage.dll" "$ninaCanonDir\" -Force -ErrorAction Stop
    Write-Host "   [OK] Copied EdsImage.dll" -ForegroundColor Green
} catch {
    Write-Host "   [ERROR] Failed to copy EdsImage.dll: $($_.Exception.Message)" -ForegroundColor Red
    pause
    exit 1
}

# Copy DPP4Lib folder to NINA's Canon folder
if (Test-Path "$sourceDir\DPP4Lib") {
    try {
        if (Test-Path "$ninaCanonDir\DPP4Lib") {
            Remove-Item "$ninaCanonDir\DPP4Lib" -Recurse -Force
        }
        Copy-Item "$sourceDir\DPP4Lib" "$ninaCanonDir\DPP4Lib" -Recurse -Force -ErrorAction Stop
        $dppCount = (Get-ChildItem "$ninaCanonDir\DPP4Lib" -File).Count
        Write-Host "   [OK] Copied DPP4Lib folder ($dppCount DLLs)" -ForegroundColor Green
    } catch {
        Write-Host "   [ERROR] Failed to copy DPP4Lib folder: $($_.Exception.Message)" -ForegroundColor Red
        pause
        exit 1
    }
}

# Copy IHL folder to NINA's Canon folder
if (Test-Path "$sourceDir\IHL") {
    try {
        if (Test-Path "$ninaCanonDir\IHL") {
            Remove-Item "$ninaCanonDir\IHL" -Recurse -Force
        }
        Copy-Item "$sourceDir\IHL" "$ninaCanonDir\IHL" -Recurse -Force -ErrorAction Stop
        $ihlCount = (Get-ChildItem "$ninaCanonDir\IHL" -File).Count
        Write-Host "   [OK] Copied IHL folder ($ihlCount DLLs)" -ForegroundColor Green
    } catch {
        Write-Host "   [ERROR] Failed to copy IHL folder: $($_.Exception.Message)" -ForegroundColor Red
        pause
        exit 1
    }
}

Write-Host ""
Write-Host "[2/3] Installing plugin DLL to NINA plugins folder..." -ForegroundColor Cyan
Write-Host ""

# Create plugin directory if it doesn't exist
if (-not (Test-Path $pluginDir)) {
    New-Item -Path $pluginDir -ItemType Directory -Force | Out-Null
}

# Copy plugin DLL
try {
    Copy-Item "$sourceDir\NINA.Plugin.Canon.EDSDK.dll" "$pluginDir\" -Force -ErrorAction Stop
    Write-Host "   [OK] Copied NINA.Plugin.Canon.EDSDK.dll" -ForegroundColor Green
} catch {
    Write-Host "   [ERROR] Failed to copy plugin DLL: $($_.Exception.Message)" -ForegroundColor Red
    pause
    exit 1
}

Write-Host ""
Write-Host "[3/3] Verifying installation..." -ForegroundColor Cyan
Write-Host ""

$allGood = $true

if (Test-Path "$ninaCanonDir\EDSDK.dll") {
    $size = [math]::Round((Get-Item "$ninaCanonDir\EDSDK.dll").Length / 1KB, 0)
    Write-Host "   [OK] EDSDK.dll ($size KB) - from NINA" -ForegroundColor Green
} else {
    Write-Host "   [MISSING] EDSDK.dll" -ForegroundColor Red
    $allGood = $false
}

if (Test-Path "$ninaCanonDir\EdsImage.dll") {
    $size = [math]::Round((Get-Item "$ninaCanonDir\EdsImage.dll").Length / 1KB, 0)
    Write-Host "   [OK] EdsImage.dll ($size KB) - RAW processing" -ForegroundColor Green
} else {
    Write-Host "   [MISSING] EdsImage.dll" -ForegroundColor Red
    $allGood = $false
}

if (Test-Path "$ninaCanonDir\DPP4Lib") {
    $dppCount = (Get-ChildItem "$ninaCanonDir\DPP4Lib" -File).Count
    Write-Host "   [OK] DPP4Lib folder ($dppCount DLLs)" -ForegroundColor Green
} else {
    Write-Host "   [MISSING] DPP4Lib folder" -ForegroundColor Red
    $allGood = $false
}

if (Test-Path "$ninaCanonDir\IHL") {
    $ihlCount = (Get-ChildItem "$ninaCanonDir\IHL" -File).Count
    Write-Host "   [OK] IHL folder ($ihlCount DLLs)" -ForegroundColor Green
} else {
    Write-Host "   [MISSING] IHL folder" -ForegroundColor Red
    $allGood = $false
}

if (Test-Path "$pluginDir\NINA.Plugin.Canon.EDSDK.dll") {
    Write-Host "   [OK] Plugin DLL" -ForegroundColor Green
} else {
    Write-Host "   [MISSING] Plugin DLL" -ForegroundColor Red
    $allGood = $false
}

Write-Host ""

if ($allGood) {
    Write-Host "============================================" -ForegroundColor Green
    Write-Host "INSTALLATION SUCCESSFUL!" -ForegroundColor Green
    Write-Host "============================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Canon EDSDK RAW processing libraries installed to:" -ForegroundColor White
    Write-Host "  $ninaCanonDir" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Plugin installed to:" -ForegroundColor White
    Write-Host "  $pluginDir" -ForegroundColor Gray
    Write-Host ""
    Write-Host "IMPORTANT:" -ForegroundColor Yellow
    Write-Host "1. Restart NINA for changes to take effect" -ForegroundColor White
    Write-Host "2. The plugin will use NINA's EDSDK.dll (already initialized)" -ForegroundColor White
    Write-Host "3. RAW processing DLLs are now available for file conversion" -ForegroundColor White
    Write-Host "4. NO Canon software installation required!" -ForegroundColor Cyan
} else {
    Write-Host "============================================" -ForegroundColor Red
    Write-Host "INSTALLATION FAILED" -ForegroundColor Red
    Write-Host "============================================" -ForegroundColor Red
    Write-Host "Some files are missing. Please check the errors above." -ForegroundColor Yellow
}

Write-Host ""
pause
