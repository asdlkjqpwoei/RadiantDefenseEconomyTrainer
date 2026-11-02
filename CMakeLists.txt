cmake_minimum_required(VERSION 4.2)

if(NOT WIN32)
  message(FATAL_ERROR "Radiant Defense Economy Trainer requires Windows.")
endif()

if(NOT CMAKE_GENERATOR STREQUAL "Visual Studio 18 2026")
  message(FATAL_ERROR
    "Use the Visual Studio 2026 generator. Run `cmake --preset vs2026` from the project directory.")
endif()

if(NOT CMAKE_GENERATOR_PLATFORM STREQUAL "x64")
  message(FATAL_ERROR "Configure this project for the x64 platform.")
endif()

find_path(RADIANTDEFENSETRAINER_NET48_REFERENCE_DIR
  NAMES mscorlib.dll
  HINTS
    ENV "ProgramFiles(x86)"
    ENV ProgramFiles
  PATH_SUFFIXES "Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8"
  NO_DEFAULT_PATH
)

if(NOT RADIANTDEFENSETRAINER_NET48_REFERENCE_DIR)
  message(FATAL_ERROR
    "The .NET Framework 4.8 Developer Pack is missing. Install it, then reconfigure.")
endif()

foreach(reference IN ITEMS
    mscorlib.dll
    System.dll
    System.Core.dll
    System.Drawing.dll
    System.Windows.Forms.dll)
  if(NOT EXISTS "${RADIANTDEFENSETRAINER_NET48_REFERENCE_DIR}/${reference}")
    message(FATAL_ERROR
      "The .NET Framework 4.8 reference pack is incomplete: ${reference} is missing from ${RADIANTDEFENSETRAINER_NET48_REFERENCE_DIR}.")
  endif()
endforeach()

set(RADIANTDEFENSETRAINER_TEXT_CONFIG "${CMAKE_CURRENT_SOURCE_DIR}/config/trainer.yaml")
if(NOT EXISTS "${RADIANTDEFENSETRAINER_TEXT_CONFIG}")
  message(FATAL_ERROR "The external UI and localization file config/trainer.yaml is missing.")
endif()

set_source_files_properties(config/trainer.yaml PROPERTIES
  VS_TOOL_OVERRIDE "Content"
  VS_CSHARP_Link "trainer.yaml"
  VS_CSHARP_CopyToOutputDirectory "PreserveNewest"
)
set(CMAKE_CONFIGURATION_TYPES "Debug;Release" CACHE STRING "Supported build configurations" FORCE)
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/bin")

project(RadiantDefenseEconomyTrainer LANGUAGES CSharp)
include(CTest)

if(NOT CMAKE_CSharp_COMPILER OR NOT EXISTS "${CMAKE_CSharp_COMPILER}")
  message(FATAL_ERROR
    "The Visual Studio C# compiler was not found. Install the .NET desktop development workload.")
endif()

set(RADIANTDEFENSETRAINER_DOTNET_REFERENCES
  "System;System.Core;System.Drawing;System.Windows.Forms"
)

function(configure_radiantdefensetrainer_csharp_target target)
  target_sources(${target} PRIVATE app.config config/trainer.yaml)
  set_target_properties(${target} PROPERTIES
    VS_DOTNET_TARGET_FRAMEWORK_VERSION "v4.8"
    VS_DOTNET_REFERENCES "${RADIANTDEFENSETRAINER_DOTNET_REFERENCES}"
    VS_GLOBAL_CodePage "65001"
    VS_GLOBAL_AppConfig "${CMAKE_CURRENT_SOURCE_DIR}/app.config"
  )
endfunction()

add_executable(RadiantDefenseEconomyTrainer WIN32
  src/Program.cs
  src/MemApi.cs
  src/RadiantDefenseMoneyLocator.cs
  src/TrainerLocalization.cs
  src/TrainerForm.cs
)
set_target_properties(RadiantDefenseEconomyTrainer PROPERTIES
  OUTPUT_NAME "Radiant Defense Economy Trainer"
  VS_DOTNET_STARTUP_OBJECT "Program"
  VS_GLOBAL_ApplicationManifest "${CMAKE_CURRENT_SOURCE_DIR}/app.manifest"
  VS_GLOBAL_OutputType "WinExe"
  VS_GLOBAL_RootNamespace "RadiantDefenseEconomyTrainer"
)
configure_radiantdefensetrainer_csharp_target(RadiantDefenseEconomyTrainer)

add_executable(TrainerSelfTest
  src/MemApi.cs
  src/RadiantDefenseMoneyLocator.cs
  src/TrainerLocalization.cs
  src/TrainerForm.cs
  test/TrainerSelfTest.cs
)
configure_radiantdefensetrainer_csharp_target(TrainerSelfTest)

if(BUILD_TESTING)
  add_test(NAME TrainerSelfTest COMMAND "$<TARGET_FILE:TrainerSelfTest>")
endif()

install(TARGETS RadiantDefenseEconomyTrainer RUNTIME DESTINATION ".")
install(FILES
  "$<TARGET_FILE:RadiantDefenseEconomyTrainer>.config"
  "$<TARGET_FILE_DIR:RadiantDefenseEconomyTrainer>/trainer.yaml"
  README.md
  DESTINATION "."
)

set(CPACK_GENERATOR "ZIP")
set(CPACK_PACKAGE_FILE_NAME "${PROJECT_NAME}-win-x64")
set(CPACK_PACKAGE_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/bin")
set(CPACK_INCLUDE_TOPLEVEL_DIRECTORY TRUE)
include(CPack)

set_property(DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
  PROPERTY VS_STARTUP_PROJECT RadiantDefenseEconomyTrainer)

message(STATUS "C# compiler: ${CMAKE_CSharp_COMPILER}")
message(STATUS ".NET Framework 4.8 references: ${RADIANTDEFENSETRAINER_NET48_REFERENCE_DIR}")
message(STATUS "Visual Studio solution generated in: ${CMAKE_BINARY_DIR}")
message(STATUS "Binaries generated in: ${CMAKE_RUNTIME_OUTPUT_DIRECTORY}/<CONFIG>")
message(STATUS "External UI text: ${RADIANTDEFENSETRAINER_TEXT_CONFIG}")
message(STATUS "Build with: cmake --build \"${CMAKE_BINARY_DIR}\" --config Release")
message(STATUS "Test with: ctest --test-dir \"${CMAKE_BINARY_DIR}\" -C Release --output-on-failure")
message(STATUS "Package with: cpack --config \"${CMAKE_BINARY_DIR}/CPackConfig.cmake\" -C Release")
