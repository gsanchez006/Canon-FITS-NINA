using NINA.Core.Utility;
using NINA.Image.ImageData;
using nom.tam.fits;
using nom.tam.util;
using System;
using System.IO;
using System.Threading.Tasks;

namespace NINA.Plugin.Canon.EDSDK.Services
{
    /// <summary>
    /// Service that converts NINA's in-memory Canon camera data to FITS format
    /// </summary>
    public class RawToFitsConverter
    {
        public RawToFitsConverter()
        {
            Logger.Info("RawToFitsConverter created - will convert from NINA's in-memory image data");
        }

        /// <summary>
        /// Convert NINA's in-memory image data directly to FITS
        /// This bypasses the need to process Canon RAW files with Canon EDSDK!
        /// NINA will save the RAW file (CR3/CR2/CRW), and we save a FITS file from the same data.
        /// </summary>
        public async Task<string> ConvertImageDataToFitsAsync(NINA.Image.Interfaces.IImageData imageData, string outputDirectory, string originalFilePath, bool deleteOriginal = false, bool useCfitsio = false, int compressionType = 11)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    Logger.Info($"  Converting in-memory image data to FITS (using {(useCfitsio ? "cfitsio native" : "CSharpFITS")})...");

                    // Generate output filename based on original Canon RAW filename (CR3/CR2/CRW)
                    string outputPath = GenerateOutputPath(originalFilePath, outputDirectory);

                    // Get image properties
                    var properties = imageData.Properties;
                    int width = properties.Width;
                    int height = properties.Height;
                    
                    Logger.Info($"  Image size: {width}x{height}");
                    
                    // Get raw pixel data from NINA (IImageArray)
                    // Note: NINA provides the camera's native bit depth (e.g., 14-bit from Canon EOS RP)
                    // stored in ushort[] (16-bit) containers. Data is NOT scaled - values use only the
                    // lower bits (0-16383 for 14-bit cameras, not 0-65535).
                    var rawDataArray = imageData.Data;
                    
                    // Create FITS file using selected writer
                    if (useCfitsio)
                    {
                        try
                        {
                            CreateFitsFileWithCfitsio(rawDataArray, width, height, outputPath, imageData.MetaData, compressionType);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"cfitsio writer failed, falling back to CSharpFITS: {ex.Message}");
                            CreateFitsFileFromNinaData(rawDataArray, width, height, outputPath, imageData.MetaData);
                        }
                    }
                    else
                    {
                        CreateFitsFileFromNinaData(rawDataArray, width, height, outputPath, imageData.MetaData);
                    }

                    Logger.Info($"  FITS file: {Path.GetFileName(outputPath)} ({new FileInfo(outputPath).Length / 1024 / 1024:F2} MB)");
                    
                    // Optionally delete the Canon RAW file after saving FITS
                    if (deleteOriginal && File.Exists(originalFilePath))
                    {
                        // Wait a bit longer to ensure NINA has completely released the file
                        await Task.Delay(2000);
                        
                        // Retry deletion up to 5 times with delays
                        bool deleted = false;
                        for (int i = 0; i < 5 && !deleted; i++)
                        {
                            try
                            {
                                File.Delete(originalFilePath);
                                Logger.Info($"  Deleted original Canon RAW file: {originalFilePath}");
                                deleted = true;
                            }
                            catch (Exception ex)
                            {
                                if (i < 4)
                                {
                                    Logger.Debug($"  Delete attempt {i + 1} failed for {originalFilePath}, retrying... ({ex.Message})");
                                    await Task.Delay(1000);
                                }
                                else
                                {
                                    Logger.Warning($"  Could not delete original file {originalFilePath} after 5 attempts: {ex.Message}");
                                }
                            }
                        }
                    }
                    
                    return outputPath;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error converting image data to FITS: {ex.Message}", ex);
                    throw;
                }
            });
        }

        /// <summary>
        /// Generate output file path for FITS file
        /// </summary>
        private string GenerateOutputPath(string rawFilePath, string outputDirectory)
        {
            string fileName = Path.GetFileNameWithoutExtension(rawFilePath);
            string outputDir = string.IsNullOrEmpty(outputDirectory) 
                ? Path.GetDirectoryName(rawFilePath) 
                : outputDirectory;

            // Ensure output directory exists
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            return Path.Combine(outputDir, fileName + ".fits");
        }

        /// <summary>
        /// Create FITS file directly from NINA's image data (bypasses Canon EDSDK completely!)
        /// Uses CSharpFITS library (pure C# implementation)
        /// </summary>
        private void CreateFitsFileFromNinaData(NINA.Image.Interfaces.IImageArray rawDataArray, int width, int height, string outputPath, ImageMetaData metadata)
        {
            try
            {
                Logger.Info($"  Creating FITS file: {width}x{height}");

                // NINA's IImageArray.FlatArray gives us the raw pixel data as ushort[]
                var flatData = rawDataArray.FlatArray;
                
                Logger.Info($"  Raw data: {flatData.Length} pixels, 16-bit");

                // Convert to jagged array for FITS (CSharpFITS doesn't support rectangular arrays)
                // Must subtract BZERO offset before casting to signed short
                // This matches CFitsio's automatic USHORT_IMG behavior
                short[][] imageArray = new short[height][];
                
                for (int y = 0; y < height; y++)
                {
                    imageArray[y] = new short[width];
                    for (int x = 0; x < width; x++)
                    {
                        // Subtract 32768 before casting to properly store unsigned values
                        // BZERO=32768 header tells readers to add it back
                        imageArray[y][x] = unchecked((short)(flatData[y * width + x] - 32768));
                    }
                }

                // Create FITS file
                Fits fits = new Fits();
                ImageHDU hdu = (ImageHDU)Fits.MakeHDU(imageArray);
                
                // Add FITS headers
                var header = hdu.Header;
                
                // Add BZERO for unsigned 16-bit interpretation (critical for proper display)
                header.AddValue("BZERO", 32768, "Offset for unsigned integer data");
                
                // Add custom Canon-specific headers
                header.AddValue("COMMENT", "Converted from Canon RAW using NINA in-memory data", "");
                header.AddValue("COMMENT", "Full 14/16-bit dynamic range preserved", "");
                header.AddValue("COMMENT", "No demosaicing, tone curves, or processing applied", "");

                // Add NINA metadata if available
                if (metadata != null)
                {
                    AddNinaMetadataToHeader(header, metadata);
                }

                fits.AddHDU(hdu);

                // Write FITS file with retry logic (NINA may lock file for preview/star detection)
                bool written = false;
                int attempts = 0;
                int maxAttempts = 5;
                
                while (!written && attempts < maxAttempts)
                {
                    attempts++;
                    try
                    {
                        // BufferedFile needs ReadWrite access for internal BinaryReader
                        using (BufferedFile bf = new BufferedFile(outputPath, FileAccess.ReadWrite, FileShare.None))
                        {
                            fits.Write(bf);
                            bf.Flush();
                        }
                        written = true;
                        if (attempts > 1)
                        {
                            Logger.Info($"  FITS file written successfully after {attempts} attempts");
                        }
                    }
                    catch (IOException) when (attempts < maxAttempts)
                    {
                        Logger.Warning($"  FITS file locked on attempt {attempts}/{maxAttempts}, retrying in 500ms...");
                        System.Threading.Thread.Sleep(500);
                    }
                }
                
                if (!written)
                {
                    throw new IOException($"Failed to write FITS file after {maxAttempts} attempts - file locked by NINA");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating FITS from NINA data: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Add NINA metadata to FITS header - matches NINA's standard FITS output
        /// Comprehensive implementation to support all current and future NINA metadata
        /// </summary>
        private void AddNinaMetadataToHeader(Header header, ImageMetaData metadata)
        {
            try
            {
                // Image metadata
                if (metadata.Image?.RecordedRMS != null)
                {
                    header.AddValue("RMS", metadata.Image.RecordedRMS.Total, "RMS error");
                    header.AddValue("RMSRA", metadata.Image.RecordedRMS.RA, "RMS error RA");
                    header.AddValue("RMSDEC", metadata.Image.RecordedRMS.Dec, "RMS error Dec");
                }

                // Note: StarCount, HFR, HFRStDev, MedianHFR, ExposureNumber may be added in future NINA versions
                
                // Image metadata
                if (metadata.Image?.ImageType != null)
                    header.AddValue("IMAGETYP", metadata.Image.ImageType, "Type of exposure");

                if (metadata.Image?.ExposureTime > 0)
                {
                    header.AddValue("EXPOSURE", metadata.Image.ExposureTime, "[s] Exposure duration");
                    header.AddValue("EXPTIME", metadata.Image.ExposureTime, "[s] Exposure duration");
                }

                if (metadata.Image?.ExposureStart != DateTime.MinValue)
                {
                    var exposureStart = metadata.Image.ExposureStart;
                    var exposureEnd = exposureStart.AddSeconds(metadata.Image.ExposureTime);
                    var exposureMid = exposureStart.AddSeconds(metadata.Image.ExposureTime / 2.0);
                    
                    header.AddValue("DATE-LOC", exposureStart.ToString("yyyy-MM-ddTHH:mm:ss.fffffff"), "Time of observation (local)");
                    header.AddValue("DATE-OBS", exposureStart.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffff"), "Time of observation (UTC)");
                    
                    // Modified Julian Date
                    double mjdObs = exposureStart.ToUniversalTime().ToOADate() + 15018.0;
                    double mjdAvg = exposureMid.ToUniversalTime().ToOADate() + 15018.0;
                    header.AddValue("MJD-OBS", mjdObs, "Modified Julian Date of observation");
                    header.AddValue("DATE-AVG", exposureMid.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffff"), "Averaged midpoint time (UTC)");
                    header.AddValue("MJD-AVG", mjdAvg, "Modified Julian Date of averaged midpoint");
                }

                // Camera metadata
                if (metadata.Camera?.BinX > 0)
                    header.AddValue("XBINNING", metadata.Camera.BinX, "X axis binning factor");

                if (metadata.Camera?.BinY > 0)
                    header.AddValue("YBINNING", metadata.Camera.BinY, "Y axis binning factor");

                if (metadata.Camera?.Gain >= 0)
                    header.AddValue("GAIN", metadata.Camera.Gain, "Sensor gain");

                if (metadata.Camera?.Offset >= 0)
                    header.AddValue("OFFSET", metadata.Camera.Offset, "Sensor gain offset");

                if (metadata.Camera?.PixelSize > 0)
                {
                    header.AddValue("XPIXSZ", metadata.Camera.PixelSize, "[um] Pixel X axis size");
                    header.AddValue("YPIXSZ", metadata.Camera.PixelSize, "[um] Pixel Y axis size");
                }

                if (!string.IsNullOrEmpty(metadata.Camera?.Name))
                    header.AddValue("INSTRUME", metadata.Camera.Name, "Imaging instrument name");

                if (!string.IsNullOrEmpty(metadata.Camera?.Id))
                    header.AddValue("CAMERAID", metadata.Camera.Id, "Imaging instrument identifier");

                if (metadata.Camera?.Temperature > -273)
                    header.AddValue("CCD-TEMP", metadata.Camera.Temperature, "[degC] CCD temperature");

                if (metadata.Camera?.SetPoint > -273)
                    header.AddValue("SET-TEMP", metadata.Camera.SetPoint, "[degC] CCD temperature setpoint");

                if (!string.IsNullOrEmpty(metadata.Camera?.ReadoutModeName))
                    header.AddValue("READOUTM", metadata.Camera.ReadoutModeName, "Sensor readout mode");

                if (metadata.Camera?.USBLimit >= 0)
                    header.AddValue("USBLIMIT", metadata.Camera.USBLimit, "Camera-specific USB setting");

                if (metadata.Camera?.BayerOffsetX >= 0)
                    header.AddValue("XBAYROFF", metadata.Camera.BayerOffsetX, "Bayer pattern X offset");

                if (metadata.Camera?.BayerOffsetY >= 0)
                    header.AddValue("YBAYROFF", metadata.Camera.BayerOffsetY, "Bayer pattern Y offset");

                if (metadata.Camera?.BayerPattern != null)
                    header.AddValue("BAYERPAT", metadata.Camera.BayerPattern.ToString(), "Bayer color pattern");

                if (metadata.Camera?.ElectronsPerADU > 0)
                    header.AddValue("EGAIN", metadata.Camera.ElectronsPerADU, "[e-/ADU] Electrons per ADU");

                // Note: SensorName, SensorType may be added in future NINA versions

                // Telescope metadata
                if (!string.IsNullOrEmpty(metadata.Telescope?.Name))
                    header.AddValue("TELESCOP", metadata.Telescope.Name, "Name of telescope");

                if (metadata.Telescope?.FocalLength > 0)
                    header.AddValue("FOCALLEN", metadata.Telescope.FocalLength, "[mm] Focal length");

                if (metadata.Telescope?.FocalRatio > 0)
                    header.AddValue("FOCRATIO", metadata.Telescope.FocalRatio, "Focal ratio");

                if (metadata.Telescope?.Coordinates != null)
                {
                    header.AddValue("RA", metadata.Telescope.Coordinates.RA, "[deg] RA of telescope");
                    header.AddValue("DEC", metadata.Telescope.Coordinates.Dec, "[deg] Declination of telescope");
                }

                if (metadata.Telescope?.Altitude >= 0)
                    header.AddValue("CENTALT", metadata.Telescope.Altitude, "[deg] Altitude of telescope");

                if (metadata.Telescope?.Azimuth >= 0)
                    header.AddValue("CENTAZ", metadata.Telescope.Azimuth, "[deg] Azimuth of telescope");

                if (metadata.Telescope?.Airmass > 0)
                    header.AddValue("AIRMASS", metadata.Telescope.Airmass, "Airmass at frame center (Gueymard 1993)");

                if (metadata.Telescope?.SideOfPier != null)
                    header.AddValue("PIERSIDE", metadata.Telescope.SideOfPier.ToString(), "Telescope pointing state");

                // Note: TrackingMode, TrackingRate may be added in future NINA versions

                // Observer location
                if (metadata.Observer?.Elevation > -500)
                    header.AddValue("SITEELEV", metadata.Observer.Elevation, "[m] Observation site elevation");

                if (metadata.Observer?.Latitude >= -90 && metadata.Observer?.Latitude <= 90)
                    header.AddValue("SITELAT", metadata.Observer.Latitude, "[deg] Observation site latitude");

                if (metadata.Observer?.Longitude >= -180 && metadata.Observer?.Longitude <= 180)
                    header.AddValue("SITELONG", metadata.Observer.Longitude, "[deg] Observation site longitude");

                // Filter wheel metadata
                if (!string.IsNullOrEmpty(metadata.FilterWheel?.Name))
                    header.AddValue("FWHEEL", metadata.FilterWheel.Name, "Filter Wheel name");

                if (!string.IsNullOrEmpty(metadata.FilterWheel?.Filter))
                    header.AddValue("FILTER", metadata.FilterWheel.Filter, "Active filter name");

                // Target metadata
                if (!string.IsNullOrEmpty(metadata.Target?.Name))
                    header.AddValue("OBJECT", metadata.Target.Name, "Name of the object of interest");

                if (metadata.Target?.Coordinates != null)
                {
                    // Add OBJCTRA and OBJCTDEC in sexagesimal format if available
                    var raString = metadata.Target.Coordinates.RAString;
                    if (!string.IsNullOrEmpty(raString))
                        header.AddValue("OBJCTRA", raString, "[H M S] RA of imaged object");
                    
                    var decString = metadata.Target.Coordinates.DecString;
                    if (!string.IsNullOrEmpty(decString))
                        header.AddValue("OBJCTDEC", decString, "[D M S] Declination of imaged object");
                }

                if (metadata.Target?.PositionAngle >= 0)
                    header.AddValue("OBJCTROT", metadata.Target.PositionAngle, "[deg] planned rotation of imaged object");

                // Focuser metadata
                if (!string.IsNullOrEmpty(metadata.Focuser?.Name))
                    header.AddValue("FOCNAME", metadata.Focuser.Name, "Focusing equipment name");

                if (metadata.Focuser?.Position.HasValue == true)
                {
                    if (metadata.Focuser.Position.Value >= 0)
                    {
                        header.AddValue("FOCPOS", (double)metadata.Focuser.Position.Value, "[step] Focuser position");
                        header.AddValue("FOCUSPOS", (double)metadata.Focuser.Position.Value, "[step] Focuser position");
                    }
                }

                if (metadata.Focuser?.StepSize > 0)
                    header.AddValue("FOCUSSZ", metadata.Focuser.StepSize, "[um] Focuser step size");

                if (metadata.Focuser?.Temperature > -273)
                {
                    header.AddValue("FOCTEMP", metadata.Focuser.Temperature, "[degC] Focuser temperature");
                    header.AddValue("FOCUSTEM", metadata.Focuser.Temperature, "[degC] Focuser temperature");
                }

                // Rotator metadata
                if (!string.IsNullOrEmpty(metadata.Rotator?.Name))
                    header.AddValue("ROTNAME", metadata.Rotator.Name, "Rotator equipment name");

                if (metadata.Rotator?.MechanicalPosition >= 0)
                {
                    header.AddValue("ROTATOR", metadata.Rotator.MechanicalPosition, "[deg] Mechanical rotator angle");
                    header.AddValue("ROTATANG", metadata.Rotator.MechanicalPosition, "[deg] Mechanical rotator angle");
                }

                if (metadata.Rotator?.StepSize > 0)
                    header.AddValue("ROTSTPSZ", metadata.Rotator.StepSize, "[deg] Rotator step size");

                // Weather metadata
                if (metadata.WeatherData?.CloudCover >= 0)
                    header.AddValue("CLOUDCVR", metadata.WeatherData.CloudCover, "[percent] Cloud cover");

                if (metadata.WeatherData?.DewPoint > -273)
                    header.AddValue("DEWPOINT", metadata.WeatherData.DewPoint, "[degC] Dew point");

                if (metadata.WeatherData?.Humidity >= 0)
                    header.AddValue("HUMIDITY", metadata.WeatherData.Humidity, "[percent] Relative humidity");

                if (metadata.WeatherData?.Pressure > 0)
                    header.AddValue("PRESSURE", metadata.WeatherData.Pressure, "[hPa] Air pressure");

                if (metadata.WeatherData?.Temperature > -273)
                    header.AddValue("AMBTEMP", metadata.WeatherData.Temperature, "[degC] Ambient air temperature");

                if (metadata.WeatherData?.WindDirection >= 0)
                    header.AddValue("WINDDIR", metadata.WeatherData.WindDirection, "[deg] Wind direction: 0=N, 180=S, 90=E, 270=W");

                if (metadata.WeatherData?.WindGust >= 0)
                    header.AddValue("WINDGUST", metadata.WeatherData.WindGust, "[kph] Wind gust");

                if (metadata.WeatherData?.WindSpeed >= 0)
                    header.AddValue("WINDSPD", metadata.WeatherData.WindSpeed, "[kph] Wind speed");

                if (metadata.WeatherData?.SkyQuality >= 0)
                    header.AddValue("SKYQLTY", metadata.WeatherData.SkyQuality, "[mag/arcsec^2] Sky quality");

                if (metadata.WeatherData?.SkyBrightness >= 0)
                    header.AddValue("SKYBRGHT", metadata.WeatherData.SkyBrightness, "[lux] Sky brightness");

                if (metadata.WeatherData?.StarFWHM >= 0)
                    header.AddValue("STARFWHM", metadata.WeatherData.StarFWHM, "[arcsec] Star FWHM from weather station");

                // Note: The following metadata categories may be added in future NINA versions:
                // - Guider (Name, RMSError, PixelScale)
                // - FlatDevice (Name, Brightness) 
                // - SafetyMonitor (Name, IsSafe)
                // - Switch (Name)
                // - Dome (Name, Azimuth, ShutterStatus)
                // - WorldCoordinateSystem/WCS (Coordinates, Rotation, PixelScale for plate solving)

                // Standard FITS keywords
                header.AddValue("ROWORDER", "TOP-DOWN", "FITS Image Orientation");
                header.AddValue("EQUINOX", 2000.0, "Equinox of celestial coordinate system");
                
                // Software version
                var ninaVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
                if (ninaVersion != null)
                    header.AddValue("SWCREATE", $"N.I.N.A. {ninaVersion} (x64)", "Software that created this file");
            }
            catch (Exception ex)
            {
                Logger.Warning($"Error adding metadata to FITS header: {ex.Message}");
            }
        }

        /// <summary>
        /// Create FITS file using CFitsio library with compression support
        /// </summary>
        private void CreateFitsFileWithCfitsio(NINA.Image.Interfaces.IImageArray rawDataArray, int width, int height, string outputPath, ImageMetaData metadata, int compressionType)
        {
            var cfitsioWriter = new CFitsioWriter();
            cfitsioWriter.WriteFitsFile(rawDataArray, width, height, outputPath, metadata, compressionType);
        }
    }
}

