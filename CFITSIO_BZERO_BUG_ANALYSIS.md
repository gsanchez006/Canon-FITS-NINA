# CFitsio BZERO/BSCALE Bug Analysis

## Issue Description

The CFitsio implementation produces FITS files that appear to have no pixel data or incorrect MTF values when opened in astronomy software, while the CSharpFITS implementation works correctly.

## Root Cause

**The bug is in how BZERO and BSCALE keywords are handled with compressed images.**

### Understanding BZERO/BSCALE in FITS

From the CFitsio Programmer's Guide (section 4.7 - Data Scaling):

1. **When writing data with BSCALE and BZERO present:**
   ```
   FITS_value = (input_value - BZERO) / BSCALE
   ```

2. **When reading data:**
   ```
   output_value = FITS_value * BSCALE + BZERO
   ```

3. **For unsigned 16-bit integers:**
   - BITPIX = 16 (signed short)
   - BSCALE = 1
   - BZERO = 32768
   - Input range: 0-65535 (unsigned)
   - Stored range: -32768 to 32767 (signed)

### The Bug in CFitsioWriter.cs

#### Current Implementation (INCORRECT)

```csharp
// Line 78-93
CFitsioNative.fits_create_img(fptr, CFitsioNative.SHORT_IMG, 2, naxes, out status);
CFitsioNative.CheckStatus(status, "Creating image HDU");

// Set BZERO=32768 to interpret signed short as unsigned (0-65535 range)
CFitsioNative.fits_update_key_lng(fptr, "BZERO", 32768, "Offset for unsigned integer data", out status);
CFitsioNative.CheckStatus(status, "Writing BZERO");

// Write BSCALE (must be 1 for raw data)
CFitsioNative.fits_update_key_lng(fptr, "BSCALE", 1, "Data scaling factor", out status);
CFitsioNative.CheckStatus(status, "Writing BSCALE");

// ... metadata ...

// Line 123
CFitsioNative.fits_write_img(fptr, CFitsioNative.TUSHORT, 1, flatData.Length, flatData, out status);
```

#### Why This Is Wrong

1. **With compression enabled:**
   - `fits_create_img()` creates a **compressed IMAGE extension** in HDU 1
   - The primary HDU (HDU 0) is **empty** with NAXIS=0
   - BZERO/BSCALE are written to the **compressed extension HDU**
   - The **primary HDU has no BZERO/BSCALE**

2. **Many FITS readers:**
   - Only check the primary HDU (HDU 0)
   - Find NAXIS=0, assume no image data
   - Never look at the compressed extension in HDU 1
   - Even if they do, they might not apply the correct scaling

3. **The data is written correctly** but the metadata is in the wrong place

### Comparison with CSharpFITS (CORRECT)

```csharp
// RawToFitsConverter.cs lines 141-168
var flatData = rawDataArray.FlatArray;

// Convert to jagged array for FITS (CSharpFITS doesn't support rectangular arrays)
short[][] imageArray = new short[height][];

for (int y = 0; y < height; y++)
{
    imageArray[y] = new short[width];
    for (int x = 0; x < width; x++)
    {
        imageArray[y][x] = (short)flatData[y * width + x];  // Cast ushort to short
    }
}

// Create FITS file
Fits fits = new Fits();
ImageHDU hdu = (ImageHDU)Fits.MakeHDU(imageArray);

// Add FITS headers
var header = hdu.Header;

// Add BZERO for unsigned 16-bit interpretation (critical for proper display)
header.AddValue("BZERO", 32768, "Offset for unsigned integer data");
```

#### Why This Works

1. **Creates uncompressed primary image** - data is in HDU 0
2. **BZERO is in the same HDU as the data**
3. **Data is written as signed short** (after casting from ushort)
4. **All readers can find the data and scaling parameters**

## The Fix

### Option 1: Disable Automatic Scaling (RECOMMENDED)

Use `fits_set_bscale()` to disable automatic scaling and write pre-scaled data:

```csharp
// After fits_create_img(), BEFORE writing BZERO/BSCALE
CFitsioNative.fits_create_img(fptr, CFitsioNative.SHORT_IMG, 2, naxes, out status);
CFitsioNative.CheckStatus(status, "Creating image HDU");

// Disable automatic scaling - we'll write raw signed values
CFitsioNative.fits_set_bscale(fptr, 1.0, 0.0, out status);
CFitsioNative.CheckStatus(status, "Disabling automatic scaling");

// NOW write BZERO/BSCALE keywords (for readers, not for writing)
CFitsioNative.fits_update_key_lng(fptr, "BZERO", 32768, "Offset for unsigned integer data", out status);
CFitsioNative.CheckStatus(status, "Writing BZERO");

CFitsioNative.fits_update_key_lng(fptr, "BSCALE", 1, "Data scaling factor", out status);
CFitsioNative.CheckStatus(status, "Writing BSCALE");

// ... metadata ...

// Convert ushort[] to short[] by subtracting 32768 (apply the offset ourselves)
short[] signedData = new short[flatData.Length];
for (int i = 0; i < flatData.Length; i++)
{
    signedData[i] = (short)(flatData[i] - 32768);
}

// Write as signed short (no automatic scaling)
CFitsioNative.fits_write_img(fptr, CFitsioNative.TSHORT, 1, signedData.Length, signedData, out status);
```

### Option 2: Use USHORT_IMG Instead of SHORT_IMG

Create the image as explicitly unsigned:

```csharp
// Use USHORT_IMG which tells cfitsio to handle unsigned shorts automatically
CFitsioNative.fits_create_img(fptr, CFitsioNative.USHORT_IMG, 2, naxes, out status);
CFitsioNative.CheckStatus(status, "Creating image HDU");

// DO NOT manually write BZERO/BSCALE - cfitsio sets them automatically
// CFitsio sets: BITPIX=16, BZERO=32768, BSCALE=1

// ... metadata ...

// Write as unsigned short - cfitsio handles the conversion
CFitsioNative.fits_write_img(fptr, CFitsioNative.TUSHORT, 1, flatData.Length, flatData, out status);
```

### Option 3: Write BZERO/BSCALE to Primary HDU (Compression Only)

For compressed images, ensure BZERO/BSCALE are also in the primary HDU:

```csharp
// After writing the compressed image data and closing that HDU...

if (compressionType != CFitsioNative.NOCOMPRESS)
{
    // Move to primary HDU
    int hdutype;
    CFitsioNative.fits_movabs_hdu(fptr, 1, out hdutype, out status);
    CFitsioNative.CheckStatus(status, "Moving to primary HDU");
    
    // Write BZERO/BSCALE to primary HDU as well
    CFitsioNative.fits_update_key_lng(fptr, "BZERO", 32768, "Offset for unsigned integer data", out status);
    CFitsioNative.CheckStatus(status, "Writing BZERO to primary HDU");
    
    CFitsioNative.fits_update_key_lng(fptr, "BSCALE", 1, "Data scaling factor", out status);
    CFitsioNative.CheckStatus(status, "Writing BSCALE to primary HDU");
    
    // Add image dimension keywords to primary HDU
    CFitsioNative.fits_update_key_lng(fptr, "NAXIS1", width, "Image width", out status);
    CFitsioNative.fits_update_key_lng(fptr, "NAXIS2", height, "Image height", out status);
    
    // Add comments...
}
```

## Required CFitsio Native Function

Add this to `CFitsioNative.cs`:

```csharp
/// <summary>
/// Set/reset the scaling parameters (BSCALE, BZERO)
/// Use bscale=1.0, bzero=0.0 to disable automatic scaling
/// </summary>
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int fits_set_bscale(IntPtr fptr, double bscale, double bzero, out int status);
```

## Verification

After implementing the fix:

1. **Check the FITS file with fitsverify:**
   ```bash
   fitsverify cfitsio.fits
   ```

2. **Inspect headers with cfitsio utility:**
   ```bash
   fitshdr cfitsio.fits
   ```

3. **Compare pixel values:**
   - Both files should show the same pixel value ranges
   - MTF values should match between CSharpFITS and CFitsio versions

4. **Test with multiple readers:**
   - PixInsight
   - MaxIm DL
   - AstroPixelProcessor
   - DS9
   - NINA's built-in preview

## Additional Notes

### Why MTF Values Differ

MTF (Mid-Tone Function) values are calculated from pixel statistics. If the BZERO offset isn't applied correctly:

- **Without BZERO:** Values appear as -32768 to 32767
- **With BZERO:** Values appear as 0 to 65535

This would cause MTF to calculate different mid-tones since the value range is shifted by 32768.

### Compression and HDU Structure

From CFitsio guide section 5.6 (Image Compression):

> "Compressed images are stored as binary tables in a special extension. The actual image data is in a binary table, and an empty primary HDU with NAXIS=0 is created."

This is why compressed images need special handling - the data isn't where traditional FITS readers expect it.

## Recommended Solution

**Option 2 (Use USHORT_IMG)** is the cleanest and most correct approach:

1. Let CFitsio handle all the unsigned integer details
2. Follows the CFitsio design patterns from the documentation
3. Minimal code changes
4. Most reliable across all compression modes

This is exactly what the CFitsio documentation recommends in section 4.4 (Support for Unsigned Integers and Signed Bytes).
