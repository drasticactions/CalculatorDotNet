cmake_minimum_required(VERSION 3.20)
project(CalcManagerShim CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE Release)
endif()

set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

get_filename_component(CALCMANAGER_DIR
    "${CMAKE_CURRENT_SOURCE_DIR}/../../external/calculator/src/CalcManager" ABSOLUTE)

file(GLOB CENGINE_SOURCES "${CALCMANAGER_DIR}/CEngine/*.cpp")
file(GLOB RATPACK_SOURCES "${CALCMANAGER_DIR}/Ratpack/*.cpp")

option(CALCSHIM_STATIC "Build a static archive instead of a shared library" OFF)

if(CALCSHIM_STATIC)
    set(CALCSHIM_LIBRARY_KIND STATIC)
else()
    set(CALCSHIM_LIBRARY_KIND SHARED)
endif()

add_library(CalcManagerShim ${CALCSHIM_LIBRARY_KIND}
    CalcManagerShim.cpp
    UnitConverterShim.cpp
    "${CALCMANAGER_DIR}/UnitConverter.cpp"
    "${CALCMANAGER_DIR}/CalculatorManager.cpp"
    "${CALCMANAGER_DIR}/CalculatorHistory.cpp"
    "${CALCMANAGER_DIR}/ExpressionCommand.cpp"
    "${CALCMANAGER_DIR}/NumberFormattingUtils.cpp"
    ${CENGINE_SOURCES}
    ${RATPACK_SOURCES})

target_include_directories(CalcManagerShim PRIVATE "${CALCMANAGER_DIR}")

if(NOT MSVC)
    target_include_directories(CalcManagerShim PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/compat")
endif()

if(MSVC)
    target_compile_options(CalcManagerShim PRIVATE /FI"${CMAKE_CURRENT_SOURCE_DIR}/StdlibPrefix.h")
else()
    target_compile_options(CalcManagerShim PRIVATE -include "${CMAKE_CURRENT_SOURCE_DIR}/StdlibPrefix.h")
endif()

set_target_properties(CalcManagerShim PROPERTIES
    CXX_VISIBILITY_PRESET hidden
    VISIBILITY_INLINES_HIDDEN ON
    POSITION_INDEPENDENT_CODE ON)

if(CALCSHIM_STATIC)
    set_target_properties(CalcManagerShim PROPERTIES PREFIX "")
endif()

if(MSVC)
    # Static CRT so the DLL loads without the VC++ redistributable installed.
    set_property(TARGET CalcManagerShim PROPERTY
        MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
endif()

if(ANDROID)
    target_link_options(CalcManagerShim PRIVATE "-Wl,-z,max-page-size=16384")
endif()

if(EMSCRIPTEN)
    option(CALCSHIM_WASM_LEGACY_EH "Use JS-based exceptions (-fexceptions) instead of the wasm EH proposal" OFF)

    if(CALCSHIM_WASM_LEGACY_EH)
        target_compile_options(CalcManagerShim PRIVATE -fexceptions)
    else()
        target_compile_options(CalcManagerShim PRIVATE -fwasm-exceptions)
    endif()
endif()

if(APPLE AND NOT CALCSHIM_STATIC)
    set_target_properties(CalcManagerShim PROPERTIES
        BUILD_WITH_INSTALL_NAME_DIR TRUE
        INSTALL_NAME_DIR "@rpath")
endif()
