# Plugin InputReceived RC1

## Purpose

Confirm that the latest matrix formal payload can enter the YMMKeyboardPlugin runtime, reach `InputReceived`, and continue into dispatch execution.

## Current Confirmed Facts

- Firmware sends `K_<row>_<col>:P/R` with a fixed HID report length of 63 bytes.
- `HidConsoleProbe` receives the formal payload on the host as `K_COLON`.
- The host-side HID transport is working for the formal payload shape.

## Validation Result

- `InputReceived` was recorded for the matrix HID event.
- The event landed in `tmp/input-diagnostics/`.
- The live run also reached `InputMapped` and `DispatchPrepared`.
- The recorded raw input was `K_0_1:P`.
- The current baseline also reaches `DispatchExecuted` for the `K_0_1 -> A` mapping.

## What Is Not Required Yet

- `InputMapped`
- `DispatchPrepared`
- Macro execution
- Label conversion

## Success Criteria

- `InputReceived >= 1`
- `raw_report_samples > 0`
- The recorded raw input matches the formal matrix payload path.
- `DispatchExecuted >= 1` for the `K_0_1 -> A` path.

## Next Step

- The next phase is to treat this as a confirmed baseline and move on to any remaining mapping or dispatch follow-up only if needed.
