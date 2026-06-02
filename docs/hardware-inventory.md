# Hardware Inventory

This inventory lists the items involved in the hardware validation preparation and the later hardware validation phase.

## Inventory

| Item | Role | Notes |
|---|---|---|
| `RP2040 Zero` | Target device | Hardware to validate later |
| `RP2040 TinyUSB firmware` | Device firmware | Firmware baseline for validation |
| `YMMKeyboardPlugin` | Host-side plugin | Device recognition and mapping logic |
| `DeviceInspector` | Inspection tool | Captures USB and HID identity data |
| `DiagnosticsComparer` | Comparison tool | Compares inspector and plugin observations |
| `ProtocolSimulator` | Replay tool | Verifies the diagnostic replay path |

## Inventory Notes

- The firmware, HID, COM, and YMM operation behavior are frozen for this preparation phase.
- The inventory is descriptive only and does not authorize hardware changes.
- Update this file only when the set of validation assets changes.

## Ownership Check

Before hardware validation begins, confirm that the following are available and current:

- repository source
- build artifacts
- diagnostics samples
- report templates
- verification script
