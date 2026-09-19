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



# Local speech-to-singing transform used by the Vocals lane. This is Resone's
# own compact DSP library; Qwen TTS itself is loaded dynamically at runtime.
add_library(wds_resone_vocals SHARED ../../native/vocals/resone_vocals.cpp
  ../../native/vocals/resone_vocal_phonetics.cpp
  ../../native/vocals/resone_vocal_articulation.cpp
)
target_include_directories(wds_resone_vocals PRIVATE ../../native/vocals)
target_compile_definitions(wds_resone_vocals PRIVATE RESONE_VOCALS_EXPORTS NOMINMAX)
set_target_properties(wds_resone_vocals PROPERTIES
 OUTPUT_NAME "wds.resone.vocals"
 RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/out"
 RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/out/Release"
 LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/out"
 LIBRARY_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/out/Release")

# Thin in-process llama.cpp bridge. Resone vendors only the pinned public ABI
# declarations needed to compile this loader; it never clones or builds llama.cpp.
# The actual implementation is supplied at runtime by a prebuilt engine ZIP.
add_library(resone_llama_bridge SHARED ../../native/inference/resone_llama_bridge.cpp)
target_include_directories(resone_llama_bridge PRIVATE
 ../../native/inference
 ../../native/vendor)
target_compile_definitions(resone_llama_bridge PRIVATE NOMINMAX)
target_link_libraries(resone_llama_bridge PRIVATE ${CMAKE_DL_LIBS})
set_target_properties(resone_llama_bridge PROPERTIES
 OUTPUT_NAME "resone_llama_bridge"
 RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/out"
 RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/out/Release"
 LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/out"
 LIBRARY_OUTPUT_DIRECTORY_RELEASE "${CMAKE_BINARY_DIR}/out/Release")

foreach(target Resone-app Resone-vst3)
 target_include_directories(${target} PRIVATE ../../native/include ../../native/vendor)
 # iPlug2/WDL and RtAudio/ASIO depend on the shell, common-dialog and
 # COM declarations supplied by the full windows.h include set. Defining
 # WIN32_LEAN_AND_MEAN here hides HDROP, OPENFILENAME, IUnknown, and Co* APIs
 # across the upstream sources compiled into both targets.
 target_compile_definitions(${target} PRIVATE NOMINMAX)
 target_link_libraries(${target} PRIVATE winmm shell32 ole32 comdlg32 comctl32 uuid)
endforeach()
