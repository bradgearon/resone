cmake_minimum_required(VERSION 3.24)
project(resone_voice_native LANGUAGES CXX)

# Deliberately compile the SAME vocal renderer used by Resone. There is no copied
# DSP source in this test project: edits to native/vocals/resone_vocals.cpp are
# picked up by the next build immediately.
add_library(wds_resone_vocals SHARED
  ../../../native/vocals/resone_vocals.cpp
  ../../../native/vocals/resone_vocal_phonetics.cpp
  ../../../native/vocals/resone_vocal_articulation.cpp
)
target_include_directories(wds_resone_vocals PRIVATE
  ../../../native/vocals
)
target_compile_features(wds_resone_vocals PRIVATE cxx_std_20)
target_compile_definitions(wds_resone_vocals PRIVATE RESONE_VOCALS_EXPORTS NOMINMAX)
set_target_properties(wds_resone_vocals PROPERTIES
  OUTPUT_NAME "wds.resone.vocals"
)
