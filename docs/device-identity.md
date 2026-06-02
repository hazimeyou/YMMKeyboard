# Device Identity

This document is the source of truth for the formal identity used by the RP2040 firmware, the DeviceInspector tool, and the plugin-side diagnostics.

## Formal Identity

| Item | Value |
|---|---|
| VID | `0x2E8A` |
| PID | `0x4020` |
| Manufacturer | `YMMKeyboard` |
| ProductName | `YMMKeyboard RP2040` |
| Serial | Board UID |
| HID Usage Page | `0xFF00` |
| HID Usage | `0x0001` |
| CDC Interface | `YMM Serial Bridge` |
| HID Interface | `YMM Control HID` |
| HID Report ID | `1` |
| HID Payload | `63 bytes` |

## Sources

| Layer | File | Role |
|---|---|---|
| Firmware | `firmware/src/RP2040TinyUsb/src/usb_descriptors.c` | USB VID/PID, strings, HID descriptor |
| Firmware | `firmware/src/RP2040TinyUsb/src/main.c` | HID payload send path |
| DeviceInspector | `tools/YMMKeyboard.DeviceInspector/DeviceIdentity.cs` | Formal identity constants and classification |
| DeviceInspector | `tools/YMMKeyboard.DeviceInspector/Program.cs` | Inspector output and matched candidate selection |
| Plugin | `ymm-plugin/src/YMMKeyboardPlugin/Settings/YMMKeyboardSettings.cs` | Formal HID filter defaults |
| Plugin | `ymm-plugin/src/YMMKeyboardPlugin/Hid/HidKeyboardLink.cs` | Formal-only HID candidate filtering |
| Plugin | `ymm-plugin/src/YMMKeyboardPlugin/Diagnostics/PluginConnectionDiagnosticCollector.cs` | Formal-only diagnostics classification |

## Removed Legacy Identity

The following legacy values are no longer part of the active identity path:

| Item | Legacy value |
|---|---|
| PID | `0x101F` |
| ProductName | `YMM HID` |
| Legacy candidate classification | `temporary` |

## Validation Notes

After the RC1 firmware identity apply, the expected observation is:

- `DeviceInspector` reports `2E8A:4020`
- `Manufacturer = YMMKeyboard`
- `ProductName = YMMKeyboard RP2040`
- `Serial` matches the board UID
- plugin diagnostics select the formal candidate
- `DiagnosticsComparer` no longer reports `IdentityMismatch`

`MissingHidUsage` may still remain as a separate issue until the next validation step confirms host-visible HID usage data.
