cmake_minimum_required(VERSION 3.25)
project(resone_llama_bridge LANGUAGES C CXX)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
add_library(resone_llama_bridge SHARED resone_llama_bridge.cpp)
target_include_directories(resone_llama_bridge PRIVATE
 .
 ../vendor)
target_compile_definitions(resone_llama_bridge PRIVATE NOMINMAX)
target_link_libraries(resone_llama_bridge PRIVATE ${CMAKE_DL_LIBS})
install(TARGETS resone_llama_bridge RUNTIME DESTINATION . LIBRARY DESTINATION .)
