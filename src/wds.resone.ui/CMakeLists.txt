cmake_minimum_required(VERSION 3.25)
project(Resone VERSION 0.1.0 LANGUAGES C CXX)
if(NOT WIN32)
 message(FATAL_ERROR "The first Resone editor targets Windows x64. The audio core tests are portable.")
endif()
set(IPLUG2_CXX_STANDARD 20 CACHE STRING "Resone requires C++20 atomic wait")
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
if(NOT DEFINED IPLUG2_DIR)
 set(IPLUG2_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../third_party/iPlug2")
endif()
include(${IPLUG2_DIR}/iPlug2.cmake)
find_package(iPlug2 REQUIRED)
iplug_add_plugin(Resone
 SOURCES Resone.cpp Resone.h ../../native/src/AudioEngine.cpp resources/resource.h
 WEB_RESOURCES resources/web/index.html resources/web/style.css resources/web/app.js resources/web/logo.png
 UI WEBVIEW FORMATS APP VST3)

# iPlug2's CMake APP target automatically compiles resources/main.rc on Windows,
# but its VST3 target does not. Explicitly attach the same resource script to
# the VST3 module so Windows can see Resone.ico (IDI_ICON1) in the actual plug-in.
if(WIN32)
 set(RESONE_WIN_RESOURCE "${CMAKE_CURRENT_SOURCE_DIR}/resources/main.rc")
 target_sources(Resone-vst3 PRIVATE "${RESONE_WIN_RESOURCE}")
 set_source_files_properties("${RESONE_WIN_RESOURCE}" PROPERTIES
  COMPILE_FLAGS "/I\"${CMAKE_CURRENT_SOURCE_DIR}/resources/fonts\" /I\"${CMAKE_CURRENT_SOURCE_DIR}/resources/img\" /I\"${CMAKE_CURRENT_SOURCE_DIR}/resources\"")
endif()

foreach(target Resone-app Resone-vst3)
 target_include_directories(${target} PRIVATE ../../native/include ../../native/vendor)
 # iPlug2/WDL and RtAudio/ASIO depend on the shell, common-dialog and
 # COM declarations supplied by the full windows.h include set. Defining
 # WIN32_LEAN_AND_MEAN here hides HDROP, OPENFILENAME, IUnknown, and Co* APIs
 # across the upstream sources compiled into both targets.
 target_compile_definitions(${target} PRIVATE NOMINMAX)
 target_link_libraries(${target} PRIVATE winmm shell32 ole32 comdlg32 uuid)
endforeach()
