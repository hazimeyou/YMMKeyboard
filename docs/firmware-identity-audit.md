# Firmware Identity Audit

This audit records where the firmware identity is defined and how it relates to the first hardware observation.

## Scope

- code audit only
- no firmware rewrite
- no flash
- no HID changes
- no PID changes
- no Usage changes
- no stash apply

## Current Observed

Observed on the attached device during RC1 Phase 1:

| Field | Value |
|---|---|
| VID | `0x2E8A` |
| PID | `0x101F` |
| Manufacturer | `YMMKeyboard` |
| ProductName | `YMM HID` |
| Serial | `50443404287E991C` |
| UsagePage | `0x0000` |
| Usage | `0x0000` |

Observed comparer result:

- `totalIssues = 13`
- main issues: `ScoreMismatch`, `MissingHidUsage`

## Current Source

The active source in the repository defines the following identity:

### `firmware/src/RP2040TinyUsb/src/usb_descriptors.c`

- `USB_VID` is defined at line 5 as `0x2E8A`
- `USB_PID` is defined at line 6 as `0x101F`
- `USB_MANUFACTURER` is defined at line 10 as `YMMKeyboard`
- `USB_PRODUCT` is defined at line 11 as `RP2040 TinyUSB Keyboard`
- `iSerialNumber` is assigned at line 47
- the serial string is generated from `pico_get_unique_board_id()` in `tud_descriptor_string_cb()` at lines 90-103
- the HID report descriptor declares `Usage Page (Vendor Defined 0xFF00)` and `Usage (0x01)` at lines 16-17
- the HID report descriptor uses `Report Count (64)` in the current source at lines 23 and 26

### `firmware/src/RP2040TinyUsb/src/main.c`

- `pico_get_unique_board_id()` is used at lines 11-21 to build the runtime UID string
- `send_event()` formats the CDC line from the UID at lines 24-45
- `send_event()` formats the HID payload from the UID at lines 36-43

## Deferred Local Edits

The stashed local edits referenced by the task contain an alternate identity definition:

### `stash@{0}:firmware/src/RP2040TinyUsb/src/usb_descriptors.c`

- `USB_VID` is `0x2E8A`
- `USB_PID` is `0x4020`
- `USB_MANUFACTURER` is `YMMKeyboard Project`
- `USB_PRODUCT` is `YMM RP2040 Control Keyboard`
- the HID report descriptor uses `Report Count (63)` for input and output at lines 23 and 26
- `iSerialNumber` is still generated from `pico_get_unique_board_id()` at lines 90-103

### `stash@{0}:firmware/src/RP2040TinyUsb/src/main.c`

- `pico_get_unique_board_id()` is used at lines 29-40 to build the runtime UID string
- `send_event()` writes CDC and HID event payloads from the UID at lines 42-70
- `poll_switches()` is present and sends button events at lines 85-100

## Expected Identity

The expected identity for the formal RP2040 target is:

| Field | Expected |
|---|---|
| VID | `0x2E8A` |
| PID | `0x4020` |
| Manufacturer | `YMMKeyboard Project` |
| ProductName | `YMM RP2040 Control Keyboard` |
| Serial | generated from `pico_get_unique_board_id()` |
| UsagePage | `0xFF00` |
| Usage | `0x0001` |

## Delta

### Matches

- VID is already `0x2E8A`
- Serial generation is already based on `pico_get_unique_board_id()`
- HID report descriptor already targets vendor-defined usage page `0xFF00`
- HID usage value is already `0x0001`

### Mismatches

- PID is `0x101F` in the active source, not `0x4020`
- Manufacturer is `YMMKeyboard` in the active source, not `YMMKeyboard Project`
- ProductName is `RP2040 TinyUSB Keyboard` in the active source, not `YMM RP2040 Control Keyboard`
- the active source uses `Report Count (64)` in the HID report descriptor, while the stashed candidate uses `63`
- the observed device reports `UsagePage=0x0000` and `Usage=0x0000`, which does not match the descriptor intent

## Notes

- The active source and the stashed candidate are intentionally kept separate in this audit.
- No changes were made to firmware or stash contents.
