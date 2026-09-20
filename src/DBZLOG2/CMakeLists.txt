cmake_minimum_required(VERSION 3.16)
project(DBZLOG2 CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Every opcode / effect lives in its own .cpp; pick them all up.
file(GLOB_RECURSE DBZLOG2_SOURCES CONFIGURE_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/src/*.cpp")

add_library(dbzlog2 STATIC ${DBZLOG2_SOURCES})
target_include_directories(dbzlog2 PUBLIC "${CMAKE_CURRENT_SOURCE_DIR}/include")

# The hardware layer (gba_io.h) is a stub on the host; define DBZLOG2_TARGET_GBA to use real MMIO addresses.
option(DBZLOG2_TARGET_GBA "Build against the real GBA memory map" OFF)
if(DBZLOG2_TARGET_GBA)
  target_compile_definitions(dbzlog2 PUBLIC DBZLOG2_TARGET_GBA=1)
endif()
