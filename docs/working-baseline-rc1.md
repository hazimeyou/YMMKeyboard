# Working Baseline RC1

## Goal

Freeze the currently confirmed keyboard input path as the working baseline.

## Confirmed End-to-End Path

```text
Physical Key
↓
Matrix Scan
↓
K_0_1:P
↓
USB HID
↓
Windows Host
↓
YMMKeyboardPlugin
↓
InputReceived
↓
InputMapped
↓
DispatchPrepared
↓
DispatchExecuted
↓
YMM actual UI action
```

## Current Baseline Facts

- Firmware version: `matrix-input-formal-payload-rc1`
- Payload format: `K_<row>_<col>:P/R`
- Matrix scan direction: `ROW=output / COL=input pull-up`
- Tested key: `K_0_1`
- Mapped action: `A`
- Plugin `InputReceived` count: `1`
- Plugin `InputMapped` count: `1`
- Plugin `DispatchPrepared` count: `1`
- Plugin `DispatchExecuted` count: `1`
- Actual YMM UI result: `A` was executed through Windows input injection

## Evidence Summary

- Host HID confirmed `K_0_1:P` and `K_0_1:R`
- Plugin diagnostics confirmed `InputReceived`, `InputMapped`, `DispatchPrepared`, and `DispatchExecuted`
- The live dispatch path succeeded after the `SendInput` structure fix

## Known Limitations

- This baseline is intentionally narrow and only freezes the confirmed `K_0_1 -> A` path.
- Additional key mappings are not validated by this baseline.
- Macro behavior is not part of this freeze.
- Future changes that affect payload length, matrix orientation, or dispatch wiring should be treated as a new RC.

## Baseline Decision

This is the current working keyboard input baseline:

`K_0_1:P -> InputReceived -> InputMapped -> DispatchPrepared -> DispatchExecuted -> YMM actual UI action`
