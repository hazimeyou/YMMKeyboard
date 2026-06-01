#include <stdio.h>
#include <string.h>
#include "pico/stdlib.h"
#include "pico/unique_id.h"
#include "tusb.h"
#include "bsp/board.h"
#include "usb_descriptors.h"

static char g_uid_hex[2 * PICO_UNIQUE_BOARD_ID_SIZE_BYTES + 1];

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

  if (tud_cdc_connected())
  {
    tud_cdc_write_str(line);
    tud_cdc_write_str("\r\n");
    tud_cdc_write_flush();
  }

  if (tud_hid_ready())
  {
    uint8_t report[65] = {0};
    char hid_line[64];
    snprintf(hid_line, sizeof(hid_line), "YMMK:%s", line);
    report[0] = REPORT_ID_VENDOR;
    memcpy(&report[1], hid_line, strnlen(hid_line, 64));
    tud_hid_report(REPORT_ID_VENDOR, &report[1], 64);
  }
}

int main(void)
{
  board_init();
  tusb_init();
  make_uid_hex();

  absolute_time_t last_hb = get_absolute_time();
  absolute_time_t last_diag = get_absolute_time();

  while (true)
  {
    tud_task();

    if (absolute_time_diff_us(last_hb, get_absolute_time()) > 3000000)
    {
      last_hb = get_absolute_time();
      if (tud_cdc_connected())
      {
        tud_cdc_write_str("HB:");
        tud_cdc_write_str(g_uid_hex);
        tud_cdc_write_str("\r\n");
        tud_cdc_write_flush();
      }
    }

    if (absolute_time_diff_us(last_diag, get_absolute_time()) > 5000000)
    {
      last_diag = get_absolute_time();
      send_event('P', 0);
      send_event('R', 0);
    }

    // TODO: GPIO matrix scan and encoder integration.
    sleep_ms(1);
  }
}
