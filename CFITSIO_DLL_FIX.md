# CFitsio DLL Loading Issue - Production vs Dev

**Issue:** cfitsio.dll fails to load in production NINA but works in development  
**Error:** `Unable to load DLL 'cfitsio.dll' or one of its dependencies: The specified module could not be found. (0x8007007E)`

---

## Problem Analysis

### From Log Comparison

**DEV Environment (`nina_dev.log`):**
```
✅ cfitsio loads successfully
✅ WriteFitsFile works fine
✅ Compression functional
```

**PROD Environment (`nina_prod.log`):**
```
❌ ERROR: Unable to load DLL 'cfitsio.dll' or one of its dependencies
❌ Falls back to CSharpFITS
❌ Compression never works
```

### Root Cause

`cfitsio.dll` **depends on Microsoft Visual C++ Runtime DLLs** that are missing on the production system. When .NET tries to load `cfitsio.dll`, Windows can't find the required runtime dependencies.

**Windows Error Code 0x8007007E** = `ERROR_MOD_NOT_FOUND` = "The specified module could not be found"

This typically means:
1. The DLL itself is missing (NOT our case - it's in the package)
2. **A dependency DLL is missing** (THIS is our case)

---

## Why It Works in Dev

On the development machine:
- ✅ Visual Studio installed (includes all C++ runtimes)
- ✅ Development tools include debug runtimes
- ✅ CMake build environment has runtime DLLs in PATH

On the production machine:
- ❌ No Visual Studio
- ❌ May not have correct Visual C++ Redistributable
- ❌ NINA's plugin folder isolated from system DLLs

---

## Solutions

### Solution 1: Include VC++ Runtime DLLs (Recommended)

Bundle the required Visual C++ runtime DLLs directly with the plugin.

**Files needed:**
- `vcruntime140.dll` - Visual C++ 2015-2022 Runtime
- `msvcp140.dll` - C++ Standard Library

**Where to get them:**
- From `C:\Windows\System32\` on the build machine
- Or from Visual C++ Redistributable installation

**Implementation:**
1. Copy VC++ runtime DLLs to plugin output folder
2. Package them in the ZIP
3. They'll be deployed alongside cfitsio.dll

**Advantages:**
- ✅ Guaranteed to work
- ✅ No user action required
- ✅ Self-contained plugin
- ✅ No version conflicts

**Disadvantages:**
- Slightly larger package size (~400 KB additional)
- Need to ensure license compliance (Microsoft allows redistribution)

### Solution 2: Static Linking (Best Long-Term)

Rebuild `cfitsio.dll` with static linking to VC++ runtime.

**CMake build flags:**
```cmake
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded")  # Static release runtime
```

**Advantages:**
- ✅ No runtime dependencies
- ✅ Smaller total deployment
- ✅ More reliable

**Disadvantages:**
- Requires rebuilding cfitsio.dll
- Larger DLL file itself

### Solution 3: Document Runtime Requirement

Add runtime check and clear error message.

**Advantages:**
- ✅ Simple to implement
- ✅ Good user experience

**Disadvantages:**
- ❌ Requires user to install redistributable
- ❌ Extra step for users

---

## Recommended Implementation

### Immediate Fix (Solution 1)

Add VC++ runtime DLLs to the build script:

**Update `build.ps1`:**

```powershell
# After copying cfitsio.dll, copy VC++ runtime dependencies
Write-Host ""
Write-Host "[4.5/5] Copying Visual C++ Runtime dependencies..." -ForegroundColor Cyan

$vcRuntimeFiles = @(
    "vcruntime140.dll",
    "msvcp140.dll"
)

foreach ($file in $vcRuntimeFiles) {
    $systemPath = "C:\Windows\System32\$file"
    if (Test-Path $systemPath) {
        Copy-Item $systemPath $OutputDir -Force
        Write-Host "  - Copied $file" -ForegroundColor Green
    } else {
        Write-Host "  - WARNING: $file not found in System32" -ForegroundColor Yellow
    }
}
```

**Update package verification:**

```powershell
$dependencies = @{
    # ... existing dependencies ...
    "vcruntime140.dll" = "$OutputDir\vcruntime140.dll"
    "msvcp140.dll" = "$OutputDir\msvcp140.dll"
}
```

### Long-Term Fix (Solution 2)

Rebuild cfitsio with static runtime:

**CMakeLists.txt modification:**
```cmake
# Force static runtime linkage
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
```

Then rebuild:
```powershell
cd cfitsio-4.6.3
mkdir build-static
cd build-static
cmake .. -DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded
cmake --build . --config Release
```

---

## Testing the Fix

### 1. Dev Environment Test
```powershell
# Temporarily rename runtime DLLs to simulate missing dependencies
cd C:\Windows\System32
ren vcruntime140.dll vcruntime140.dll.bak
# Try running NINA with plugin
# Should fail with same error as production
```

### 2. Production Environment Test
After deploying fixed package:
1. Enable cfitsio engine in plugin settings
2. Take a test exposure
3. Check logs - should see successful cfitsio compression
4. Verify FITS file is compressed (smaller than uncompressed)

### 3. Verification Commands
```powershell
# Check if cfitsio.dll loaded
Get-Process -Name "NINA" | Select-Object -ExpandProperty Modules | Where-Object {$_.ModuleName -like "*cfitsio*"}

# Check dependencies with dumpbin (if available)
dumpbin /dependents cfitsio.dll
```

---

## Implementation Steps

1. **Update build.ps1** to include VC++ runtime DLLs
2. **Rebuild plugin package** with runtime dependencies
3. **Test on production system** (or clean VM without VC++ installed)
4. **Update v1.0.2 to v1.0.3** with this fix
5. **Document in README** that VC++ runtimes are included

---

## Files to Modify

### `build.ps1`
Add runtime DLL copying after cfitsio.dll copy

### `NINA.Plugin.Canon.EDSDK.csproj` (Optional)
Can add explicit None/Copy items for runtime DLLs if needed

### `README.md`
Update to mention included VC++ runtimes

---

## License Compliance

**Microsoft Visual C++ Runtime Redistribution:**
- ✅ Allowed per Microsoft's redistribution terms
- ✅ No separate license file required
- ✅ Can be bundled with applications
- ✅ No attribution required in most cases

**Reference:** Microsoft Visual C++ Redistributable License Terms allow redistribution with applications.

---

## Summary

**Problem:** Production NINA can't load cfitsio.dll due to missing VC++ runtime dependencies  
**Solution:** Bundle vcruntime140.dll and msvcp140.dll with plugin  
**Effort:** 10 minutes to update build script  
**Result:** Plugin works on all systems without requiring manual runtime installation  

**Version:** Should be released as **v1.0.3** (bug fix release)
