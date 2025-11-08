using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Canon RAW to FITS Converter")]
[assembly: AssemblyDescription("A NINA plugin that automatically converts all Canon RAW images (CR2, CR3, CRW, and newer formats) to FITS format using NINA's in-memory image data. Preserves full 14/16-bit dynamic range without demosaicing or processing.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Gus Sanchez")]
[assembly: AssemblyProduct("Canon RAW to FITS Converter")]
[assembly: AssemblyCopyright("Copyright © 2025 Gus Sanchez")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("a5b4c6d7-e8f9-4a5b-9c8d-7e6f5a4b3c2d")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
[assembly: AssemblyVersion("1.0.6.0")]
[assembly: AssemblyFileVersion("1.0.6.0")]

// Plugin specific metadata
[assembly: AssemblyMetadata("MinimumApplicationVersion", "3.0.0.0")]
[assembly: AssemblyMetadata("License", "MPL-2.0")]
[assembly: AssemblyMetadata("LicenseURL", "https://www.mozilla.org/en-US/MPL/2.0/")]
[assembly: AssemblyMetadata("Repository", "https://github.com/gsanchez006/Canon-FITS-NINA")]
[assembly: AssemblyMetadata("FeaturedImageURL", "")]
[assembly: AssemblyMetadata("ScreenshotURL", "")]
[assembly: AssemblyMetadata("AltScreenshotURL", "")]
[assembly: AssemblyMetadata("Homepage", "")]
[assembly: AssemblyMetadata("ChangelogURL", "")]
[assembly: AssemblyMetadata("Tags", "Canon,FITS,CR2,CR3,CRW,RAW,Converter,In-Memory")]
[assembly: AssemblyMetadata("LongDescription", @"This plugin automatically converts all Canon RAW images (CR2, CR3, CRW, and newer formats) to FITS format using NINA's in-memory image data during capture.

Features:
- Converts RAW to FITS in real-time during image capture using NINA's in-memory data
- Preserves full 14/16-bit dynamic range without demosaicing, tone curves, or processing
- FITS files saved automatically to the same directory as RAW files
- Complete FITS header preservation from NINA's imaging metadata (exposure, temperature, coordinates, etc.)
- Optional RAW file deletion after successful conversion
- Support for all Canon RAW formats (CR2, CR3, CRW, and newer formats as they are released)
- Optimized for burst mode imaging (handles rapid-fire 0.00025s exposures)
- File locking retry logic to handle NINA's temporary file locks during preview/analysis

The plugin hooks into NINA's BeforeImageSaved event to access raw image data directly from memory, bypassing the limitations of disk-based RAW file processing. This approach ensures compatibility with all Canon RAW formats, both current and future, without requiring Canon EDSDK or external libraries for RAW processing.")]
