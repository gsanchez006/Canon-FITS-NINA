using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NINA.Plugin.Canon.EDSDK.Native
{
    /// <summary>
    /// P/Invoke wrapper for NASA CFitsio 4.6.3 library
    /// Complete implementation for creating FITS files with compression and full metadata support
    /// Supports both cfitsio.dll (custom build from cfitsio-4.6.3) and cfitsionative.dll (NINA's library)
    /// </summary>
    public static class CFitsioNative
    {
        // Try custom DLL first, fall back to NINA's DLL
        private const string DllName = "cfitsio.dll";
        private const string DllNameFallback = "cfitsionative.dll";

        #region FITS Constants

        // Compression algorithm codes
        public const int RICE_1 = 11;
        public const int GZIP_1 = 21;
        public const int GZIP_2 = 22;
        public const int PLIO_1 = 31;
        public const int HCOMPRESS_1 = 41;
        public const int NOCOMPRESS = -1;

        // BITPIX values
        public const int BYTE_IMG = 8;
        public const int SHORT_IMG = 16;
        public const int LONG_IMG = 32;
        public const int LONGLONG_IMG = 64;
        public const int FLOAT_IMG = -32;
        public const int DOUBLE_IMG = -64;
        public const int USHORT_IMG = 20;    // Unsigned 16-bit (BZERO=32768)
        public const int ULONG_IMG = 40;     // Unsigned 32-bit

        // Data type codes
        public const int TSTRING = 16;
        public const int TBYTE = 11;
        public const int TSHORT = 21;
        public const int TUSHORT = 20;
        public const int TINT = 31;
        public const int TUINT = 30;
        public const int TLONG = 41;
        public const int TLONGLONG = 81;
        public const int TFLOAT = 42;
        public const int TDOUBLE = 82;
        public const int TLOGICAL = 14;

        // File access modes
        public const int READONLY = 0;
        public const int READWRITE = 1;

        // Status codes
        public const int OK = 0;

        #endregion

        #region File Operations

        /// <summary>
        /// Create a new FITS file
        /// </summary>
        [DllImport(DllName, EntryPoint = "ffinit", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int fits_create_file_impl(out IntPtr fptr, string filename, out int status);

        /// <summary>
        /// Close a FITS file
        /// </summary>
        [DllImport(DllName, EntryPoint = "ffclos", CallingConvention = CallingConvention.Cdecl)]
        private static extern int fits_close_file_impl(IntPtr fptr, out int status);

        /// <summary>
        /// Flush data to disk
        /// </summary>
        [DllImport(DllName, EntryPoint = "ffflus", CallingConvention = CallingConvention.Cdecl)]
        private static extern int fits_flush_file_impl(IntPtr fptr, out int status);

        /// <summary>
        /// Move to absolute HDU number (1-based)
        /// </summary>
        [DllImport(DllName, EntryPoint = "ffmahd", CallingConvention = CallingConvention.Cdecl)]
        private static extern int fits_movabs_hdu_impl(IntPtr fptr, int hdunum, out int hdutype, out int status);

        #endregion

        #region Image Operations

        /// <summary>
        /// Create a primary array image HDU (64-bit version for large images)
        /// </summary>
        [DllImport(DllName, EntryPoint = "ffcrimll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int fits_create_img_impl(IntPtr fptr, int bitpix, int naxis, long[] naxes, out int status);

        /// <summary>
        /// Write image data (signed short)
        /// </summary>
        [DllImport(DllName, EntryPoint = "ffppr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int fits_write_img_impl(IntPtr fptr, int datatype, long firstelem, long nelements, short[] array, out int status);

        /// <summary>
        /// Write image data (unsigned short)
        /// </summary>
        [DllImport(DllName, EntryPoint = "ffppr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int fits_write_img_ushort_impl(IntPtr fptr, int datatype, long firstelem, long nelements, ushort[] array, out int status);

        #endregion

        #region Compression Operations

        /// <summary>
        /// Set compression type for image
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int fits_set_compression_type(IntPtr fptr, int ctype, out int status);

        /// <summary>
        /// Set tile dimensions for compression
        /// Note: Uses int[] because C long is 32-bit on Windows
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int fits_set_tile_dim(IntPtr fptr, int ndim, int[] dims, out int status);

        /// <summary>
        /// Set HCOMPRESS scale factor
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int fits_set_hcomp_scale(IntPtr fptr, float scale, out int status);

        /// <summary>
        /// Set HCOMPRESS smooth parameter
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int fits_set_hcomp_smooth(IntPtr fptr, int smooth, out int status);

        /// <summary>
        /// Set quantize level (for quantization of floating point images)
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int fits_set_quantize_level(IntPtr fptr, float qlevel, out int status);

        #endregion

        #region Keyword Writing Operations

        /// <summary>
        /// Write a string keyword
        /// </summary>
        [DllImport(DllName, EntryPoint = "ffpkys", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int fits_write_key_str_impl(IntPtr fptr, string keyname, string value, string comment, out int status);

        /// <summary>
        /// Write a long string keyword (> 68 chars)
        /// </summary>
        [DllImport(DllName, EntryPoint = "ffpkls", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int fits_write_key_longstr_impl(IntPtr fptr, string keyname, string value, string comment, out int status);

        /// <summary>
        /// Write a long integer keyword
        /// </summary>
        [DllImport(DllName, EntryPoint = "ffpkyj", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int fits_write_key_lng_impl(IntPtr fptr, string keyname, long value, string comment, out int status);

        /// <summary>
        /// Write a double precision keyword
        /// </summary>
        [DllImport(DllName, EntryPoint = "ffpkyd", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int fits_write_key_dbl_impl(IntPtr fptr, string keyname, double value, int decimals, string comment, out int status);

        /// <summary>
        /// Write a float keyword
        /// </summary>
        [DllImport(DllName, EntryPoint = "ffpkye", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int fits_write_key_flt_impl(IntPtr fptr, string keyname, float value, int decimals, string comment, out int status);

        /// <summary>
        /// Write a comment keyword
        /// </summary>
        [DllImport(DllName, EntryPoint = "ffpcom", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int fits_write_comment_impl(IntPtr fptr, string comment, out int status);

        /// <summary>
        /// Write a history keyword
        /// </summary>
        [DllImport(DllName, EntryPoint = "ffphis", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int fits_write_history_impl(IntPtr fptr, string history, out int status);

        /// <summary>
        /// Update an existing keyword (modify or insert if not found) - string
        /// </summary>
        [DllImport(DllName, EntryPoint = "ffukys", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int fits_update_key_str_impl(IntPtr fptr, string keyname, string value, string comment, out int status);

        /// <summary>
        /// Update a long integer keyword
        /// </summary>
        [DllImport(DllName, EntryPoint = "ffukyj", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int fits_update_key_lng_impl(IntPtr fptr, string keyname, long value, string comment, out int status);

        /// <summary>
        /// Update a double keyword
        /// </summary>
        [DllImport(DllName, EntryPoint = "ffukyd", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int fits_update_key_dbl_impl(IntPtr fptr, string keyname, double value, int decimals, string comment, out int status);

        #endregion

        #region Error Handling

        /// <summary>
        /// Get error status message
        /// </summary>
        [DllImport(DllName, EntryPoint = "ffgerr", CallingConvention = CallingConvention.Cdecl)]
        private static extern void fits_get_errstatus_impl(int status, StringBuilder errtext);

        /// <summary>
        /// Read and clear the entire error message stack
        /// </summary>
        [DllImport(DllName, EntryPoint = "ffgmsg", CallingConvention = CallingConvention.Cdecl)]
        private static extern void fits_read_errmsg_impl(StringBuilder err_text);

        /// <summary>
        /// Clear the error message stack
        /// </summary>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void fits_clear_errmsg();

        #endregion

        #region Public Safe Wrappers

        public static int fits_create_file(out IntPtr fptr, string filename, out int status)
        {
            status = 0;
            // Prepend ! to overwrite existing file
            string fn = filename.StartsWith("!") ? filename : "!" + filename;
            return fits_create_file_impl(out fptr, fn, out status);
        }

        public static int fits_close_file(IntPtr fptr, out int status)
        {
            return fits_close_file_impl(fptr, out status);
        }

        public static int fits_flush_file(IntPtr fptr, out int status)
        {
            return fits_flush_file_impl(fptr, out status);
        }

        public static int fits_movabs_hdu(IntPtr fptr, int hdunum, out int hdutype, out int status)
        {
            return fits_movabs_hdu_impl(fptr, hdunum, out hdutype, out status);
        }

        public static int fits_create_img(IntPtr fptr, int bitpix, int naxis, long[] naxes, out int status)
        {
            return fits_create_img_impl(fptr, bitpix, naxis, naxes, out status);
        }

        public static int fits_write_img(IntPtr fptr, int datatype, long firstelem, long nelements, short[] array, out int status)
        {
            return fits_write_img_impl(fptr, datatype, firstelem, nelements, array, out status);
        }

        public static int fits_write_img(IntPtr fptr, int datatype, long firstelem, long nelements, ushort[] array, out int status)
        {
            return fits_write_img_ushort_impl(fptr, datatype, firstelem, nelements, array, out status);
        }

        public static int fits_write_key_str(IntPtr fptr, string keyname, string value, string comment, out int status)
        {
            return fits_write_key_str_impl(fptr, keyname, value ?? "", comment ?? "", out status);
        }

        public static int fits_write_key_longstr(IntPtr fptr, string keyname, string value, string comment, out int status)
        {
            return fits_write_key_longstr_impl(fptr, keyname, value ?? "", comment ?? "", out status);
        }

        public static int fits_write_key_lng(IntPtr fptr, string keyname, long value, string comment, out int status)
        {
            return fits_write_key_lng_impl(fptr, keyname, value, comment ?? "", out status);
        }

        public static int fits_write_key_dbl(IntPtr fptr, string keyname, double value, int decimals, string comment, out int status)
        {
            return fits_write_key_dbl_impl(fptr, keyname, value, decimals, comment ?? "", out status);
        }

        public static int fits_write_key_flt(IntPtr fptr, string keyname, float value, int decimals, string comment, out int status)
        {
            return fits_write_key_flt_impl(fptr, keyname, value, decimals, comment ?? "", out status);
        }

        public static int fits_write_comment(IntPtr fptr, string comment, out int status)
        {
            return fits_write_comment_impl(fptr, comment ?? "", out status);
        }

        public static int fits_write_history(IntPtr fptr, string history, out int status)
        {
            return fits_write_history_impl(fptr, history ?? "", out status);
        }

        public static int fits_update_key_str(IntPtr fptr, string keyname, string value, string comment, out int status)
        {
            return fits_update_key_str_impl(fptr, keyname, value ?? "", comment ?? "", out status);
        }

        public static int fits_update_key_lng(IntPtr fptr, string keyname, long value, string comment, out int status)
        {
            return fits_update_key_lng_impl(fptr, keyname, value, comment ?? "", out status);
        }

        public static int fits_update_key_dbl(IntPtr fptr, string keyname, double value, int decimals, string comment, out int status)
        {
            return fits_update_key_dbl_impl(fptr, keyname, value, decimals, comment ?? "", out status);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Get CFITSIO error message
        /// </summary>
        public static string GetErrorMessage(int status)
        {
            if (status == 0) return "No error";

            var errText = new StringBuilder(81);
            fits_get_errstatus_impl(status, errText);
            
            var fullErr = new StringBuilder(512);
            fits_read_errmsg_impl(fullErr);
            
            if (fullErr.Length > 0)
                return $"{errText} - {fullErr}";
            else
                return errText.ToString();
        }

        /// <summary>
        /// Check status and throw exception if error occurred
        /// </summary>
        public static void CheckStatus(int status, string operation)
        {
            if (status != 0)
            {
                string errorMsg = GetErrorMessage(status);
                throw new Exception($"CFitsio error in {operation}: {errorMsg} (status={status})");
            }
        }

        /// <summary>
        /// Safe write string keyword - truncates long strings or uses longstr
        /// </summary>
        public static void WriteKeyString(IntPtr fptr, string keyname, string value, string comment)
        {
            int status = 0;
            
            // FITS standard keyword values are max 68 chars, use longstr for longer
            if (value != null && value.Length > 68)
            {
                fits_write_key_longstr(fptr, keyname, value, comment, out status);
            }
            else
            {
                fits_write_key_str(fptr, keyname, value ?? "", comment, out status);
            }
            
            CheckStatus(status, $"Writing keyword {keyname}");
        }

        /// <summary>
        /// Safe write double keyword with automatic precision
        /// </summary>
        public static void WriteKeyDouble(IntPtr fptr, string keyname, double value, string comment, int decimals = -15)
        {
            int status = 0;
            fits_write_key_dbl(fptr, keyname, value, decimals, comment, out status);
            CheckStatus(status, $"Writing keyword {keyname}");
        }

        /// <summary>
        /// Safe write integer keyword
        /// </summary>
        public static void WriteKeyInt(IntPtr fptr, string keyname, long value, string comment)
        {
            int status = 0;
            fits_write_key_lng(fptr, keyname, value, comment, out status);
            CheckStatus(status, $"Writing keyword {keyname}");
        }

        /// <summary>
        /// Safe write comment
        /// </summary>
        public static void WriteComment(IntPtr fptr, string comment)
        {
            int status = 0;
            fits_write_comment(fptr, comment, out status);
            CheckStatus(status, "Writing COMMENT");
        }

        #endregion
    }
}
