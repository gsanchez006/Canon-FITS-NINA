using NINA.Core.Utility;
using NINA.Image.ImageData;
using NINA.Plugin.Canon.EDSDK.Native;
using System;
using System.IO;

namespace NINA.Plugin.Canon.EDSDK.Services
{
    /// <summary>
    /// CFitsio FITS writer using NASA CFitsio 4.6.3 library
    /// Supports compression (RICE, GZIP, PLIO, HCOMPRESS) and writes all NINA metadata headers
    /// </summary>
    public class CFitsioWriter
    {
        /// <summary>
        /// Write FITS file using CFitsio with compression support
        /// </summary>
        public void WriteFitsFile(NINA.Image.Interfaces.IImageArray rawDataArray, int width, int height, string outputPath, ImageMetaData metadata, int compressionType = CFitsioNative.RICE_1)
        {
            IntPtr fptr = IntPtr.Zero;
            int status = 0;

            try
            {
                Logger.Info($"  Creating FITS file with CFitsio: {width}x{height}");
                Logger.Info($"  Compression: {GetCompressionName(compressionType)}");

                // Delete existing file if it exists
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                // Create FITS file
                CFitsioNative.fits_create_file(out fptr, outputPath, out status);
                CFitsioNative.CheckStatus(status, "Creating FITS file");

                // Image dimensions
                long[] naxes = new long[] { width, height };

                // For compressed images, set compression parameters BEFORE creating image
                if (compressionType != CFitsioNative.NOCOMPRESS)
                {
                    Logger.Info($"  Setting compression type: {GetCompressionName(compressionType)}");
                    CFitsioNative.fits_set_compression_type(fptr, compressionType, out status);
                    CFitsioNative.CheckStatus(status, "Setting compression type");

                    // Set tile size for compression
                    // HCOMPRESS requires square tiles (powers of 2: 16x16, 32x32, etc.)
                    // Other methods work well with row-oriented tiles (width x 1)
                    int[] tileDims;
                    if (compressionType == CFitsioNative.HCOMPRESS_1)
                    {
                        // Use 16x16 tiles for HCOMPRESS (small tiles work better for lossless)
                        tileDims = new int[] { 16, 16 };
                        Logger.Info($"  Setting HCOMPRESS tile dimensions: 16x16");
                    }
                    else
                    {
                        // Row-oriented tiles for RICE, GZIP, PLIO (optimal for astronomy)
                        tileDims = new int[] { width, 1 };
                        Logger.Info($"  Setting tile dimensions: {width}x1");
                    }
                    CFitsioNative.fits_set_tile_dim(fptr, 2, tileDims, out status);
                    CFitsioNative.CheckStatus(status, "Setting tile dimensions");

                    // For HCOMPRESS, set scale factor (0 = lossless)
                    if (compressionType == CFitsioNative.HCOMPRESS_1)
                    {
                        CFitsioNative.fits_set_hcomp_scale(fptr, 0, out status);
                        CFitsioNative.CheckStatus(status, "Setting HCOMPRESS scale");
                        
                        CFitsioNative.fits_set_hcomp_smooth(fptr, 0, out status);
                        CFitsioNative.CheckStatus(status, "Setting HCOMPRESS smooth");
                    }
                }

                // Create image array (16-bit signed, we'll set BZERO for unsigned interpretation)
                // Note: With compression enabled, cfitsio creates a compressed IMAGE extension
                // and puts it in HDU 1 with an empty primary HDU 0. This is FITS standard,
                // but some software expects image in primary HDU. The NAXIS keywords are
                // automatically set correctly by cfitsio in both the primary and extension HDUs.
                Logger.Info($"  Creating image with naxis=2, naxes=[{naxes[0]}, {naxes[1]}], bitpix={CFitsioNative.SHORT_IMG}");
                CFitsioNative.fits_create_img(fptr, CFitsioNative.SHORT_IMG, 2, naxes, out status);
                CFitsioNative.CheckStatus(status, "Creating image HDU");

                // Set BZERO=32768 to interpret signed short as unsigned (0-65535 range)
                CFitsioNative.fits_update_key_lng(fptr, "BZERO", 32768, "Offset for unsigned integer data", out status);
                CFitsioNative.CheckStatus(status, "Writing BZERO");

                // Write BSCALE (must be 1 for raw data)
                CFitsioNative.fits_update_key_lng(fptr, "BSCALE", 1, "Data scaling factor", out status);
                CFitsioNative.CheckStatus(status, "Writing BSCALE");

                // Write all NINA metadata headers
                WriteNinaMetadata(fptr, metadata);

                // Write Canon-specific comments
                CFitsioNative.WriteComment(fptr, "Converted from Canon RAW using NINA in-memory data");
                CFitsioNative.WriteComment(fptr, "Full 14/16-bit dynamic range preserved");
                CFitsioNative.WriteComment(fptr, "No demosaicing, tone curves, or processing applied");
                CFitsioNative.WriteComment(fptr, $"CFitsio v4.6.3 - Compression: {GetCompressionName(compressionType)}");

                // Get pixel data as flat array
                var flatData = rawDataArray.FlatArray;
                
                // Debug: Check pixel value range
                if (flatData.Length > 0)
                {
                    ushort minVal = ushort.MaxValue;
                    ushort maxVal = ushort.MinValue;
                    for (int i = 0; i < Math.Min(flatData.Length, 10000); i++)
                    {
                        if (flatData[i] < minVal) minVal = flatData[i];
                        if (flatData[i] > maxVal) maxVal = flatData[i];
                    }
                    Logger.Info($"  Pixel value range (first 10k pixels): min={minVal}, max={maxVal}");
                }
                
                // Write image data as ushort[] (CFitsio will handle BZERO offset internally)
                // With BITPIX=16 and BZERO=32768, CFitsio stores: physical_value = ushort_value - 32768
                // When reading: ushort_value = physical_value + 32768
                Logger.Info($"  Writing {flatData.Length} pixels ({flatData.Length * 2 / 1024 / 1024.0:F2} MB uncompressed)");
                CFitsioNative.fits_write_img(fptr, CFitsioNative.TUSHORT, 1, flatData.Length, flatData, out status);
                CFitsioNative.CheckStatus(status, "Writing image data");

                // For compressed images, add informational keywords to primary HDU
                // to help software that only reads the primary HDU
                if (compressionType != CFitsioNative.NOCOMPRESS)
                {
                    Logger.Info("  Adding compatibility metadata to primary HDU");
                    
                    // Move to primary HDU (HDU 1 in 1-based indexing)
                    int hdutype;
                    CFitsioNative.fits_movabs_hdu(fptr, 1, out hdutype, out status);
                    CFitsioNative.CheckStatus(status, "Moving to primary HDU");
                    
                    // Add informational comments explaining the file structure
                    CFitsioNative.WriteComment(fptr, "This is a compressed FITS image following FITS 4.0 standard");
                    CFitsioNative.WriteComment(fptr, $"Image dimensions: {width} x {height} pixels (NAXIS1 x NAXIS2)");
                    CFitsioNative.WriteComment(fptr, "Actual image data is in HDU 1 (COMPRESSED_IMAGE extension)");
                    CFitsioNative.WriteComment(fptr, $"Compression: {GetCompressionName(compressionType)}");
                    CFitsioNative.WriteComment(fptr, "Use FITS software that supports compressed image extensions");
                }

                // Flush to disk
                CFitsioNative.fits_flush_file(fptr, out status);
                CFitsioNative.CheckStatus(status, "Flushing file");

                // Close file
                CFitsioNative.fits_close_file(fptr, out status);
                CFitsioNative.CheckStatus(status, "Closing file");

                fptr = IntPtr.Zero;

                // Log file size
                var fileInfo = new FileInfo(outputPath);
                if (fileInfo.Exists)
                {
                    double sizeMB = fileInfo.Length / 1024.0 / 1024.0;
                    double ratio = compressionType != CFitsioNative.NOCOMPRESS 
                        ? (1.0 - (fileInfo.Length / (double)(flatData.Length * 2))) * 100.0
                        : 0;
                    
                    if (compressionType != CFitsioNative.NOCOMPRESS)
                        Logger.Info($"  FITS file size: {sizeMB:F2} MB (compressed {ratio:F1}% smaller)");
                    else
                        Logger.Info($"  FITS file size: {sizeMB:F2} MB (uncompressed)");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"CFitsio writer error: {ex.Message}", ex);
                
                // Clean up file pointer if still open
                if (fptr != IntPtr.Zero)
                {
                    try
                    {
                        CFitsioNative.fits_close_file(fptr, out status);
                    }
                    catch { }
                }
                
                throw;
            }
        }

        /// <summary>
        /// Write all NINA metadata headers to FITS file
        /// Comprehensive implementation matching NINA's standard FITS output
        /// </summary>
        private void WriteNinaMetadata(IntPtr fptr, ImageMetaData metadata)
        {
            if (metadata == null) return;

            try
            {
                // Image metadata
                if (metadata.Image?.RecordedRMS != null)
                {
                    CFitsioNative.WriteKeyDouble(fptr, "RMS", metadata.Image.RecordedRMS.Total, "RMS error");
                    CFitsioNative.WriteKeyDouble(fptr, "RMSRA", metadata.Image.RecordedRMS.RA, "RMS error RA");
                    CFitsioNative.WriteKeyDouble(fptr, "RMSDEC", metadata.Image.RecordedRMS.Dec, "RMS error Dec");
                }

                if (metadata.Image?.ImageType != null)
                    CFitsioNative.WriteKeyString(fptr, "IMAGETYP", metadata.Image.ImageType, "Type of exposure");

                if (metadata.Image?.ExposureTime > 0)
                {
                    CFitsioNative.WriteKeyDouble(fptr, "EXPOSURE", metadata.Image.ExposureTime, "[s] Exposure duration");
                    CFitsioNative.WriteKeyDouble(fptr, "EXPTIME", metadata.Image.ExposureTime, "[s] Exposure duration");
                }

                if (metadata.Image?.ExposureStart != DateTime.MinValue)
                {
                    var exposureStart = metadata.Image.ExposureStart;
                    var exposureEnd = exposureStart.AddSeconds(metadata.Image.ExposureTime);
                    var exposureMid = exposureStart.AddSeconds(metadata.Image.ExposureTime / 2.0);
                    
                    CFitsioNative.WriteKeyString(fptr, "DATE-LOC", exposureStart.ToString("yyyy-MM-ddTHH:mm:ss.fffffff"), "Time of observation (local)");
                    CFitsioNative.WriteKeyString(fptr, "DATE-OBS", exposureStart.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffff"), "Time of observation (UTC)");
                    
                    // Modified Julian Date
                    double mjdObs = exposureStart.ToUniversalTime().ToOADate() + 15018.0;
                    double mjdAvg = exposureMid.ToUniversalTime().ToOADate() + 15018.0;
                    CFitsioNative.WriteKeyDouble(fptr, "MJD-OBS", mjdObs, "Modified Julian Date of observation");
                    CFitsioNative.WriteKeyString(fptr, "DATE-AVG", exposureMid.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffff"), "Averaged midpoint time (UTC)");
                    CFitsioNative.WriteKeyDouble(fptr, "MJD-AVG", mjdAvg, "Modified Julian Date of averaged midpoint");
                }

                // Camera metadata
                if (metadata.Camera?.BinX > 0)
                    CFitsioNative.WriteKeyInt(fptr, "XBINNING", metadata.Camera.BinX, "X axis binning factor");

                if (metadata.Camera?.BinY > 0)
                    CFitsioNative.WriteKeyInt(fptr, "YBINNING", metadata.Camera.BinY, "Y axis binning factor");

                if (metadata.Camera?.Gain >= 0)
                    CFitsioNative.WriteKeyDouble(fptr, "GAIN", metadata.Camera.Gain, "Sensor gain");

                if (metadata.Camera?.Offset >= 0)
                    CFitsioNative.WriteKeyDouble(fptr, "OFFSET", metadata.Camera.Offset, "Sensor gain offset");

                if (metadata.Camera?.PixelSize > 0)
                {
                    CFitsioNative.WriteKeyDouble(fptr, "XPIXSZ", metadata.Camera.PixelSize, "[um] Pixel X axis size");
                    CFitsioNative.WriteKeyDouble(fptr, "YPIXSZ", metadata.Camera.PixelSize, "[um] Pixel Y axis size");
                }

                if (!string.IsNullOrEmpty(metadata.Camera?.Name))
                    CFitsioNative.WriteKeyString(fptr, "INSTRUME", metadata.Camera.Name, "Imaging instrument name");

                if (!string.IsNullOrEmpty(metadata.Camera?.Id))
                    CFitsioNative.WriteKeyString(fptr, "CAMERAID", metadata.Camera.Id, "Imaging instrument identifier");

                if (metadata.Camera?.Temperature > -273)
                    CFitsioNative.WriteKeyDouble(fptr, "CCD-TEMP", metadata.Camera.Temperature, "[degC] CCD temperature");

                if (metadata.Camera?.SetPoint > -273)
                    CFitsioNative.WriteKeyDouble(fptr, "SET-TEMP", metadata.Camera.SetPoint, "[degC] CCD temperature setpoint");

                if (!string.IsNullOrEmpty(metadata.Camera?.ReadoutModeName))
                    CFitsioNative.WriteKeyString(fptr, "READOUTM", metadata.Camera.ReadoutModeName, "Sensor readout mode");

                if (metadata.Camera?.USBLimit >= 0)
                    CFitsioNative.WriteKeyInt(fptr, "USBLIMIT", metadata.Camera.USBLimit, "Camera-specific USB setting");

                if (metadata.Camera?.BayerOffsetX >= 0)
                    CFitsioNative.WriteKeyInt(fptr, "XBAYROFF", metadata.Camera.BayerOffsetX, "Bayer pattern X offset");

                if (metadata.Camera?.BayerOffsetY >= 0)
                    CFitsioNative.WriteKeyInt(fptr, "YBAYROFF", metadata.Camera.BayerOffsetY, "Bayer pattern Y offset");

                if (metadata.Camera?.BayerPattern != null)
                    CFitsioNative.WriteKeyString(fptr, "BAYERPAT", metadata.Camera.BayerPattern.ToString(), "Bayer color pattern");

                if (metadata.Camera?.ElectronsPerADU > 0)
                    CFitsioNative.WriteKeyDouble(fptr, "EGAIN", metadata.Camera.ElectronsPerADU, "[e-/ADU] Electrons per ADU");

                // Telescope metadata
                if (!string.IsNullOrEmpty(metadata.Telescope?.Name))
                    CFitsioNative.WriteKeyString(fptr, "TELESCOP", metadata.Telescope.Name, "Name of telescope");

                if (metadata.Telescope?.FocalLength > 0)
                    CFitsioNative.WriteKeyDouble(fptr, "FOCALLEN", metadata.Telescope.FocalLength, "[mm] Focal length");

                if (metadata.Telescope?.FocalRatio > 0)
                    CFitsioNative.WriteKeyDouble(fptr, "FOCRATIO", metadata.Telescope.FocalRatio, "Focal ratio");

                if (metadata.Telescope?.Coordinates != null)
                {
                    CFitsioNative.WriteKeyDouble(fptr, "RA", metadata.Telescope.Coordinates.RA, "[deg] RA of telescope");
                    CFitsioNative.WriteKeyDouble(fptr, "DEC", metadata.Telescope.Coordinates.Dec, "[deg] Declination of telescope");
                }

                if (metadata.Telescope?.Altitude >= 0)
                    CFitsioNative.WriteKeyDouble(fptr, "CENTALT", metadata.Telescope.Altitude, "[deg] Altitude of telescope");

                if (metadata.Telescope?.Azimuth >= 0)
                    CFitsioNative.WriteKeyDouble(fptr, "CENTAZ", metadata.Telescope.Azimuth, "[deg] Azimuth of telescope");

                if (metadata.Telescope?.Airmass > 0)
                    CFitsioNative.WriteKeyDouble(fptr, "AIRMASS", metadata.Telescope.Airmass, "Airmass at frame center");

                if (metadata.Telescope?.SideOfPier != null)
                    CFitsioNative.WriteKeyString(fptr, "PIERSIDE", metadata.Telescope.SideOfPier.ToString(), "Telescope pointing state");

                // Observer location
                if (metadata.Observer?.Elevation > -500)
                    CFitsioNative.WriteKeyDouble(fptr, "SITEELEV", metadata.Observer.Elevation, "[m] Observation site elevation");

                if (metadata.Observer?.Latitude >= -90 && metadata.Observer?.Latitude <= 90)
                    CFitsioNative.WriteKeyDouble(fptr, "SITELAT", metadata.Observer.Latitude, "[deg] Observation site latitude");

                if (metadata.Observer?.Longitude >= -180 && metadata.Observer?.Longitude <= 180)
                    CFitsioNative.WriteKeyDouble(fptr, "SITELONG", metadata.Observer.Longitude, "[deg] Observation site longitude");

                // Filter wheel metadata
                if (!string.IsNullOrEmpty(metadata.FilterWheel?.Name))
                    CFitsioNative.WriteKeyString(fptr, "FWHEEL", metadata.FilterWheel.Name, "Filter Wheel name");

                if (!string.IsNullOrEmpty(metadata.FilterWheel?.Filter))
                    CFitsioNative.WriteKeyString(fptr, "FILTER", metadata.FilterWheel.Filter, "Active filter name");

                // Target metadata
                if (!string.IsNullOrEmpty(metadata.Target?.Name))
                    CFitsioNative.WriteKeyString(fptr, "OBJECT", metadata.Target.Name, "Name of the object of interest");

                if (metadata.Target?.Coordinates != null)
                {
                    var raString = metadata.Target.Coordinates.RAString;
                    if (!string.IsNullOrEmpty(raString))
                        CFitsioNative.WriteKeyString(fptr, "OBJCTRA", raString, "[H M S] RA of imaged object");
                    
                    var decString = metadata.Target.Coordinates.DecString;
                    if (!string.IsNullOrEmpty(decString))
                        CFitsioNative.WriteKeyString(fptr, "OBJCTDEC", decString, "[D M S] Declination of imaged object");
                }

                if (metadata.Target?.PositionAngle >= 0)
                    CFitsioNative.WriteKeyDouble(fptr, "OBJCTROT", metadata.Target.PositionAngle, "[deg] planned rotation of imaged object");

                // Focuser metadata
                if (!string.IsNullOrEmpty(metadata.Focuser?.Name))
                    CFitsioNative.WriteKeyString(fptr, "FOCNAME", metadata.Focuser.Name, "Focusing equipment name");

                if (metadata.Focuser?.Position.HasValue == true && metadata.Focuser.Position.Value >= 0)
                {
                    CFitsioNative.WriteKeyDouble(fptr, "FOCPOS", (double)metadata.Focuser.Position.Value, "[step] Focuser position");
                    CFitsioNative.WriteKeyDouble(fptr, "FOCUSPOS", (double)metadata.Focuser.Position.Value, "[step] Focuser position");
                }

                if (metadata.Focuser?.StepSize > 0)
                    CFitsioNative.WriteKeyDouble(fptr, "FOCUSSZ", metadata.Focuser.StepSize, "[um] Focuser step size");

                if (metadata.Focuser?.Temperature > -273)
                {
                    CFitsioNative.WriteKeyDouble(fptr, "FOCTEMP", metadata.Focuser.Temperature, "[degC] Focuser temperature");
                    CFitsioNative.WriteKeyDouble(fptr, "FOCUSTEM", metadata.Focuser.Temperature, "[degC] Focuser temperature");
                }

                // Rotator metadata
                if (!string.IsNullOrEmpty(metadata.Rotator?.Name))
                    CFitsioNative.WriteKeyString(fptr, "ROTNAME", metadata.Rotator.Name, "Rotator equipment name");

                if (metadata.Rotator?.MechanicalPosition >= 0)
                {
                    CFitsioNative.WriteKeyDouble(fptr, "ROTATOR", metadata.Rotator.MechanicalPosition, "[deg] Mechanical rotator angle");
                    CFitsioNative.WriteKeyDouble(fptr, "ROTATANG", metadata.Rotator.MechanicalPosition, "[deg] Mechanical rotator angle");
                }

                if (metadata.Rotator?.StepSize > 0)
                    CFitsioNative.WriteKeyDouble(fptr, "ROTSTPSZ", metadata.Rotator.StepSize, "[deg] Rotator step size");

                // Weather metadata
                if (metadata.WeatherData?.CloudCover >= 0)
                    CFitsioNative.WriteKeyDouble(fptr, "CLOUDCVR", metadata.WeatherData.CloudCover, "[percent] Cloud cover");

                if (metadata.WeatherData?.DewPoint > -273)
                    CFitsioNative.WriteKeyDouble(fptr, "DEWPOINT", metadata.WeatherData.DewPoint, "[degC] Dew point");

                if (metadata.WeatherData?.Humidity >= 0)
                    CFitsioNative.WriteKeyDouble(fptr, "HUMIDITY", metadata.WeatherData.Humidity, "[percent] Relative humidity");

                if (metadata.WeatherData?.Pressure > 0)
                    CFitsioNative.WriteKeyDouble(fptr, "PRESSURE", metadata.WeatherData.Pressure, "[hPa] Air pressure");

                if (metadata.WeatherData?.Temperature > -273)
                    CFitsioNative.WriteKeyDouble(fptr, "AMBTEMP", metadata.WeatherData.Temperature, "[degC] Ambient air temperature");

                if (metadata.WeatherData?.WindDirection >= 0)
                    CFitsioNative.WriteKeyDouble(fptr, "WINDDIR", metadata.WeatherData.WindDirection, "[deg] Wind direction");

                if (metadata.WeatherData?.WindGust >= 0)
                    CFitsioNative.WriteKeyDouble(fptr, "WINDGUST", metadata.WeatherData.WindGust, "[kph] Wind gust");

                if (metadata.WeatherData?.WindSpeed >= 0)
                    CFitsioNative.WriteKeyDouble(fptr, "WINDSPD", metadata.WeatherData.WindSpeed, "[kph] Wind speed");

                if (metadata.WeatherData?.SkyQuality >= 0)
                    CFitsioNative.WriteKeyDouble(fptr, "SKYQLTY", metadata.WeatherData.SkyQuality, "[mag/arcsec^2] Sky quality");

                if (metadata.WeatherData?.SkyBrightness >= 0)
                    CFitsioNative.WriteKeyDouble(fptr, "SKYBRGHT", metadata.WeatherData.SkyBrightness, "[lux] Sky brightness");

                if (metadata.WeatherData?.StarFWHM >= 0)
                    CFitsioNative.WriteKeyDouble(fptr, "STARFWHM", metadata.WeatherData.StarFWHM, "[arcsec] Star FWHM");

                // Standard FITS keywords
                CFitsioNative.WriteKeyString(fptr, "ROWORDER", "TOP-DOWN", "FITS Image Orientation");
                CFitsioNative.WriteKeyDouble(fptr, "EQUINOX", 2000.0, "Equinox of celestial coordinate system");
                
                // Software version
                var ninaVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
                if (ninaVersion != null)
                    CFitsioNative.WriteKeyString(fptr, "SWCREATE", $"N.I.N.A. {ninaVersion} + Canon FITS Plugin", "Software that created this file");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error writing FITS metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Get human-readable compression name
        /// </summary>
        private string GetCompressionName(int compressionType)
        {
            switch (compressionType)
            {
                case CFitsioNative.RICE_1: return "RICE";
                case CFitsioNative.GZIP_1: return "GZIP Level 1";
                case CFitsioNative.GZIP_2: return "GZIP Level 2";
                case CFitsioNative.PLIO_1: return "PLIO";
                case CFitsioNative.HCOMPRESS_1: return "HCOMPRESS (lossless)";
                case CFitsioNative.NOCOMPRESS: return "None";
                default: return $"Unknown ({compressionType})";
            }
        }
    }
}
