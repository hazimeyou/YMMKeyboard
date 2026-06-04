#include <stdio.h>
#include <string.h>
#include "pico/stdlib.h"
#include "pico/unique_id.h"
#include "tusb.h"
#include "bsp/board.h"
#include "usb_descriptors.h"

#define FW_ID "YMMKeyboard-RP2040-TinyUSB"
#define FW_VERSION "matrix-reverse-direction-probe-rc1"
#define FW_FEATURES "FW_INFO,HID_STATUS,REV_ROW_CONFIG,REV_COL_CONFIG,REV_SCAN_FRAME,REV_COL_EDGE,REV_MATRIX_CANDIDATE"
#define FW_BUILD_TIME __DATE__ " " __TIME__
#define MATRIX_COL_COUNT 7
#define MATRIX_ROW_COUNT 6
#define MATRIX_DEBOUNCE_US 20000
#define MATRIX_DIAG_INTERVAL_US 500000

static const uint MATRIX_COL_PINS[MATRIX_COL_COUNT] = {2, 8, 7, 6, 5, 4, 3};
static const uint MATRIX_ROW_PINS[MATRIX_ROW_COUNT] = {28, 27, 26, 15, 14, 29};

static char g_uid_hex[2 * PICO_UNIQUE_BOARD_ID_SIZE_BYTES + 1];
static bool g_hid_last_send_result = false;
static uint32_t g_hid_ready_true_count = 0;
static uint32_t g_hid_ready_false_count = 0;
static uint32_t g_hid_report_call_count = 0;
static uint32_t g_hid_report_success_count = 0;
static uint32_t g_hid_report_fail_count = 0;
static uint32_t g_hid_test_sent_count = 0;
static uint32_t g_hid_test_fail_count = 0;
static uint8_t g_fw_info_remaining = 5;
static bool g_fw_info_started = false;
static absolute_time_t g_fw_info_last_emit;
static bool g_usb_mounted = false;
static bool g_usb_suspended = false;
static uint32_t g_mount_count = 0;
static uint32_t g_unmount_count = 0;
static uint32_t g_suspend_count = 0;
static uint32_t g_resume_count = 0;
static char g_usb_event_queue[8][160];
static uint8_t g_usb_event_head = 0;
static uint8_t g_usb_event_tail = 0;
static uint8_t g_usb_event_count = 0;
static bool g_matrix_initialized = false;
static bool g_matrix_last_raw[MATRIX_ROW_COUNT][MATRIX_COL_COUNT];
static bool g_matrix_stable[MATRIX_ROW_COUNT][MATRIX_COL_COUNT];
static absolute_time_t g_matrix_last_change[MATRIX_ROW_COUNT][MATRIX_COL_COUNT];
static absolute_time_t g_matrix_last_diag;
static bool g_row_state_last[MATRIX_ROW_COUNT];
static char g_scan_frame_rows[MATRIX_ROW_COUNT + 1];
static uint g_matrix_scan_column = 0;
static uint8_t g_rev_last_raw[MATRIX_ROW_COUNT][MATRIX_COL_COUNT];
static uint8_t g_rev_stable[MATRIX_ROW_COUNT][MATRIX_COL_COUNT];
static absolute_time_t g_rev_last_change[MATRIX_ROW_COUNT][MATRIX_COL_COUNT];
static bool g_rev_col_state_last[MATRIX_COL_COUNT];
static char g_rev_scan_frame_cols[MATRIX_COL_COUNT + 1];
static uint g_rev_scan_row = 0;

static void write_cdc_line(const char *line)
{
  if (!tud_cdc_connected())
    return;

  tud_cdc_write_str(line);
  tud_cdc_write_str("\r\n");
  tud_cdc_write_flush();
}

static void enqueue_usb_event(const char *event)
{
  char line[160];
  snprintf(line, sizeof(line),
           "USB_EVENT %s mounted=%s suspended=%s mountCount=%lu unmountCount=%lu suspendCount=%lu resumeCount=%lu",
           event,
           g_usb_mounted ? "true" : "false",
           g_usb_suspended ? "true" : "false",
           (unsigned long)g_mount_count,
           (unsigned long)g_unmount_count,
           (unsigned long)g_suspend_count,
           (unsigned long)g_resume_count);
  snprintf(g_usb_event_queue[g_usb_event_tail], sizeof(g_usb_event_queue[g_usb_event_tail]), "%s", line);
  g_usb_event_tail = (uint8_t)((g_usb_event_tail + 1) % (uint8_t)(sizeof(g_usb_event_queue) / sizeof(g_usb_event_queue[0])));
  if (g_usb_event_count < (uint8_t)(sizeof(g_usb_event_queue) / sizeof(g_usb_event_queue[0])))
  {
    g_usb_event_count++;
  }
  else
  {
    g_usb_event_head = (uint8_t)((g_usb_event_head + 1) % (uint8_t)(sizeof(g_usb_event_queue) / sizeof(g_usb_event_queue[0])));
  }
}

static void flush_usb_events_if_ready(void)
{
  if (!tud_cdc_connected())
    return;

  while (g_usb_event_count > 0)
  {
    write_cdc_line(g_usb_event_queue[g_usb_event_head]);
    g_usb_event_head = (uint8_t)((g_usb_event_head + 1) % (uint8_t)(sizeof(g_usb_event_queue) / sizeof(g_usb_event_queue[0])));
    g_usb_event_count--;
  }
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
           "HID_STATUS:%s mounted=%s suspended=%s hidReady=%s mountCount=%lu unmountCount=%lu suspendCount=%lu resumeCount=%lu sendCount=%lu sendFailCount=%lu lastSendResult=%s hidReadyTrueCount=%lu hidReadyFalseCount=%lu hidReportCallCount=%lu hidReportSuccessCount=%lu hidReportFailCount=%lu reportId=%u reportLength=%u descriptorLength=%u",
           phase,
           g_usb_mounted ? "true" : "false",
           g_usb_suspended ? "true" : "false",
           hid_ready ? "true" : "false",
           (unsigned long)g_mount_count,
           (unsigned long)g_unmount_count,
           (unsigned long)g_suspend_count,
           (unsigned long)g_resume_count,
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

static void write_hid_status_short(void)
{
  char line[96];
  bool hid_ready = tud_hid_ready();
  snprintf(line, sizeof(line),
           "HID_STATUS ready=%s mounted=%s suspended=%s sendCount=%lu failCount=%lu",
           hid_ready ? "true" : "false",
           g_usb_mounted ? "true" : "false",
           g_usb_suspended ? "true" : "false",
           (unsigned long)g_hid_report_call_count + (unsigned long)g_hid_test_sent_count,
           (unsigned long)g_hid_report_fail_count);
  write_cdc_line(line);
}

static void write_matrix_diag(void)
{
  if (!g_matrix_initialized)
    return;

  char line[128];
  snprintf(line, sizeof(line),
           "MATRIX_SCAN cols=%u rows=%u",
           (unsigned)MATRIX_COL_COUNT,
           (unsigned)MATRIX_ROW_COUNT);
  write_cdc_line(line);
}

static void write_row_config(void)
{
  char line[64];
  for (uint row = 0; row < MATRIX_ROW_COUNT; row++)
  {
    snprintf(line, sizeof(line),
             "ROW_CONFIG GP%u INPUT_PULLUP",
             (unsigned)MATRIX_ROW_PINS[row]);
    write_cdc_line(line);
  }
}

static void write_col_config(void)
{
  char line[160];
  for (uint col = 0; col < MATRIX_COL_COUNT; col++)
  {
    snprintf(line, sizeof(line),
             "COL_CONFIG GP%u OUTPUT",
             (unsigned)MATRIX_COL_PINS[col]);
    write_cdc_line(line);
  }
}

static void write_rev_row_config(void)
{
  char line[64];
  for (uint row = 0; row < MATRIX_ROW_COUNT; row++)
  {
    snprintf(line, sizeof(line),
             "REV_ROW_CONFIG GP%u OUTPUT",
             (unsigned)MATRIX_ROW_PINS[row]);
    write_cdc_line(line);
  }
}

static void write_rev_col_config(void)
{
  char line[64];
  for (uint col = 0; col < MATRIX_COL_COUNT; col++)
  {
    snprintf(line, sizeof(line),
             "REV_COL_CONFIG GP%u INPUT_PULLUP",
             (unsigned)MATRIX_COL_PINS[col]);
    write_cdc_line(line);
  }
}

static void write_scan_frame(uint col, const bool row_values[MATRIX_ROW_COUNT])
{
  for (uint row = 0; row < MATRIX_ROW_COUNT; row++)
    g_scan_frame_rows[row] = row_values[row] ? '1' : '0';
  g_scan_frame_rows[MATRIX_ROW_COUNT] = '\0';

  char line[128];
  snprintf(line, sizeof(line),
           "SCAN_FRAME COL=%u ROWS=%s",
           (unsigned)col,
           g_scan_frame_rows);
  write_cdc_line(line);
}

static void write_rev_scan_frame(uint row, const uint8_t col_levels[MATRIX_COL_COUNT])
{
  for (uint col = 0; col < MATRIX_COL_COUNT; col++)
    g_rev_scan_frame_cols[col] = col_levels[col] ? '1' : '0';
  g_rev_scan_frame_cols[MATRIX_COL_COUNT] = '\0';

  char line[128];
  snprintf(line, sizeof(line),
           "REV_SCAN_FRAME ROW=%u COLS=%s",
           (unsigned)row,
           g_rev_scan_frame_cols);
  write_cdc_line(line);
}

static void write_row_edge(uint row, bool old_value, bool new_value)
{
  char line[96];
  snprintf(line, sizeof(line),
           "ROW_EDGE row=%u old=%u new=%u",
           (unsigned)row,
           old_value ? 1u : 0u,
           new_value ? 1u : 0u);
  write_cdc_line(line);
}

static void write_matrix_candidate(uint row, uint col)
{
  char line[96];
  snprintf(line, sizeof(line),
           "MATRIX_CANDIDATE row=%u col=%u",
           (unsigned)row,
           (unsigned)col);
  write_cdc_line(line);
}

static void write_rev_col_edge(uint row, uint col, uint8_t old_value, uint8_t new_value)
{
  char line[112];
  snprintf(line, sizeof(line),
           "REV_COL_EDGE row=%u col=%u old=%u new=%u",
           (unsigned)row,
           (unsigned)col,
           (unsigned)old_value,
           (unsigned)new_value);
  write_cdc_line(line);
}

static void write_rev_matrix_candidate(uint row, uint col)
{
  char line[96];
  snprintf(line, sizeof(line),
           "REV_MATRIX_CANDIDATE row=%u col=%u",
           (unsigned)row,
           (unsigned)col);
  write_cdc_line(line);
}

static void write_matrix_edge(uint row, uint col, bool pressed)
{
  char line[128];
  snprintf(line, sizeof(line),
           "MATRIX_EDGE row=%u col=%u pressed=%s keyId=K_%u_%u",
           (unsigned)row,
           (unsigned)col,
           pressed ? "true" : "false",
           (unsigned)row,
           (unsigned)col);
  write_cdc_line(line);
}

static void write_matrix_key(uint row, uint col, bool pressed)
{
  char line[128];
  snprintf(line, sizeof(line),
           "MATRIX_KEY row=%u col=%u pressed=%s keyId=K_%u_%u",
           (unsigned)row,
           (unsigned)col,
           pressed ? "true" : "false",
           (unsigned)row,
           (unsigned)col);
  write_cdc_line(line);
}

static void write_matrix_hid(uint row, uint col, bool pressed)
{
  char line[96];
  snprintf(line, sizeof(line),
           "MATRIX_HID row=%u col=%u pressed=%s payload=K_%u_%u",
           (unsigned)row,
           (unsigned)col,
           pressed ? "true" : "false",
           (unsigned)row,
           (unsigned)col);
  write_cdc_line(line);
}

static void init_matrix_input(void)
{
  for (uint col = 0; col < MATRIX_COL_COUNT; col++)
  {
    gpio_init(MATRIX_COL_PINS[col]);
    gpio_set_dir(MATRIX_COL_PINS[col], GPIO_OUT);
    gpio_put(MATRIX_COL_PINS[col], 1);
  }

  for (uint row = 0; row < MATRIX_ROW_COUNT; row++)
  {
    gpio_init(MATRIX_ROW_PINS[row]);
    gpio_set_dir(MATRIX_ROW_PINS[row], GPIO_IN);
    gpio_pull_up(MATRIX_ROW_PINS[row]);
  }

  for (uint row = 0; row < MATRIX_ROW_COUNT; row++)
  {
    g_row_state_last[row] = false;
    for (uint col = 0; col < MATRIX_COL_COUNT; col++)
    {
      g_matrix_last_raw[row][col] = false;
      g_matrix_stable[row][col] = false;
      g_matrix_last_change[row][col] = get_absolute_time();
    }
  }

  write_row_config();
  write_col_config();

  g_matrix_last_diag = get_absolute_time();
  g_matrix_initialized = true;
}

static void init_matrix_reverse_input(void)
{
  for (uint row = 0; row < MATRIX_ROW_COUNT; row++)
  {
    gpio_init(MATRIX_ROW_PINS[row]);
    gpio_set_dir(MATRIX_ROW_PINS[row], GPIO_OUT);
    gpio_put(MATRIX_ROW_PINS[row], 1);
  }

  for (uint col = 0; col < MATRIX_COL_COUNT; col++)
  {
    gpio_init(MATRIX_COL_PINS[col]);
    gpio_set_dir(MATRIX_COL_PINS[col], GPIO_IN);
    gpio_pull_up(MATRIX_COL_PINS[col]);
  }

  for (uint row = 0; row < MATRIX_ROW_COUNT; row++)
  {
    for (uint col = 0; col < MATRIX_COL_COUNT; col++)
    {
      g_rev_last_raw[row][col] = 1;
      g_rev_stable[row][col] = 1;
      g_rev_last_change[row][col] = get_absolute_time();
    }
  }

  write_rev_row_config();
  write_rev_col_config();

  g_matrix_last_diag = get_absolute_time();
  g_matrix_initialized = true;
}

static bool send_hid_report(const char *label, const char *payload_text, uint16_t payload_length, bool pressed)
{
  bool hid_ready = tud_hid_ready();
  bool send_result = false;

  if (hid_ready)
  {
    uint8_t report[63] = {0};
    size_t copy_len = payload_length;
    if (copy_len > sizeof(report))
      copy_len = sizeof(report);
    memcpy(report, payload_text, copy_len);

    g_hid_report_call_count++;
    send_result = tud_hid_report(REPORT_ID_VENDOR, report, (uint16_t)copy_len);
    if (send_result)
    {
      g_hid_report_success_count++;
    }
    else
    {
      g_hid_report_fail_count++;
    }
  }
  else
  {
    g_hid_ready_false_count++;
    g_hid_report_fail_count++;
  }

  g_hid_last_send_result = send_result;
  if (send_result)
    g_hid_ready_true_count++;

  char diag_line[160];
  snprintf(diag_line, sizeof(diag_line),
           "HID_DIAG:button=%s pressed=%s mounted=%s suspended=%s hidReady=%s reportLength=%u reportId=%u sendResult=%s",
           label,
           pressed ? "true" : "false",
           g_usb_mounted ? "true" : "false",
           g_usb_suspended ? "true" : "false",
           hid_ready ? "true" : "false",
           (unsigned)payload_length,
           (unsigned)REPORT_ID_VENDOR,
           send_result ? "true" : "false");
  write_cdc_line(diag_line);

  return send_result;
}

static void poll_matrix_reverse_input(void)
{
  if (!g_matrix_initialized)
    return;

  absolute_time_t now = get_absolute_time();
  uint row = g_rev_scan_row;
  g_rev_scan_row = (uint)((g_rev_scan_row + 1) % MATRIX_ROW_COUNT);

  for (uint r = 0; r < MATRIX_ROW_COUNT; r++)
    gpio_put(MATRIX_ROW_PINS[r], 1);

  gpio_put(MATRIX_ROW_PINS[row], 0);
  sleep_us(5);

  uint8_t col_values[MATRIX_COL_COUNT];
  for (uint col = 0; col < MATRIX_COL_COUNT; col++)
  {
    uint8_t raw_level = gpio_get(MATRIX_COL_PINS[col]) ? 1u : 0u;
    col_values[col] = raw_level;
    bool last_raw = g_rev_last_raw[row][col];

    if (raw_level != last_raw)
    {
      g_rev_last_raw[row][col] = raw_level;
      g_rev_last_change[row][col] = now;
      write_rev_col_edge(row, col, last_raw, raw_level);
      if (raw_level == 0)
        write_rev_matrix_candidate(row, col);
    }

    if (raw_level != g_rev_stable[row][col] &&
        absolute_time_diff_us(g_rev_last_change[row][col], now) >= MATRIX_DEBOUNCE_US)
    {
      g_rev_stable[row][col] = raw_level;
    }
  }

  write_rev_scan_frame(row, col_values);
}

static void write_hid_result(const char *button, bool pressed, bool hid_ready, uint16_t report_length, bool send_result)
{
  write_hid_diag(button, pressed, hid_ready, report_length, REPORT_ID_VENDOR, send_result);
}

static void write_hid_test_result(bool send_result)
{
  char line[128];
  snprintf(line, sizeof(line),
           "HID_TEST sent=%s mounted=%s suspended=%s ready=%s reportId=%u len=%u",
           send_result ? "true" : "false",
           g_usb_mounted ? "true" : "false",
           g_usb_suspended ? "true" : "false",
           tud_hid_ready() ? "true" : "false",
           (unsigned)REPORT_ID_VENDOR,
           63u);
  write_cdc_line(line);
}

static void write_fw_info(void)
{
  char line[192];
  snprintf(line, sizeof(line),
           "FW_INFO FW_ID=%s FW_VERSION=%s FW_BUILD_TIME=%s FW_FEATURES=%s",
           FW_ID,
           FW_VERSION,
           FW_BUILD_TIME,
           FW_FEATURES);
  write_cdc_line(line);
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

static void emit_fw_info_if_ready(void)
{
  if (!tud_cdc_connected() || g_fw_info_remaining == 0)
    return;

  absolute_time_t now = get_absolute_time();
  if (!g_fw_info_started || absolute_time_diff_us(g_fw_info_last_emit, now) >= 1000000)
  {
    g_fw_info_started = true;
    g_fw_info_last_emit = now;
    g_fw_info_remaining--;
    write_fw_info();
  }
}

static void send_event(const char *key_id, bool pressed)
{
  char line[96];
  snprintf(line, sizeof(line), "%s:%c:%s", g_uid_hex, pressed ? 'P' : 'R', key_id);

  write_cdc_line(line);

  char hid_line[63];
  snprintf(hid_line, sizeof(hid_line), "%s", key_id);
  size_t payload_len = strnlen(hid_line, sizeof(hid_line));
  bool report_ok = send_hid_report(key_id, hid_line, (uint16_t)payload_len, pressed);
  write_hid_result(key_id, pressed, tud_hid_ready(), (uint16_t)payload_len, report_ok);
}

static void poll_matrix_input(void)
{
  if (!g_matrix_initialized)
    return;

  absolute_time_t now = get_absolute_time();
  uint col = g_matrix_scan_column;
  g_matrix_scan_column = (uint)((g_matrix_scan_column + 1) % MATRIX_COL_COUNT);

  for (uint c = 0; c < MATRIX_COL_COUNT; c++)
    gpio_put(MATRIX_COL_PINS[c], 1);

  gpio_put(MATRIX_COL_PINS[col], 0);
  sleep_us(5);

  bool row_values[MATRIX_ROW_COUNT];
  for (uint row = 0; row < MATRIX_ROW_COUNT; row++)
  {
    bool raw_pressed = gpio_get(MATRIX_ROW_PINS[row]) == 0;
    row_values[row] = raw_pressed;
    bool last_raw = g_matrix_last_raw[row][col];

    if (raw_pressed != last_raw)
    {
      g_matrix_last_raw[row][col] = raw_pressed;
      g_matrix_last_change[row][col] = now;
      write_matrix_edge(row, col, raw_pressed);
      write_row_edge(row, g_row_state_last[row], raw_pressed);
      g_row_state_last[row] = raw_pressed;
      write_matrix_candidate(row, col);
    }

    if (raw_pressed != g_matrix_stable[row][col] &&
        absolute_time_diff_us(g_matrix_last_change[row][col], now) >= MATRIX_DEBOUNCE_US)
    {
      g_matrix_stable[row][col] = raw_pressed;

      char key_id[16];
      snprintf(key_id, sizeof(key_id), "K_%u_%u", (unsigned)row, (unsigned)col);
      write_matrix_key(row, col, raw_pressed);
      send_event(key_id, raw_pressed);
      write_matrix_hid(row, col, raw_pressed);
    }
  }
  write_scan_frame(col, row_values);
}

void tud_mount_cb(void)
{
  g_usb_mounted = true;
  g_usb_suspended = false;
  g_mount_count++;
  enqueue_usb_event("mount");
}

void tud_umount_cb(void)
{
  g_usb_mounted = false;
  g_usb_suspended = false;
  g_unmount_count++;
  enqueue_usb_event("unmount");
}

void tud_suspend_cb(bool remote_wakeup_en)
{
  (void)remote_wakeup_en;
  g_usb_suspended = true;
  g_suspend_count++;
  enqueue_usb_event(remote_wakeup_en ? "suspend remoteWakeup=true" : "suspend remoteWakeup=false");
}

void tud_resume_cb(void)
{
  g_usb_suspended = false;
  g_resume_count++;
  enqueue_usb_event("resume");
}

int main(void)
{
  board_init();
  tusb_init();
  make_uid_hex();
  init_matrix_reverse_input();

  absolute_time_t last_hb = get_absolute_time();
  absolute_time_t last_status = get_absolute_time();

  while (true)
  {
    tud_task();
    flush_usb_events_if_ready();

    emit_fw_info_if_ready();
    poll_matrix_reverse_input();

    if (absolute_time_diff_us(g_matrix_last_diag, get_absolute_time()) >= MATRIX_DIAG_INTERVAL_US)
    {
      g_matrix_last_diag = get_absolute_time();
      write_matrix_diag();
    }

    if (absolute_time_diff_us(last_hb, get_absolute_time()) > 3000000)
    {
      last_hb = get_absolute_time();
      char hb_line[80];
      snprintf(hb_line, sizeof(hb_line), "HB: fw=%s uid=%s", FW_VERSION, g_uid_hex);
      write_cdc_line(hb_line);
    }

    if (absolute_time_diff_us(last_status, get_absolute_time()) > 1000000)
    {
      last_status = get_absolute_time();
      write_hid_status("poll");
      write_hid_status_short();
    }

    sleep_ms(1);
  }
}
