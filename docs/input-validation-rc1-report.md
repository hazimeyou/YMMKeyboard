# Input Validation RC1 Report

## Summary

- Date: 2026-06-02 23:43 JST
- Operator: Codex
- Host machine: `WEEEEEEI`
- OS version: `Microsoft Windows 10.0.26200`
- Validation target: RC1
- Outcome: input pipeline events not observed yet

## Preconditions

- Hardware Validation RC2 completed
- Plugin selected candidate: `HID:2E8A:4020`
- Input diagnostics enabled: yes
- Output directory: `tmp/input-diagnostics/`

## Input Attempt

- Pressed key: `Key1`
- Press count: `2`
- Notes:
  - The device was reconnected before the attempt.
  - YMM was running when the attempt was made.

## Observed Diagnostics

### Input Diagnostics

- Latest file: `tmp/input-diagnostics/input-diagnostics-20260602_234226.json`
- Event count: `0`
- `InputReceived`: `0`
- `InputMapped`: `0`
- `DispatchPrepared`: `0`

### Plugin Logs

- Diagnostics log: `%LOCALAPPDATA%\\YMMKeyboard\\_diagnostics\\YMMKeyboardPlugin_20260602.log`
- `HID link started`: yes
- `HID candidate interfaces selected`: yes
- `HID device detected`: yes
- `raw_report_samples`: `0`

## Warnings

- `No candidate was selected.` was not present in the latest plugin diagnostics snapshot.
- No `InputReceived` event reached the input diagnostics pipeline.
- No raw HID report sample was observed in the runtime summary.

## Errors

- No fatal errors were recorded in the plugin diagnostics snapshot.
- The YMM log still contains the existing `ExplorerViewModel.Refresh` `ObjectDisposedException`, which is unrelated to input capture.

## Result

- Pass / Fail / Blocked: `Blocked`
- Reason: the HID candidate is selected and the HID link is active, but the actual input report has not reached the input diagnostics pipeline yet.

## Next Step

- Retry `Key1` with a slightly longer press so we can confirm whether a raw HID report is emitted at all.
