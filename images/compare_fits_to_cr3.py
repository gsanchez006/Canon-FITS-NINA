#!/usr/bin/env python3
"""
FITS to Canon RAW Comparison Tool
Compares FITS files against their source Canon CR3 files to verify conversion accuracy.
"""

import sys
import os
from astropy.io import fits
import numpy as np

def extract_cr3_raw_data(cr3_path):
    """
    Extract raw pixel data from Canon CR3 file using rawpy.
    """
    try:
        import rawpy
        
        with rawpy.imread(cr3_path) as raw:
            # Get the raw image data (unprocessed Bayer array)
            raw_image = raw.raw_image.copy()
            
            # CR3 metadata
            print(f"\nCR3 File Information:")
            print(f"  Dimensions: {raw_image.shape[1]} x {raw_image.shape[0]}")
            print(f"  Dtype: {raw_image.dtype}")
            print(f"  Color description: {raw.color_desc.decode('utf-8')}")
            print(f"  Raw pattern: {raw.raw_pattern.tolist()}")
            print(f"  Black levels: {raw.black_level_per_channel}")
            print(f"  White level: {raw.white_level}")
            
            # Statistics
            print(f"\nCR3 Pixel Statistics:")
            print(f"  Min:    {np.min(raw_image):>15}")
            print(f"  Max:    {np.max(raw_image):>15}")
            print(f"  Mean:   {np.mean(raw_image):>15.2f}")
            print(f"  Median: {np.median(raw_image):>15.2f}")
            print(f"  StdDev: {np.std(raw_image):>15.2f}")
            
            return raw_image, raw
            
    except ImportError:
        print("\n⚠️  rawpy library not installed.")
        print("   Install with: pip install rawpy")
        print("   This library is needed to read Canon CR3 raw data.\n")
        return None, None
    except Exception as e:
        print(f"\n❌ Error reading CR3 file: {e}\n")
        return None, None

def analyze_fits_file(fits_path):
    """Analyze FITS file and return pixel data and metadata."""
    print(f"\n{'='*80}")
    print(f"Analyzing FITS: {os.path.basename(fits_path)}")
    print(f"{'='*80}")
    
    try:
        with fits.open(fits_path) as hdul:
            # Get primary HDU data
            data = hdul[0].data
            header = hdul[0].header
            
            print(f"\nFITS Header Information:")
            print(f"  NAXIS1 (width):  {header.get('NAXIS1', 'N/A')}")
            print(f"  NAXIS2 (height): {header.get('NAXIS2', 'N/A')}")
            print(f"  BITPIX: {header.get('BITPIX', 'N/A')}")
            print(f"  BZERO:  {header.get('BZERO', 'N/A')}")
            print(f"  BSCALE: {header.get('BSCALE', 'N/A')}")
            
            print(f"\nFITS Data Information:")
            print(f"  Shape: {data.shape}")
            print(f"  Dtype: {data.dtype}")
            
            # Note: Astropy automatically applies BZERO/BSCALE
            print(f"\nFITS Pixel Statistics (after BZERO/BSCALE applied by Astropy):")
            print(f"  Min:    {np.min(data):>15}")
            print(f"  Max:    {np.max(data):>15}")
            print(f"  Mean:   {np.mean(data):>15.2f}")
            print(f"  Median: {np.median(data):>15.2f}")
            print(f"  StdDev: {np.std(data):>15.2f}")
            
            return data, header
            
    except Exception as e:
        print(f"\n❌ Error reading FITS file: {e}\n")
        return None, None

def compare_fits_to_cr3(fits_path, cr3_path):
    """Compare FITS file to its source CR3 file."""
    print(f"\n{'='*80}")
    print(f"COMPARISON: {os.path.basename(fits_path)} ↔ {os.path.basename(cr3_path)}")
    print(f"{'='*80}")
    
    # Load FITS data
    fits_data, fits_header = analyze_fits_file(fits_path)
    if fits_data is None:
        return None
    
    # Load CR3 data
    print(f"\n{'='*80}")
    print(f"Analyzing CR3: {os.path.basename(cr3_path)}")
    print(f"{'='*80}")
    
    cr3_data, cr3_raw = extract_cr3_raw_data(cr3_path)
    if cr3_data is None:
        return None
    
    # Compare dimensions
    print(f"\n{'='*80}")
    print("DIMENSION COMPARISON")
    print(f"{'='*80}")
    
    fits_height, fits_width = fits_data.shape
    cr3_height, cr3_width = cr3_data.shape
    
    print(f"FITS: {fits_width} x {fits_height}")
    print(f"CR3:  {cr3_width} x {cr3_height}")
    
    if fits_width != cr3_width or fits_height != cr3_height:
        print("\n⚠️  DIMENSIONS DO NOT MATCH!")
        print("   This could be due to:")
        print("   - Different cropping/borders in FITS vs CR3")
        print("   - FITS may exclude optical black areas")
        return {
            'dimensions_match': False,
            'fits_shape': (fits_width, fits_height),
            'cr3_shape': (cr3_width, cr3_height)
        }
    else:
        print("\n✓ Dimensions match perfectly")
    
    # Compare pixel data
    print(f"\n{'='*80}")
    print("PIXEL DATA COMPARISON")
    print(f"{'='*80}")
    
    # Direct comparison
    pixel_diff = fits_data.astype(np.int32) - cr3_data.astype(np.int32)
    
    identical_pixels = np.sum(pixel_diff == 0)
    different_pixels = np.sum(pixel_diff != 0)
    total_pixels = fits_data.size
    
    pct_identical = 100.0 * identical_pixels / total_pixels
    
    print(f"\nPixel-by-Pixel Comparison:")
    print(f"  Total pixels:     {total_pixels:>15,}")
    print(f"  Identical pixels: {identical_pixels:>15,} ({pct_identical:.4f}%)")
    print(f"  Different pixels: {different_pixels:>15,} ({100-pct_identical:.4f}%)")
    
    if identical_pixels == total_pixels:
        print(f"\n✓ ✓ ✓  PIXEL-PERFECT MATCH!  ✓ ✓ ✓")
        print("The FITS file is a bit-perfect representation of the CR3 raw data.")
        is_perfect = True
    else:
        print(f"\n⚠️  Pixels differ between FITS and CR3")
        
        # Analyze differences
        print(f"\nDifference Statistics:")
        print(f"  Min diff:  {np.min(pixel_diff):>15}")
        print(f"  Max diff:  {np.max(pixel_diff):>15}")
        print(f"  Mean diff: {np.mean(pixel_diff):>15.2f}")
        print(f"  Abs mean:  {np.mean(np.abs(pixel_diff)):>15.2f}")
        print(f"  StdDev:    {np.std(pixel_diff):>15.2f}")
        
        # Check if it's a constant offset
        unique_diffs = np.unique(pixel_diff)
        print(f"  Unique diff values: {len(unique_diffs)}")
        
        if len(unique_diffs) == 1 and unique_diffs[0] != 0:
            print(f"\n  ℹ  All pixels differ by constant offset: {unique_diffs[0]}")
        elif len(unique_diffs) <= 10:
            print(f"\n  ℹ  Small number of unique differences: {unique_diffs.tolist()}")
        
        is_perfect = False
    
    # Value range comparison
    print(f"\n{'='*80}")
    print("VALUE RANGE COMPARISON")
    print(f"{'='*80}")
    
    fits_min, fits_max = np.min(fits_data), np.max(fits_data)
    cr3_min, cr3_max = np.min(cr3_data), np.max(cr3_data)
    
    print(f"\nFITS range: {fits_min} to {fits_max}")
    print(f"CR3 range:  {cr3_min} to {cr3_max}")
    
    if fits_min == cr3_min and fits_max == cr3_max:
        print("✓ Value ranges match")
    else:
        print("⚠️  Value ranges differ")
    
    return {
        'dimensions_match': True,
        'pixel_perfect': is_perfect,
        'identical_pixels': identical_pixels,
        'different_pixels': different_pixels,
        'total_pixels': total_pixels,
        'pct_identical': pct_identical,
        'fits_range': (fits_min, fits_max),
        'cr3_range': (cr3_min, cr3_max),
        'diff_stats': {
            'min': np.min(pixel_diff),
            'max': np.max(pixel_diff),
            'mean': np.mean(pixel_diff),
            'abs_mean': np.mean(np.abs(pixel_diff)),
            'std': np.std(pixel_diff)
        }
    }

def main():
    """Main comparison workflow."""
    base_path = r"c:\Users\Gus\Documents\VS Code Projects\NINA Canon\images"
    
    print("="*80)
    print("FITS to Canon CR3 Comparison Tool")
    print("="*80)
    print("\nThis tool compares FITS files against their source Canon CR3 files")
    print("to verify conversion accuracy.\n")
    
    # Comparison 1: CSharpFITS
    print("\n" + "█"*80)
    print("█" + " "*78 + "█")
    print("█" + "  COMPARISON 1: CSharpFITS Implementation".center(78) + "█")
    print("█" + " "*78 + "█")
    print("█"*80)
    
    csharp_fits = os.path.join(base_path, "csharp.fits")
    csharp_cr3 = os.path.join(base_path, "csharp.cr3")
    
    csharp_result = compare_fits_to_cr3(csharp_fits, csharp_cr3)
    
    # Comparison 2: CFitsio
    print("\n" + "█"*80)
    print("█" + " "*78 + "█")
    print("█" + "  COMPARISON 2: CFitsio Implementation".center(78) + "█")
    print("█" + " "*78 + "█")
    print("█"*80)
    
    cfitsio_fits = os.path.join(base_path, "cfitsio.fits")
    cfitsio_cr3 = os.path.join(base_path, "cfitsio.cr3")
    
    cfitsio_result = compare_fits_to_cr3(cfitsio_fits, cfitsio_cr3)
    
    # Final Summary
    if csharp_result and cfitsio_result:
        print("\n" + "="*80)
        print("FINAL SUMMARY")
        print("="*80)
        
        print("\n┌" + "─"*78 + "┐")
        print("│" + " CSharpFITS Implementation ".center(78) + "│")
        print("├" + "─"*78 + "┤")
        
        if csharp_result.get('pixel_perfect'):
            print("│  ✓ PIXEL-PERFECT CONVERSION".ljust(79) + "│")
            print("│    100% of pixels match the CR3 source exactly".ljust(79) + "│")
        else:
            pct = csharp_result.get('pct_identical', 0)
            print(f"│  ⚠️  {pct:.4f}% pixels identical".ljust(79) + "│")
            if csharp_result.get('different_pixels', 0) > 0:
                diff_mean = csharp_result['diff_stats']['abs_mean']
                print(f"│     Mean absolute difference: {diff_mean:.2f}".ljust(79) + "│")
        
        print("└" + "─"*78 + "┘")
        
        print("\n┌" + "─"*78 + "┐")
        print("│" + " CFitsio Implementation ".center(78) + "│")
        print("├" + "─"*78 + "┤")
        
        if cfitsio_result.get('pixel_perfect'):
            print("│  ✓ PIXEL-PERFECT CONVERSION".ljust(79) + "│")
            print("│    100% of pixels match the CR3 source exactly".ljust(79) + "│")
        else:
            pct = cfitsio_result.get('pct_identical', 0)
            print(f"│  ⚠️  {pct:.4f}% pixels identical".ljust(79) + "│")
            if cfitsio_result.get('different_pixels', 0) > 0:
                diff_mean = cfitsio_result['diff_stats']['abs_mean']
                print(f"│     Mean absolute difference: {diff_mean:.2f}".ljust(79) + "│")
        
        print("└" + "─"*78 + "┘")
        
        # Determine which is better
        print("\n" + "="*80)
        print("VERDICT")
        print("="*80 + "\n")
        
        csharp_perfect = csharp_result.get('pixel_perfect', False)
        cfitsio_perfect = cfitsio_result.get('pixel_perfect', False)
        
        if csharp_perfect and cfitsio_perfect:
            print("🏆 BOTH implementations create PIXEL-PERFECT FITS files!")
            print("   No difference in quality - both are excellent.")
        elif csharp_perfect:
            print("🏆 WINNER: CSharpFITS Implementation")
            print("   Creates pixel-perfect conversions.")
            print("   CFitsio has some pixel differences.")
        elif cfitsio_perfect:
            print("🏆 WINNER: CFitsio Implementation")
            print("   Creates pixel-perfect conversions.")
            print("   CSharpFITS has some pixel differences.")
        else:
            csharp_pct = csharp_result.get('pct_identical', 0)
            cfitsio_pct = cfitsio_result.get('pct_identical', 0)
            
            if csharp_pct > cfitsio_pct:
                print(f"🏆 WINNER: CSharpFITS Implementation")
                print(f"   {csharp_pct:.4f}% accuracy vs {cfitsio_pct:.4f}%")
            elif cfitsio_pct > csharp_pct:
                print(f"🏆 WINNER: CFitsio Implementation")
                print(f"   {cfitsio_pct:.4f}% accuracy vs {csharp_pct:.4f}%")
            else:
                print("   TIE: Both implementations have equal accuracy")
                print(f"   {csharp_pct:.4f}% pixels match source")

if __name__ == '__main__':
    main()
