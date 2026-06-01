#pragma once

#include <stdint.h>
#include "tusb.h"

enum
{
  ITF_NUM_CDC = 0,
  ITF_NUM_CDC_DATA,
  ITF_NUM_VENDOR_HID,
  ITF_NUM_TOTAL
};

enum
{
  EPNUM_CDC_NOTIF = 0x81,
  EPNUM_CDC_OUT = 0x02,
  EPNUM_CDC_IN = 0x82,
  EPNUM_HID_OUT = 0x03,
  EPNUM_HID_IN = 0x83
};

enum
{
  REPORT_ID_VENDOR = 1
};

extern const uint8_t hid_report_descriptor[];
extern const uint16_t hid_report_descriptor_len;

