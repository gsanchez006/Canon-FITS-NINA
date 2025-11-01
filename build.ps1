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

    # Step 4: Copy Dependencies
    Write-Host "[4/5] Copying dependencies..." -ForegroundColor Yellow
    
    if (-not (Test-Path $OutputDir)) {
        throw "Output directory not found: $OutputDir"
    }

    # Note: The .csproj file automatically copies:
    # - lib\*.dll to output directory
    # - EDSDK\**\*.dll to output directory
    #
    # This includes:
    # - lib\CSharpFITS_v1.1.dll
    # - lib\EDSDK.dll
    # - lib\EdsImage.dll
    # - EDSDK\DPP4Lib\**\*.dll
    # - EDSDK\IHL\**\*.dll
    #
    # CFitsio is built separately and copied here:

    # Copy cfitsio.dll
    $cfitsioFound = $false
    if (Test-Path "build\cfitsio.dll") {
        Copy-Item "build\cfitsio.dll" $OutputDir -Force
        Write-Host "  - Copied cfitsio.dll from build\" -ForegroundColor Green
        $cfitsioFound = $true
    }
    elseif (Test-Path "cfitsio-4.6.3\cfitsio.dll") {
        Copy-Item "cfitsio-4.6.3\cfitsio.dll" $OutputDir -Force
        Write-Host "  - Copied cfitsio.dll from cfitsio-4.6.3\" -ForegroundColor Green
        $cfitsioFound = $true
    }
    
    if (-not $cfitsioFound) {
        Write-Host "  - WARNING: cfitsio.dll not found" -ForegroundColor Yellow
    }

    # Verify all required dependencies are present
    Write-Host ""
    Write-Host "Verifying dependencies:" -ForegroundColor Yellow
    
    $allPresent = $true
    
    $dependencies = @{
        "CSharpFITS_v1.1.dll" = "$OutputDir\CSharpFITS_v1.1.dll"
        "EDSDK.dll" = "$OutputDir\EDSDK.dll"
        "EdsImage.dll" = "$OutputDir\EdsImage.dll"
        "cfitsio.dll" = "$OutputDir\cfitsio.dll"
        "EDSDK\DPP4Lib" = "$OutputDir\EDSDK\DPP4Lib"
        "EDSDK\IHL" = "$OutputDir\EDSDK\IHL"
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
        Write-Host "WARNING: Some dependencies are missing!" -ForegroundColor Yellow
        Write-Host "Make sure all required files are in lib\ and EDSDK\ folders" -ForegroundColor Yellow
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

    # Copy all dependencies from build output
    $filesToCopy = @(
        "cfitsio.dll",
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

    # Copy EDSDK folders from build output
    if (Test-Path "$OutputDir\EDSDK\DPP4Lib") {
        Copy-Item "$OutputDir\EDSDK\DPP4Lib" "$PackageDir\$PackageName\EDSDK\DPP4Lib" -Recurse -Force
    }

    if (Test-Path "$OutputDir\EDSDK\IHL") {
        Copy-Item "$OutputDir\EDSDK\IHL" "$PackageDir\$PackageName\EDSDK\IHL" -Recurse -Force
    }

    # Also copy standalone folders if they exist (backward compatibility)
    if (Test-Path "$OutputDir\DPP4Lib") {
        Copy-Item "$OutputDir\DPP4Lib" "$PackageDir\$PackageName\DPP4Lib" -Recurse -Force
    }

    if (Test-Path "$OutputDir\IHL") {
        Copy-Item "$OutputDir\IHL" "$PackageDir\$PackageName\IHL" -Recurse -Force
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
