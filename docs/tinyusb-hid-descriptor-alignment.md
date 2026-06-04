# TinyUSB HID Report Descriptor Alignment Investigation

## Scope

This note checks whether the current HID report descriptor, the firmware send path, and the TinyUSB API usage are aligned.

The goal is to answer four questions:

- whether the device uses a report ID
- how many bytes are sent
- how many bytes the descriptor advertises
- whether the endpoint size matches the on-wire packet size

## Current Descriptor Summary

Source:

- `firmware/src/RP2040TinyUsb/src/usb_descriptors.c`

### HID interface

| Item | Value |
|---|---|
| HID interface number | `ITF_NUM_VENDOR_HID` = `2` |
| Interface string | `YMM Control HID` |

### HID endpoint

| Item | Value |
|---|---|
| Endpoint address | `0x83` (`EPNUM_HID_IN`) |
| Endpoint size | `64` bytes |
| Polling interval | `1` ms |

### Report descriptor

| Item | Value |
|---|---|
| Report descriptor length | `29` bytes |
| Report ID | Present (`REPORT_ID_VENDOR = 1`) |
| Usage Page | `0xFF00` |
| Usage | `0x0001` |
| Report Size | `8` bits |
| Report Count | `63` |
| Input report payload length | `63` bytes |
| Output report payload length | `63` bytes |

### Report descriptor bytes

```text
06 00 FF 09 01 A1 01 85 01 15 00 26 FF 00 75 08
95 3F 09 01 81 02 95 3F 09 01 91 02 C0
```

### Effective wire size

The descriptor advertises:

- `1` byte report ID
- `63` bytes payload

So the total input report size on the wire is `64` bytes, which matches the endpoint size.

## Current Send Call Summary

Source:

- `firmware/src/RP2040TinyUsb/src/main.c`

### Common send path

The firmware uses:

```c
tud_hid_report(REPORT_ID_VENDOR, report, copy_len);
```

Important details:

- `report_id = 1`
- `copy_len` is the payload length only
- the report ID is not manually inserted into the payload buffer
- TinyUSB prepends the report ID internally when `report_id != 0`

### `send_event()`

Observed behavior:

- builds a text payload that starts with `YMMK:`
- computes payload length with `strnlen(...)`
- sends the payload without including any manual report-ID byte
- uses `tud_hid_ready()` as a send gate

### `send_test_report()`

Observed behavior:

- builds `TEST_HID_0001`-style payload text
- sends `63` bytes
- uses `report_id = 1`
- uses the same `tud_hid_report(...)` path as the button event send path

## TinyUSB API Alignment Check

The current code matches TinyUSB's expected calling convention:

- if `report_id != 0`, TinyUSB handles the report ID byte for us
- the `len` argument is the payload length excluding the report ID
- the wire packet becomes `1 + len`

That means:

- `report_id = 1`
- `len = 63`
- wire size = `64`

This is aligned with the descriptor and endpoint size.

## Mismatch Candidates

The current code does not show a report-ID or report-length mismatch.

Possible remaining causes are more likely to be:

1. `tud_hid_ready()` is false at send time
2. `tud_hid_report()` returns false even when called
3. the firmware send path is not actually reached when expected
4. the host is opening the correct path but not receiving traffic for a transport-level reason

## Recommended Fix

### Recommendation: keep Report ID 1

Keep the current descriptor and send-path shape:

- Report ID: `1`
- payload length: `63`
- endpoint size: `64`

Reason:

- it is the canonical TinyUSB usage pattern
- the descriptor and the send call already match
- removing the report ID would require a wider behavior change than the current evidence supports

### What should change next

The next minimal fix should be observability, not alignment:

- record `tud_hid_ready()` at the exact send site
- record `tud_hid_report()` return values separately for test and button sends
- keep the forced test report, because it already isolates button/debounce from HID transport

## Test Plan

1. Build firmware.
2. Flash one board only.
3. Start the standalone HID console probe.
4. Confirm whether any report bytes arrive.
5. Check CDC diagnostics for:
   - `HID_STATUS`
   - `HID_TEST`
   - `HID_DIAG`

Expected outcomes:

- If reports still do not arrive, the issue is not a report-ID / length mismatch.
- If reports arrive, then the host-side receive path is not the blocker.

## Conclusion

The current firmware is aligned as follows:

- report ID is used
- send length is `63` bytes of payload
- descriptor input payload length is `63` bytes
- on-wire input report size is `64` bytes
- endpoint size is `64` bytes

So the current failure is not explained by a report descriptor length mismatch.

The next most useful step is to keep the current descriptor and focus on send-path observability and transport-level confirmation.
