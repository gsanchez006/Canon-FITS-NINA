# FITS to CR3 Comparison Tool - Usage Guide

## Overview

This tool compares FITS files against their source Canon CR3 (RAW) files to verify that the FITS conversion is accurate and pixel-perfect. It's essential for validating that FITS engines (CSharpFITS, CFitsio) are producing scientifically correct output.

## Requirements

- **Python 3.13** (rawpy requires 3.13 or earlier, not available for 3.14+)
- **Virtual Environment**: `.venv` in project root
- **Required Packages**: astropy, numpy, rawpy

## Setup (One-Time)

If the virtual environment is not already set up:

```powershell
# Navigate to project root
cd "c:\Users\Gus\Documents\VS Code Projects\NINA Canon"

# Create Python 3.13 virtual environment
py -3.13 -m venv .venv

# Activate the virtual environment
.\.venv\Scripts\Activate.ps1

# Install required packages
pip install astropy numpy rawpy
```

## Running the Comparison Tool

### Method 1: Compare Default Test Images

The script is pre-configured to compare:
- `csharp.fits` ↔ `csharp.cr3` (CSharpFITS implementation)
- `cfitsio.fits` ↔ `cfitsio.cr3` (CFitsio implementation)

```powershell
# Navigate to images folder
cd "c:\Users\Gus\Documents\VS Code Projects\NINA Canon\images"

# Run the comparison
& "c:/Users/Gus/Documents/VS Code Projects/NINA Canon/.venv/Scripts/python.exe" compare_fits_to_cr3.py
```

### Method 2: Compare Custom Files

To compare different FITS/CR3 pairs, edit `compare_fits_to_cr3.py`:

1. Open `compare_fits_to_cr3.py`
2. Scroll to the `main()` function at the bottom
3. Modify the file paths:

```python
def main():
    print("="*80)
    print("FITS to Canon CR3 Comparison Tool")
    print("="*80)
    print("\nThis tool compares FITS files against their source Canon CR3 files")
    print("to verify conversion accuracy.\n")
    
    # ===== CUSTOMIZE THESE PATHS =====
    fits_pairs = [
        ("your_image.fits", "your_image.cr3", "Your Description"),
        ("another.fits", "another.cr3", "Another Test"),
    ]
    # =================================
    
    results = []
    for i, (fits_file, cr3_file, description) in enumerate(fits_pairs, 1):
        # ... rest of the code
```

### Method 3: Quick Single Comparison (Command Line)

Compare a single FITS/CR3 pair without modifying the script:

```powershell
cd "c:\Users\Gus\Documents\VS Code Projects\NINA Canon\images"

& "c:/Users/Gus/Documents/VS Code Projects/NINA Canon/.venv/Scripts/python.exe" -c @"
from compare_fits_to_cr3 import analyze_fits_file, extract_cr3_raw_data
import numpy as np

fits_file = 'your_image.fits'
cr3_file = 'your_image.cr3'

print(f'Comparing {fits_file} ↔ {cr3_file}')
print('='*60)

fits_data, fits_header = analyze_fits_file(fits_file)
cr3_data = extract_cr3_raw_data(cr3_file)

print(f'\nFITS Stats: Min={np.min(fits_data)}, Max={np.max(fits_data)}, Mean={np.mean(fits_data):.2f}')
print(f'CR3 Stats:  Min={np.min(cr3_data)}, Max={np.max(cr3_data)}, Mean={np.mean(cr3_data):.2f}')

offset = np.mean(fits_data) - np.mean(cr3_data)
print(f'\nMean Offset: {offset:.2f}')

if abs(offset) < 1.0:
    print('✅ PERFECT MATCH!')
elif abs(offset - 32768) < 100:
    print('❌ BZERO BUG DETECTED (+32768 offset)')
else:
    print(f'⚠️  Unexpected offset: {offset:.2f}')
"@
```

## Understanding the Output

### Pixel Statistics

The tool shows pixel value ranges for both FITS and CR3:

```
FITS Pixel Statistics:
  Min:               1706
  Max:              12806
  Mean:           2045.66

CR3 Pixel Statistics:
  Min:               1706
  Max:              12806
  Mean:           2045.81
```

### What to Look For

#### ✅ **CORRECT** (Bug Fixed)
- **Min/Max values match** between FITS and CR3
- **Mean offset < 1.0** (minor rounding differences are normal)
- Example: FITS mean 2045.66 vs CR3 mean 2045.81 (only 0.15 difference)

#### ❌ **BZERO BUG** (Unfixed)
- **Mean offset ≈ 32768**
- Example: FITS mean 34813.08 vs CR3 mean 2045.26 (32768 offset)
- FITS values are 32768+ too high

#### ⚠️ **Dimension Mismatch**
- FITS: 6024 x 4020
- CR3: 6288 x 4056
- This is **NORMAL** - FITS excludes optical black borders
- Pixel-by-pixel comparison skipped, but statistics still valid

## Test Cases

### Test 1: Dark Flat (3s exposure)
**Purpose**: Low signal test, good for detecting offset bugs
**Expected**: Mean around 2000-2100 (dark current + bias)

### Test 2: Normal Exposure
**Purpose**: Full dynamic range test
**Expected**: Values spread across 0-16383 range

### Test 3: Bright Exposure
**Purpose**: Test high-value handling (near saturation)
**Expected**: Max values approaching 16383

## Troubleshooting

### Error: "No module named 'rawpy'"
```powershell
# Reinstall rawpy
& "c:/Users/Gus/Documents/VS Code Projects/NINA Canon/.venv/Scripts/python.exe" -m pip install rawpy
```

### Error: "No matching distribution found for rawpy"
- **Cause**: Python 3.14 doesn't support rawpy
- **Fix**: Use Python 3.13 or earlier
```powershell
py -0  # List available Python versions
py -3.13 -m venv .venv  # Recreate with 3.13
```

### Error: "FileNotFoundError: [Errno 2] No such file"
- Ensure you're in the `images` folder
- Check that `.fits` and `.cr3` files have matching names
- Use absolute paths if needed

### Python Environment Not Activated
```powershell
# Check if virtual environment is active (prompt should show (.venv))
# If not, activate it:
& "c:/Users/Gus/Documents/VS Code Projects/NINA Canon/.venv/Scripts/Activate.ps1"
```

## Technical Details

### How It Works

1. **FITS Reading**: Uses `astropy.io.fits`
   - Automatically applies BZERO/BSCALE corrections
   - Returns actual pixel values as `uint16`

2. **CR3 Reading**: Uses `rawpy`
   - Extracts raw Bayer array (no demosaicing)
   - Returns unprocessed sensor data as `uint16`

3. **Comparison**: Uses `numpy`
   - Calculates min/max/mean/median/stddev
   - Compares dimensions (if they match, does pixel-by-pixel)
   - Detects common bugs (BZERO offset, scaling issues)

### BZERO Bug Explanation

**FITS Standard for Unsigned 16-bit**:
```
BITPIX = 16       (signed short storage)
BZERO = 32768     (offset for unsigned interpretation)
stored_value = actual_value - 32768
```

**Bug Symptom**: If BZERO is not applied during write, values end up 32768 too high when read.

**CFitsio**: Handles BZERO automatically with `USHORT_IMG` type ✅

**CSharpFITS (Fixed)**: Now subtracts 32768 before casting to signed short ✅

## Related Files

- **`compare_fits_to_cr3.py`**: Main comparison script
- **`analyze_fits.py`**: FITS header/pixel analysis tool (doesn't need CR3)
- **`CSHARPFITS_BZERO_FIX.md`**: Documentation of the bug fix
- **`FITS_ANALYSIS_REPORT.md`**: Comprehensive analysis report

## Quick Reference Commands

```powershell
# Navigate to images folder
cd "c:\Users\Gus\Documents\VS Code Projects\NINA Canon\images"

# Run full comparison (default test images)
& "c:/Users/Gus/Documents/VS Code Projects/NINA Canon/.venv/Scripts/python.exe" compare_fits_to_cr3.py

# Analyze just FITS headers (no CR3 needed)
& "c:/Users/Gus/Documents/VS Code Projects/NINA Canon/.venv/Scripts/python.exe" analyze_fits.py

# Check Python version in venv
& "c:/Users/Gus/Documents/VS Code Projects/NINA Canon/.venv/Scripts/python.exe" --version

# Verify rawpy is installed
& "c:/Users/Gus/Documents/VS Code Projects/NINA Canon/.venv/Scripts/python.exe" -c "import rawpy; print('rawpy version:', rawpy.__version__)"
```

## Example Output Interpretation

### ✅ Successful Test (Bug Fixed)
```
FITS Pixel Statistics:
  Min:               1706
  Max:              12806
  Mean:           2045.66

CR3 Pixel Statistics:
  Min:               1706
  Max:              12806
  Mean:           2045.81

Mean Offset: 0.15
✅ BUG FIX VERIFIED: Values match source CR3!
```

### ❌ Failed Test (Bug Present)
```
FITS Pixel Statistics:
  Min:              34471
  Max:              45571
  Mean:          34813.08

CR3 Pixel Statistics:
  Min:               1703
  Max:              12803
  Mean:           2045.26

Mean Offset: 32767.82
❌ BZERO BUG DETECTED: +32768 offset
```
