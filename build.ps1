# Build script for NINA.Plugin.Canon.EDSDK
# PowerShell version

$ErrorActionPreference = "Stop"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Building NINA.Plugin.Canon.EDSDK" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

$ProjectFile = "NINA.Plugin.Canon.EDSDK.csproj"
$OutputDir = "bin\Release\net8.0-windows"
$PackageDir = "package"
$PackageName = "NINA.Plugin.Canon.EDSDK"

try {
    # Step 1: Clean
    Write-Host "[1/5] Cleaning previous builds..." -ForegroundColor Yellow
    dotnet clean $ProjectFile --configuration Release
    if ($LASTEXITCODE -ne 0) {
        throw "Clean failed with exit code $LASTEXITCODE"
    }
    Write-Host ""

    # Step 2: Restore
    Write-Host "[2/5] Restoring NuGet packages..." -ForegroundColor Yellow
    dotnet restore $ProjectFile
    if ($LASTEXITCODE -ne 0) {
        throw "Restore failed with exit code $LASTEXITCODE"
    }
    Write-Host ""

    # Step 3: Build
    Write-Host "[3/5] Building Release configuration..." -ForegroundColor Yellow
    dotnet build $ProjectFile --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
    Write-Host ""

    # Step 4: Verify Dependencies
    Write-Host "[4/5] Verifying dependencies..." -ForegroundColor Yellow
    
    if (-not (Test-Path $OutputDir)) {
        throw "Output directory not found: $OutputDir"
    }

    # All dependencies are automatically copied by .csproj from dependencies/ folder:
    # - dependencies/cfitsio-4.6.3/x64/*.dll (cfitsio.dll, zlib.dll)
    # - dependencies/edsdk-13.19.0/x64/*.dll (EDSDK.dll, EdsImage.dll)
    # - dependencies/edsdk-13.19.0/x64/DPP4Lib/** (Canon RAW processing)
    # - dependencies/edsdk-13.19.0/x64/IHL/** (Canon Image Handling Library)
    # - dependencies/CSharpFITS_v1.1.dll (Alternative FITS writer)
    #
    # All dependencies are Windows-native (built with MSVC, no GCC/MSYS2 dependencies)
    
    Write-Host "  All dependencies are Windows-native (MSVC builds)" -ForegroundColor Green

    # Verify all required dependencies are present in output
    Write-Host ""
    Write-Host "Verifying dependencies in output:" -ForegroundColor Yellow
    
    $allPresent = $true
    
    $dependencies = @{
        "CSharpFITS_v1.1.dll" = "$OutputDir\CSharpFITS_v1.1.dll"
        "EDSDK.dll" = "$OutputDir\EDSDK.dll"
        "EdsImage.dll" = "$OutputDir\EdsImage.dll"
        "cfitsio.dll" = "$OutputDir\cfitsio.dll"
        "zlib.dll" = "$OutputDir\zlib.dll"
        "DPP4Lib folder" = "$OutputDir\DPP4Lib"
        "IHL folder" = "$OutputDir\IHL"
    }

    foreach ($dep in $dependencies.GetEnumerator()) {
        if (Test-Path $dep.Value) {
            Write-Host "  - $($dep.Key): " -NoNewline
            Write-Host "OK" -ForegroundColor Green
        }
        else {
            Write-Host "  - $($dep.Key): " -NoNewline
            Write-Host "MISSING" -ForegroundColor Red
            $allPresent = $false
        }
    }

    if (-not $allPresent) {
        Write-Host ""
        Write-Host "ERROR: Some dependencies are missing!" -ForegroundColor Red
        Write-Host "Check that dependencies/ folder structure is correct" -ForegroundColor Red
        throw "Missing dependencies in build output"
    }
    
    Write-Host ""

    # Step 5: Create Package
    Write-Host "[5/5] Creating distribution package..." -ForegroundColor Yellow
    
    if (Test-Path $PackageDir) {
        Remove-Item $PackageDir -Recurse -Force
    }
    
    New-Item -ItemType Directory -Path $PackageDir | Out-Null
    New-Item -ItemType Directory -Path "$PackageDir\$PackageName" | Out-Null

    # Copy main plugin DLL
    Copy-Item "$OutputDir\NINA.Plugin.Canon.EDSDK.dll" "$PackageDir\$PackageName\" -Force

    # Copy all dependencies from build output (Windows-native builds)
    $filesToCopy = @(
        "cfitsio.dll",
        "zlib.dll",
        "CSharpFITS_v1.1.dll",
        "EDSDK.dll",
        "EdsImage.dll"
    )

    foreach ($file in $filesToCopy) {
        $sourcePath = "$OutputDir\$file"
        if (Test-Path $sourcePath) {
            Copy-Item $sourcePath "$PackageDir\$PackageName\" -Force
        }
    }

    # Copy Canon EDSDK folders (DPP4Lib and IHL)
    if (Test-Path "$OutputDir\DPP4Lib") {
        Copy-Item "$OutputDir\DPP4Lib" "$PackageDir\$PackageName\DPP4Lib" -Recurse -Force
        Write-Host "  - Packaged DPP4Lib folder" -ForegroundColor Green
    }

    if (Test-Path "$OutputDir\IHL") {
        Copy-Item "$OutputDir\IHL" "$PackageDir\$PackageName\IHL" -Recurse -Force
        Write-Host "  - Packaged IHL folder" -ForegroundColor Green
    }

    # Copy documentation
    if (Test-Path "README.md") {
        Copy-Item "README.md" "$PackageDir\$PackageName\" -Force
    }

    if (Test-Path "LICENSE") {
        Copy-Item "LICENSE" "$PackageDir\$PackageName\" -Force
    }

    # Create ZIP archive
    Write-Host "  - Creating ZIP archive..." -ForegroundColor Green
    $zipPath = "$PackageDir\$PackageName.zip"
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
    Compress-Archive -Path "$PackageDir\$PackageName\*" -DestinationPath $zipPath -Force

    Write-Host ""
    Write-Host "============================================" -ForegroundColor Green
    Write-Host "BUILD SUCCESSFUL!" -ForegroundColor Green
    Write-Host "============================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Plugin DLL: " -NoNewline
    Write-Host "$OutputDir\NINA.Plugin.Canon.EDSDK.dll" -ForegroundColor Cyan
    Write-Host "Package: " -NoNewline
    Write-Host "$PackageDir\$PackageName.zip" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "To install:" -ForegroundColor Yellow
    Write-Host "1. Run: " -NoNewline
    Write-Host ".\install.ps1" -ForegroundColor Cyan
    Write-Host "   OR" -ForegroundColor Yellow
    Write-Host "2. Extract $PackageName.zip" -ForegroundColor Yellow
    Write-Host "3. Copy contents to: " -NoNewline
    Write-Host "`$env:LOCALAPPDATA\NINA\Plugins\3.0.0\NINA.Plugin.Canon.EDSDK" -ForegroundColor Cyan
    Write-Host "4. Restart NINA" -ForegroundColor Yellow
    Write-Host ""
}
catch {
    Write-Host ""
    Write-Host "============================================" -ForegroundColor Red
    Write-Host "BUILD FAILED!" -ForegroundColor Red
    Write-Host "============================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    exit 1
}
