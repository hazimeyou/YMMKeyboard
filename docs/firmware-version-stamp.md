# Firmware Version Stamp

## Purpose

The firmware emits a version stamp over CDC so we can confirm which UF2 is actually running on the board.

## Format

The firmware prints a line like:

```text
FW_INFO FW_ID=YMMKeyboard-RP2040-TinyUSB FW_VERSION=hid-send-debug-rc1 FW_BUILD_TIME=<__DATE__ __TIME__> FW_FEATURES=HID_STATUS,HID_TEST,HID_DIAG,FORCED_HID_TEST
```

## Runtime Rules

- Emit the `FW_INFO` line after CDC becomes available.
- Emit it at least five times.
- Space the emissions roughly one second apart.
- Keep the `HB:` heartbeat, but include the firmware version in the line.

## Interpretation

- If `FW_INFO` does not appear, the flashed UF2 is not the latest diagnostic build or the CDC trace is not capturing the right runtime stream.
- If `FW_INFO` appears but `HID_STATUS` does not, the runtime is alive but the HID diagnostics path is not being executed.