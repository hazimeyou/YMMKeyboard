#include <stdio.h>
#include <string.h>
#include "pico/stdlib.h"
#include "pico/unique_id.h"
#include "tusb.h"
#include "bsp/board.h"
#include "usb_descriptors.h"

static char g_uid_hex[2 * PICO_UNIQUE_BOARD_ID_SIZE_BYTES + 1];
static bool g_hid_last_send_result = false;
static uint32_t g_hid_ready_true_count = 0;
static uint32_t g_hid_ready_false_count = 0;
static uint32_t g_hid_report_call_count = 0;
static uint32_t g_hid_report_success_count = 0;
static uint32_t g_hid_report_fail_count = 0;

static void write_cdc_line(const char *line)
{
  if (!tud_cdc_connected())
    return;

  tud_cdc_write_str(line);
  tud_cdc_write_str("\r\n");
  tud_cdc_write_flush();
}

static void write_hid_diag(const char *button, bool pressed, bool hid_ready, uint16_t report_length, uint8_t report_id, bool send_result)
{
  char line[128];
  snprintf(line, sizeof(line),
           "HID_DIAG:button=%s pressed=%s hidReady=%s reportLength=%u reportId=%u sendResult=%s",
           button,
           pressed ? "true" : "false",
           hid_ready ? "true" : "false",
           (unsigned)report_length,
           (unsigned)report_id,
           send_result ? "true" : "false");
  write_cdc_line(line);
}

static void write_hid_status(const char *phase)
{
  char line[220];
  bool hid_ready = tud_hid_ready();
  snprintf(line, sizeof(line),
           "HID_STATUS:%s ready=%s sendCount=%lu sendFailCount=%lu lastSendResult=%s hidReadyTrueCount=%lu hidReadyFalseCount=%lu hidReportCallCount=%lu hidReportSuccessCount=%lu hidReportFailCount=%lu reportId=%u reportLength=%u descriptorLength=%u",
           phase,
           hid_ready ? "true" : "false",
           (unsigned long)g_hid_report_call_count,
           (unsigned long)g_hid_report_fail_count,
           g_hid_last_send_result ? "true" : "false",
           (unsigned long)g_hid_ready_true_count,
           (unsigned long)g_hid_ready_false_count,
           (unsigned long)g_hid_report_call_count,
           (unsigned long)g_hid_report_success_count,
           (unsigned long)g_hid_report_fail_count,
           (unsigned)REPORT_ID_VENDOR,
           (unsigned)63,
           (unsigned)hid_report_descriptor_len);
  write_cdc_line(line);
}

static void write_hid_result(const char *button, bool pressed, bool hid_ready, uint16_t report_length, bool send_result)
{
  write_hid_diag(button, pressed, hid_ready, report_length, REPORT_ID_VENDOR, send_result);
}

static void make_uid_hex(void)
{
  pico_unique_board_id_t id;
  pico_get_unique_board_id(&id);
  for (uint8_t i = 0; i < PICO_UNIQUE_BOARD_ID_SIZE_BYTES; i++)
  {
    static const char hex[] = "0123456789abcdef";
    g_uid_hex[i * 2] = hex[(id.id[i] >> 4) & 0x0F];
    g_uid_hex[i * 2 + 1] = hex[id.id[i] & 0x0F];
  }
  g_uid_hex[2 * PICO_UNIQUE_BOARD_ID_SIZE_BYTES] = '\0';
}

static void send_event(const char state, uint8_t sw_id)
{
  char line[64];
  snprintf(line, sizeof(line), "%s:%c:SW_%02u", g_uid_hex, state, sw_id);

  write_cdc_line(line);

  bool hid_ready = tud_hid_ready();
  if (!hid_ready)
  {
    g_hid_last_send_result = false;
    g_hid_ready_false_count++;
    write_hid_result("SW_00", state == 'P', false, 0, false);
    return;
  }

  g_hid_ready_true_count++;

  uint8_t report[63] = {0};
  char hid_line[63];
  snprintf(hid_line, sizeof(hid_line), "YMMK:%s", line);
  size_t payload_len = strnlen(hid_line, sizeof(report));
  memcpy(report, hid_line, payload_len);

  g_hid_report_call_count++;
  bool report_ok = tud_hid_report(REPORT_ID_VENDOR, report, (uint16_t)payload_len);
  if (report_ok)
  {
    g_hid_report_success_count++;
  }
  else
  {
    g_hid_report_fail_count++;
  }
  g_hid_last_send_result = report_ok;
  write_hid_result("SW_00", state == 'P', true, (uint16_t)payload_len, report_ok);
}

int main(void)
{
  board_init();
  tusb_init();
  make_uid_hex();

  absolute_time_t last_hb = get_absolute_time();
  absolute_time_t last_diag = get_absolute_time();
  absolute_time_t last_status = get_absolute_time();

  while (true)
  {
    tud_task();

    if (absolute_time_diff_us(last_hb, get_absolute_time()) > 3000000)
    {
      last_hb = get_absolute_time();
      char hb_line[80];
      snprintf(hb_line, sizeof(hb_line), "HB:%s", g_uid_hex);
      write_cdc_line(hb_line);
    }

    if (absolute_time_diff_us(last_diag, get_absolute_time()) > 5000000)
    {
      last_diag = get_absolute_time();
      send_event('P', 0);
      send_event('R', 0);
    }

    if (absolute_time_diff_us(last_status, get_absolute_time()) > 1000000)
    {
      last_status = get_absolute_time();
      write_hid_status("poll");
    }

    // TODO: GPIO matrix scan and encoder integration.
    sleep_ms(1);
  }
}
