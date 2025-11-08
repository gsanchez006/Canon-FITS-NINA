#!/usr/bin/env python3
"""
Analyze FITS files to compare CSharpFITS vs CFitsio implementations.
Checks BZERO/BSCALE, HDU structure, and pixel statistics.
"""

import sys
from astropy.io import fits
import numpy as np

def analyze_fits_file(filepath):
    """Analyze a FITS file and return detailed information."""
    print(f"\n{'='*80}")
    print(f"Analyzing: {filepath}")
    print(f"{'='*80}")
    
    with fits.open(filepath) as hdul:
        print(f"\nNumber of HDUs: {len(hdul)}")
        print(f"\nHDU Structure:")
        hdul.info()
        
        # Analyze each HDU
        for i, hdu in enumerate(hdul):
            print(f"\n{'-'*80}")
            print(f"HDU {i}: {hdu.name} ({type(hdu).__name__})")
            print(f"{'-'*80}")
            
            header = hdu.header
            
            # Check for key keywords
            keywords_to_check = ['NAXIS', 'NAXIS1', 'NAXIS2', 'BITPIX', 'BZERO', 'BSCALE', 
                                'ZCMPTYPE', 'ZBITPIX', 'ZNAXIS', 'ZNAXIS1', 'ZNAXIS2']
            
            print("\nKey Keywords:")
            for keyword in keywords_to_check:
                if keyword in header:
                    value = header[keyword]
                    comment = header.comments[keyword] if keyword in header.comments else ""
                    print(f"  {keyword:10s} = {str(value):>20s}  / {comment}")
            
            # If this HDU has image data, analyze it
            if hasattr(hdu, 'data') and hdu.data is not None:
                data = hdu.data
                
                if data.size > 0:
                    print(f"\nData Information:")
                    print(f"  Shape: {data.shape}")
                    print(f"  Dtype: {data.dtype}")
                    print(f"  Size: {data.size} pixels")
                    
                    print(f"\nPixel Statistics (RAW from file):")
                    print(f"  Min:    {np.min(data):>15.2f}")
                    print(f"  Max:    {np.max(data):>15.2f}")
                    print(f"  Mean:   {np.mean(data):>15.2f}")
                    print(f"  Median: {np.median(data):>15.2f}")
                    print(f"  StdDev: {np.std(data):>15.2f}")
                    
                    # Calculate percentiles for MTF analysis
                    print(f"\nPercentiles (for MTF calculation):")
                    percentiles = [0.1, 1, 5, 10, 25, 50, 75, 90, 95, 99, 99.9]
                    for p in percentiles:
                        val = np.percentile(data, p)
                        print(f"  {p:5.1f}%: {val:>15.2f}")
                    
                    # Check for signed vs unsigned range
                    if np.min(data) < 0:
                        print(f"\n  ⚠️  Data contains NEGATIVE values (signed range)")
                        print(f"      Range: {np.min(data):.0f} to {np.max(data):.0f}")
                    else:
                        print(f"\n  ✓ Data is all POSITIVE (unsigned range)")
                        print(f"      Range: {np.min(data):.0f} to {np.max(data):.0f}")
                    
                    # Check if BZERO/BSCALE were applied
                    if 'BZERO' in header and 'BSCALE' in header:
                        bzero = header['BZERO']
                        bscale = header['BSCALE']
                        print(f"\n  BZERO={bzero}, BSCALE={bscale} keywords present")
                        
                        if bzero == 32768 and bscale == 1:
                            # This is the unsigned 16-bit conversion
                            if np.min(data) >= 0 and np.max(data) <= 65535:
                                print(f"  ✓ Data correctly scaled to unsigned 16-bit range (0-65535)")
                            elif np.min(data) >= -32768 and np.max(data) <= 32767:
                                print(f"  ⚠️  Data in signed range but BZERO=32768!")
                                print(f"      BZERO offset may NOT have been applied during reading")
                                print(f"      Astropy automatically applies it, but some readers may not")
                            else:
                                print(f"  ⚠️  Unexpected data range!")
                    
                    # Sample some pixel values
                    print(f"\nSample pixel values (first 10):")
                    if len(data.shape) == 2:
                        sample = data[0, :10]
                    else:
                        sample = data.flat[:10]
                    print(f"  {sample}")

def compare_images(file1, file2):
    """Compare two FITS images pixel-by-pixel."""
    print(f"\n{'='*80}")
    print(f"COMPARISON: {file1} vs {file2}")
    print(f"{'='*80}")
    
    with fits.open(file1) as hdul1, fits.open(file2) as hdul2:
        # Find the image data in each file
        data1 = None
        data2 = None
        
        for hdu in hdul1:
            if hasattr(hdu, 'data') and hdu.data is not None and hdu.data.size > 0:
                data1 = hdu.data
                break
        
        for hdu in hdul2:
            if hasattr(hdu, 'data') and hdu.data is not None and hdu.data.size > 0:
                data2 = hdu.data
                break
        
        if data1 is None or data2 is None:
            print("ERROR: Could not find image data in one or both files")
            return
        
        print(f"\nImage 1 shape: {data1.shape}, dtype: {data1.dtype}")
        print(f"Image 2 shape: {data2.shape}, dtype: {data2.dtype}")
        
        if data1.shape != data2.shape:
            print("\n⚠️  Images have DIFFERENT shapes!")
            return
        
        # Compare statistics
        print(f"\nStatistical Comparison:")
        print(f"{'Metric':<15} {'Image 1':>20} {'Image 2':>20} {'Difference':>20}")
        print(f"{'-'*80}")
        
        stats = [
            ('Min', np.min(data1), np.min(data2)),
            ('Max', np.max(data1), np.max(data2)),
            ('Mean', np.mean(data1), np.mean(data2)),
            ('Median', np.median(data1), np.median(data2)),
            ('StdDev', np.std(data1), np.std(data2)),
        ]
        
        for name, val1, val2 in stats:
            diff = val2 - val1
            print(f"{name:<15} {val1:>20.2f} {val2:>20.2f} {diff:>20.2f}")
        
        # Check if images are identical
        if np.array_equal(data1, data2):
            print(f"\n✓ Images are PIXEL-PERFECT IDENTICAL")
        else:
            # Calculate difference
            diff = data2.astype(np.float64) - data1.astype(np.float64)
            abs_diff = np.abs(diff)
            
            num_different = np.count_nonzero(diff)
            pct_different = 100.0 * num_different / diff.size
            
            print(f"\n⚠️  Images are DIFFERENT")
            print(f"  Pixels different: {num_different:,} ({pct_different:.2f}%)")
            print(f"  Max abs diff: {np.max(abs_diff):.2f}")
            print(f"  Mean abs diff: {np.mean(abs_diff):.2f}")
            print(f"  Median abs diff: {np.median(abs_diff):.2f}")
            
            # Check if it's just an offset (BZERO issue)
            unique_diffs = np.unique(diff)
            if len(unique_diffs) == 1:
                offset = unique_diffs[0]
                print(f"\n  ✓ All pixels differ by CONSTANT OFFSET: {offset:.0f}")
                if offset == 32768 or offset == -32768:
                    print(f"    This is likely a BZERO offset issue!")

if __name__ == '__main__':
    import os
    
    # Analyze both FITS files
    base_path = r"c:\Users\Gus\Documents\VS Code Projects\NINA Canon\images"
    
    csharp_fits = os.path.join(base_path, "csharp.fits")
    cfitsio_fits = os.path.join(base_path, "cfitsio.fits")
    
    if os.path.exists(csharp_fits):
        analyze_fits_file(csharp_fits)
    else:
        print(f"ERROR: {csharp_fits} not found")
    
    if os.path.exists(cfitsio_fits):
        analyze_fits_file(cfitsio_fits)
    else:
        print(f"ERROR: {cfitsio_fits} not found")
    
    # Compare the two
    if os.path.exists(csharp_fits) and os.path.exists(cfitsio_fits):
        compare_images(csharp_fits, cfitsio_fits)
