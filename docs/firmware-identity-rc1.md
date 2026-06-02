# Firmware Identity RC1

## Summary

RC1 applies the formal device identity to the firmware and the diagnostics path. Legacy identity support is removed from the active flow.

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

## Applied Files

- `firmware/src/RP2040TinyUsb/src/usb_descriptors.c`
- `firmware/src/RP2040TinyUsb/src/main.c`
- `tools/YMMKeyboard.DeviceInspector/DeviceIdentity.cs`
- `tools/YMMKeyboard.DeviceInspector/Program.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/Settings/YMMKeyboardSettings.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/Hid/HidKeyboardLink.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/Diagnostics/PluginConnectionDiagnosticCollector.cs`
- `tools/YMMKeyboard.DiagnosticsComparer/Program.cs`
- `docs/device-identity.md`
- `docs/firmware-identity-review.md`

## What Changed

- `PID 0x101F` was removed from the active identity path.
- `YMM HID` was removed from the active identity path.
- firmware USB strings were aligned to the formal identity.
- HID descriptor and send path were aligned to the 63-byte formal payload.
- plugin-side HID candidate filtering now targets the formal identity only.
- DeviceInspector no longer emits the temporary identity path.
- DiagnosticsComparer no longer treats the temporary identity as valid.

## What Was Not Done

- no HID send verification
- no COM send verification
- no macro verification
- no YMM action verification
- no hardware flash or live validation

## Next Step

Run Hardware Validation RC2 to confirm the observed device identity matches the formal source definition:

- `2E8A:4020`
- `YMMKeyboard RP2040`
- board UID serial
- `UsagePage = 0xFF00`
- `Usage = 0x0001`

`MissingHidUsage` may still be tracked separately if the host-visible usage fields need follow-up.
