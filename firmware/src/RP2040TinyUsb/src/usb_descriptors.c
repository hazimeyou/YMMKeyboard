#include "usb_descriptors.h"
#include "pico/unique_id.h"
#include <string.h>

#define USB_VID              0x2E8A
#define USB_PID              0x4020
#define USB_BCD              0x0100
#define CFG_TUD_ENDPOINT0_SIZE 64

#define USB_MANUFACTURER     "YMMKeyboard"
#define USB_PRODUCT          "YMMKeyboard RP2040"
#define USB_CDC_ITF_STR      "YMM Serial Bridge"
#define USB_HID_ITF_STR      "YMM Control HID"

const uint8_t hid_report_descriptor[] = {
  0x06, 0x00, 0xFF,        // Usage Page (Vendor Defined 0xFF00)
  0x09, 0x01,              // Usage (0x01)
  0xA1, 0x01,              // Collection (Application)
  0x85, REPORT_ID_VENDOR,  // Report ID
  0x15, 0x00,              // Logical Minimum (0)
  0x26, 0xFF, 0x00,        // Logical Maximum (255)
  0x75, 0x08,              // Report Size (8)
  0x95, 0x3F,              // Report Count (63)
  0x09, 0x01,              // Usage
  0x81, 0x02,              // Input
  0x95, 0x3F,              // Report Count (63)
  0x09, 0x01,              // Usage
  0x91, 0x02,              // Output
  0xC0                     // End Collection
};

const uint16_t hid_report_descriptor_len = sizeof(hid_report_descriptor);

const tusb_desc_device_t desc_device = {
    .bLength = sizeof(tusb_desc_device_t),
    .bDescriptorType = TUSB_DESC_DEVICE,
    .bcdUSB = 0x0200,
    .bDeviceClass = TUSB_CLASS_MISC,
    .bDeviceSubClass = MISC_SUBCLASS_COMMON,
    .bDeviceProtocol = MISC_PROTOCOL_IAD,
    .bMaxPacketSize0 = CFG_TUD_ENDPOINT0_SIZE,
    .idVendor = USB_VID,
    .idProduct = USB_PID,
    .bcdDevice = USB_BCD,
    .iManufacturer = 0x01,
    .iProduct = 0x02,
    .iSerialNumber = 0x03,
    .bNumConfigurations = 0x01
};

uint8_t const *tud_descriptor_device_cb(void)
{
  return (uint8_t const *)&desc_device;
}

enum { CONFIG_TOTAL_LEN = TUD_CONFIG_DESC_LEN + TUD_CDC_DESC_LEN + TUD_HID_DESC_LEN };

uint8_t const desc_configuration[] = {
    TUD_CONFIG_DESCRIPTOR(1, ITF_NUM_TOTAL, 0, CONFIG_TOTAL_LEN, 0x00, 100),
    TUD_CDC_DESCRIPTOR(ITF_NUM_CDC, 0x05, EPNUM_CDC_NOTIF, 8, EPNUM_CDC_OUT, EPNUM_CDC_IN, 64),
    TUD_HID_DESCRIPTOR(ITF_NUM_VENDOR_HID, 0x06, HID_ITF_PROTOCOL_NONE, hid_report_descriptor_len, EPNUM_HID_IN, 64, 1)};

uint8_t const *tud_descriptor_configuration_cb(uint8_t index)
{
  (void)index;
  return desc_configuration;
}

static const char *string_desc_arr[] = {
    (const char[]){0x09, 0x04},
    USB_MANUFACTURER,
    USB_PRODUCT,
    NULL,
    "YMM CDC",
    USB_CDC_ITF_STR,
    USB_HID_ITF_STR};

static uint16_t _desc_str[32];

uint16_t const *tud_descriptor_string_cb(uint8_t index, uint16_t langid)
{
  (void)langid;
  uint8_t chr_count;

  if (index == 0)
  {
    _desc_str[1] = 0x0409;
    chr_count = 1;
  }
  else if (index == 3)
  {
    pico_unique_board_id_t id;
    pico_get_unique_board_id(&id);
    static char serial[2 * PICO_UNIQUE_BOARD_ID_SIZE_BYTES + 1];
    for (uint8_t i = 0; i < PICO_UNIQUE_BOARD_ID_SIZE_BYTES; i++)
    {
      static const char hex[] = "0123456789ABCDEF";
      serial[i * 2] = hex[(id.id[i] >> 4) & 0x0F];
      serial[i * 2 + 1] = hex[id.id[i] & 0x0F];
    }
    serial[2 * PICO_UNIQUE_BOARD_ID_SIZE_BYTES] = '\0';
    chr_count = (uint8_t)strlen(serial);
    for (uint8_t i = 0; i < chr_count; i++) _desc_str[1 + i] = serial[i];
  }
  else
  {
    if (!(index < sizeof(string_desc_arr) / sizeof(string_desc_arr[0]))) return NULL;
    const char *str = string_desc_arr[index];
    chr_count = (uint8_t)strlen(str);
    if (chr_count > 31) chr_count = 31;
    for (uint8_t i = 0; i < chr_count; i++) _desc_str[1 + i] = str[i];
  }

  _desc_str[0] = (uint16_t)((TUSB_DESC_STRING << 8) | (2 * chr_count + 2));
  return _desc_str;
}

uint8_t const *tud_hid_descriptor_report_cb(uint8_t instance)
{
  (void)instance;
  return hid_report_descriptor;
}

uint16_t tud_hid_get_report_cb(uint8_t instance, uint8_t report_id, hid_report_type_t report_type,
                               uint8_t *buffer, uint16_t reqlen)
{
  (void)instance;
  (void)report_id;
  (void)report_type;
  memset(buffer, 0, reqlen);
  return reqlen;
}

void tud_hid_set_report_cb(uint8_t instance, uint8_t report_id, hid_report_type_t report_type,
                           uint8_t const *buffer, uint16_t bufsize)
{
  (void)instance;
  (void)report_id;
  (void)report_type;
  (void)buffer;
  (void)bufsize;
}
