#----------------------------------------------------------------
# Generated CMake target import file for configuration "Debug".
#----------------------------------------------------------------

# Commands may need to know the format version.
set(CMAKE_IMPORT_FILE_VERSION 1)

# Import target "CFITSIO::cfitsio" for configuration "Debug"
set_property(TARGET CFITSIO::cfitsio APPEND PROPERTY IMPORTED_CONFIGURATIONS DEBUG)
set_target_properties(CFITSIO::cfitsio PROPERTIES
  IMPORTED_IMPLIB_DEBUG "${_IMPORT_PREFIX}/lib/libcfitsio.dll.a"
  IMPORTED_LOCATION_DEBUG "${_IMPORT_PREFIX}/bin/libcfitsio.dll"
  )

list(APPEND _IMPORT_CHECK_TARGETS CFITSIO::cfitsio )
list(APPEND _IMPORT_CHECK_FILES_FOR_CFITSIO::cfitsio "${_IMPORT_PREFIX}/lib/libcfitsio.dll.a" "${_IMPORT_PREFIX}/bin/libcfitsio.dll" )

# Commands beyond this point should not need to know the version.
set(CMAKE_IMPORT_FILE_VERSION)
