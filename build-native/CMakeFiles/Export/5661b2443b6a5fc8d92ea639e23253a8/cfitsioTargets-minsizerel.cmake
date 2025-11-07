#----------------------------------------------------------------
# Generated CMake target import file for configuration "MinSizeRel".
#----------------------------------------------------------------

# Commands may need to know the format version.
set(CMAKE_IMPORT_FILE_VERSION 1)

# Import target "CFITSIO::cfitsio" for configuration "MinSizeRel"
set_property(TARGET CFITSIO::cfitsio APPEND PROPERTY IMPORTED_CONFIGURATIONS MINSIZEREL)
set_target_properties(CFITSIO::cfitsio PROPERTIES
  IMPORTED_IMPLIB_MINSIZEREL "${_IMPORT_PREFIX}/lib/cfitsio.lib"
  IMPORTED_LOCATION_MINSIZEREL "${_IMPORT_PREFIX}/bin/cfitsio.dll"
  )

list(APPEND _cmake_import_check_targets CFITSIO::cfitsio )
list(APPEND _cmake_import_check_files_for_CFITSIO::cfitsio "${_IMPORT_PREFIX}/lib/cfitsio.lib" "${_IMPORT_PREFIX}/bin/cfitsio.dll" )

# Commands beyond this point should not need to know the version.
set(CMAKE_IMPORT_FILE_VERSION)
