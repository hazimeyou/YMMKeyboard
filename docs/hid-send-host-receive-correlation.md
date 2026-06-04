# HID Send / Host Receive Correlation RC1

## Purpose

Correlate the firmware-side HID send path with the host-side HID receive path using the latest diagnostic firmware.

## Confirmed Firmware Identity

- `FW_ID = YMMKeyboard-RP2040-TinyUSB`
- `FW_VERSION = hid-send-debug-rc1`
- `FW_FEATURES = HID_STATUS,HID_TEST,HID_DIAG,FORCED_HID_TEST`

## CDC Trace Capture

### Capture Summary

- Tool: `tools/YMMKeyboard.CdcTraceCapture`
- Port: `COM12`
- Duration: `30s`
- Result file: `tmp/cdc-trace-capture/cdc-trace.json`
- Conclusion: `data_received`

### Firmware-side markers seen in CDC trace

The CDC trace capture shows runtime activity including:

- `HID_STATUS`
- `HID_DIAG`
- `HB:`
- `SW_00`

For this particular capture, `FW_INFO` was not isolated cleanly in the line stream, but the latest diagnostic firmware has already been confirmed by a separate stamp capture.

### CDC keyword counts from the trace

- `HID_STATUS`: `60`
- `HID_DIAG`: `47`
- `HB:`: `10`
- `SW_`: `30`

### Important runtime observation

The trace shows `HID_STATUS ready=true` repeatedly, which means the TinyUSB HID interface is becoming ready during runtime.

## HidConsoleProbe Capture

### Capture Summary

- Tool: `tools/YMMKeyboard.HidConsoleProbe`
- VID/PID: `2E8A:4020`
- Duration: `30s`
- Result file: `tmp/hid-console-probe/hid-console-probe.json`
- Conclusion: `report_received`

### Host-side read result

- `readSuccessCount = 30`
- `readTimeoutCount = 31`
- `openSucceeded = true`
- `readLoopStarted = true`

### First received report

- `Length = 64`
- `Hex = 01 54 45 53 54 5F 48 49 44 5F 30 37 33 33 ...`
- `Ascii = TEST_HID_0733`

## Correlation Result

The correlation is now proven:

- firmware produced HID test traffic
- host opened the correct HID path
- host read HID reports successfully
- the first readable payload was `TEST_HID_0733`

## What This Means

### Confirmed

- firmware is sending HID reports
- host is receiving HID reports
- the host HID path and report framing are compatible enough for the forced test payload

### Still open

- why the earlier standalone probe runs were all `readSuccessCount=0`
- whether those earlier failures were caused by an older UF2, runtime timing, or trace capture timing
- whether the button-triggered `SW_00` path is identical in behavior to the forced test path for every case

## Recommended Next Step

Use the confirmed send/receive path as the baseline, then compare the button-triggered `SW_00` reports against the forced `TEST_HID_*` payloads.

That will let us separate:

- forced test send path
- button input / debounce path
- any remaining host parsing edge cases