cmake_minimum_required(VERSION 3.13)

set(PICO_BOARD waveshare_rp2040_zero CACHE STRING "Target board")

if(NOT DEFINED ENV{PICO_SDK_PATH})
  message(FATAL_ERROR "PICO_SDK_PATH is not set.")
endif()

include($ENV{PICO_SDK_PATH}/external/pico_sdk_import.cmake)

project(ymm_keyboard_fw C CXX ASM)
set(CMAKE_C_STANDARD 11)
set(CMAKE_CXX_STANDARD 17)

pico_sdk_init()

add_executable(ymm_keyboard_fw
  src/main.c
  src/usb_descriptors.c
)

target_include_directories(ymm_keyboard_fw PRIVATE
  ${CMAKE_CURRENT_LIST_DIR}/src
)

target_compile_definitions(ymm_keyboard_fw PRIVATE
  PICO_STDIO_USB=0
  PICO_STDIO_UART=0
)

target_link_libraries(ymm_keyboard_fw
  pico_stdlib
  tinyusb_device
  tinyusb_board
  hardware_gpio
  hardware_timer
)

pico_add_extra_outputs(ymm_keyboard_fw)
