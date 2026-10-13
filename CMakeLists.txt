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
  PATHS
    "C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8"
    "C:/Program Files/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8"
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

set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${RADIANTDEFENSETRAINER_TEXT_CONFIG}")
set_source_files_properties(config/trainer.yaml PROPERTIES HEADER_FILE_ONLY TRUE)
foreach(configuration IN ITEMS Debug Release MinSizeRel RelWithDebInfo)
  file(MAKE_DIRECTORY "${CMAKE_BINARY_DIR}/${configuration}")
  configure_file(
    "${RADIANTDEFENSETRAINER_TEXT_CONFIG}"
    "${CMAKE_BINARY_DIR}/${configuration}/trainer.yaml"
    COPYONLY
  )
endforeach()

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
  set_target_properties(${target} PROPERTIES
    VS_DOTNET_TARGET_FRAMEWORK_VERSION "v4.8"
    VS_DOTNET_REFERENCES "${RADIANTDEFENSETRAINER_DOTNET_REFERENCES}"
    VS_GLOBAL_CodePage "65001"
  )
endfunction()

add_executable(RadiantDefenseEconomyTrainer WIN32
  src/Program.cs
  src/MemApi.cs
  src/RadiantDefenseMoneyLocator.cs
  src/TrainerLocalization.cs
  src/TrainerForm.cs
  config/trainer.yaml
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
  config/trainer.yaml
  test/TrainerSelfTest.cs
)
configure_radiantdefensetrainer_csharp_target(TrainerSelfTest)

if(BUILD_TESTING)
  add_test(NAME TrainerSelfTest COMMAND "$<TARGET_FILE:TrainerSelfTest>")
endif()

set_property(DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
  PROPERTY VS_STARTUP_PROJECT RadiantDefenseEconomyTrainer)

message(STATUS "C# compiler: ${CMAKE_CSharp_COMPILER}")
message(STATUS ".NET Framework 4.8 references: ${RADIANTDEFENSETRAINER_NET48_REFERENCE_DIR}")
message(STATUS "Visual Studio solution generated in: ${CMAKE_BINARY_DIR}")
message(STATUS "External UI text: ${RADIANTDEFENSETRAINER_TEXT_CONFIG}")
message(STATUS "Build with: cmake --build \"${CMAKE_BINARY_DIR}\" --config Release")
message(STATUS "Test with: ctest --test-dir \"${CMAKE_BINARY_DIR}\" -C Release --output-on-failure")
