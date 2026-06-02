# Firmware Identity Review

## Current State

The formal identity has been applied in source.

Formal identity:

- `VID = 0x2E8A`
- `PID = 0x4020`
- `Manufacturer = YMMKeyboard`
- `ProductName = YMMKeyboard RP2040`
- `Serial = Board UID`
- `UsagePage = 0xFF00`
- `Usage = 0x0001`

This review is source-level only. Hardware validation is deferred to RC2.

## Source Review

The identity source of truth is now split across the firmware and plugin diagnostics path:

- `firmware/src/RP2040TinyUsb/src/usb_descriptors.c`
- `firmware/src/RP2040TinyUsb/src/main.c`
- `tools/YMMKeyboard.DeviceInspector/DeviceIdentity.cs`
- `tools/YMMKeyboard.DeviceInspector/Program.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/Settings/YMMKeyboardSettings.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/Hid/HidKeyboardLink.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/Diagnostics/PluginConnectionDiagnosticCollector.cs`
- `tools/YMMKeyboard.DiagnosticsComparer/Program.cs`

## Resolved Deltas

The following legacy values were removed from the active path:

- `PID 0x101F`
- `ProductName YMM HID`
- `temporary` identity classification

The following formal values are now aligned in source:

- `VID 0x2E8A`
- `PID 0x4020`
- `Manufacturer YMMKeyboard`
- `ProductName YMMKeyboard RP2040`
- `UsagePage 0xFF00`
- `Usage 0x0001`

## Remaining Validation

RC2 should confirm:

- `DeviceInspector` reports `2E8A:4020`
- `DeviceInspector` reports `YMMKeyboard RP2040`
- plugin diagnostics select the formal candidate
- `DiagnosticsComparer` no longer reports `IdentityMismatch`

`MissingHidUsage` may remain separately if host-side usage visibility still needs follow-up.
