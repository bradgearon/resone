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
foreach(target Resone-app Resone-vst3)
 target_include_directories(${target} PRIVATE ../../native/include ../../native/vendor)
 # iPlug2/WDL and RtAudio/ASIO depend on the shell, common-dialog and
 # COM declarations supplied by the full windows.h include set. Defining
 # WIN32_LEAN_AND_MEAN here hides HDROP, OPENFILENAME, IUnknown, and Co* APIs
 # across the upstream sources compiled into both targets.
 target_compile_definitions(${target} PRIVATE NOMINMAX)
 target_link_libraries(${target} PRIVATE winmm shell32 ole32 comdlg32 uuid)
endforeach()
