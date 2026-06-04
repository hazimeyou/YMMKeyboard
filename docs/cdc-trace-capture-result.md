# CDC Trace Capture Result

## Current Status

- completed

## Expected Files

- `tmp/cdc-trace-capture/cdc-trace.log`
- `tmp/cdc-trace-capture/cdc-trace.json`

## Run Summary

- Date: 2026-06-04
- Board count: 1
- YMM4 / Plugin: not used
- Port: `COM12`
- Duration: `30s`
- Timeout: `500ms`

## What Was Captured

- COM port enumeration
- line-by-line CDC output
- keyword hits:
  - `FW_INFO`
  - `USB_EVENT`
  - `HID_STATUS`
  - `HID_DIAG`
  - `HB:`
  - `SW_`

## Result Snapshot

### Port Selection

- `port`: `COM12`
- `description`: `USB Serial Device`
- `manufacturer`: `Microsoft`
- `pnpDeviceId`: `USB\VID_2E8A&PID_4020&MI_00\8&A4FB9C4&0&0000`
- `vid`: `2E8A`
- `pid`: `4020`
- `serialEstimate`: `8&A4FB9C4&0&0000`

### Firmware Version

- `firmwareInfoDetected`: `true`
- `firmwareId`: `YMMKeyboard-RP2040-TinyUSB`
- `firmwareVersion`: `lifecycle-diagnostics-rc1`
- `firmwareBuildTime`: `Jun  3 2026 22:42:39`
- `firmwareFeatures`: `FW_INFO,HID_STATUS,HID_TEST,HID_DIAG,USB_EVENT,LIFECYCLE_DIAG`

### Open Result

- `openSucceeded`: `true`
- `lineCount`: `97`
- `firstLineAt`: `2026-06-04 11:45:36.968`
- `lastLineAt`: `2026-06-04 11:46:06.686`
- `readTimeoutCount`: `30`
- `lastException`: `TimeoutException`

### Keyword Summary

- `FW_INFO`: `4`
- `FW_ID`: `0`
- `FW_VERSION`: `0`
- `FW_FEATURES`: `0`
- `USB_EVENT`: `1`
- `HID_STATUS`: `63`
- `HID_TEST`: `4`
- `HID_DIAG`: `47`
- `HB:`: `10`
- `P/R:`: `0`
- `SW_`: `30`

## Interpretation

- This CDC capture shows the new lifecycle diagnostics firmware is alive.
- `FW_INFO` is observed and confirms the current firmware image.
- `USB_EVENT mount` is observed.
- `HID_STATUS` appears repeatedly with `ready=true`.
- `HID_DIAG` appears repeatedly for `SW_00` and the forced test path.
- `SW_00` is observed in CDC with `sendResult=true`.

## Conclusion

- CDC output is alive.
- USB lifecycle diagnostics are active.
- The button path is observed in CDC and appears to send successfully.
- The firmware image is confirmed as `lifecycle-diagnostics-rc1`.
