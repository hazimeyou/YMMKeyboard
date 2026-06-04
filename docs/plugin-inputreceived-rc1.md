# Plugin InputReceived RC1

## Purpose

Confirm that the latest matrix formal payload can enter the YMMKeyboardPlugin runtime and reach `InputReceived`.

## Current Confirmed Facts

- Firmware sends `K_<row>_<col>:P/R` with a fixed HID report length of 63 bytes.
- `HidConsoleProbe` receives the formal payload on the host as `K_COLON`.
- The host-side HID transport is working for the formal payload shape.

## Verification Target

The next live YMM4 session should confirm:

- `InputReceived` is recorded for the matrix HID event.
- The event lands in `tmp/input-diagnostics/`.

## What Is Not Required Yet

- `InputMapped`
- `DispatchPrepared`
- Macro execution
- Label conversion

## Success Criteria

- `InputReceived >= 1`
- `raw_report_samples > 0`
- The recorded raw input matches the formal matrix payload path.

## Next Step

Run YMM4 with the latest plugin build and collect a fresh `input-diagnostics` snapshot from a live matrix press.
