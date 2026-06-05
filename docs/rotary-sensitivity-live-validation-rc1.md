# Rotary Sensitivity Live Validation RC1

## Goal

Validate that the plugin-side `RotarySensitivity` setting changes how many `SW36` / `SW37` press events are treated as one rotary action.

## Live Setup

- Firmware: no additional rotary firmware changes
- Plugin: rotary sensitivity setting enabled in `YMMKeyboardSettings`
- Default sensitivity: `2`
- Release behavior: `SW36:R` / `SW37:R` are ignored for dispatch

## Diagnostics

Expected rotary diagnostics:

- `RotaryAccumulated`
- `RotaryFiltered`
- `RotaryDispatched`
- `RotaryIgnoredRelease`

Expected standard diagnostics:

- `InputReceived`
- `InputMapped`
- `DispatchPrepared`
- `DispatchExecuted`

## Validation Plan

1. Verify the settings UI shows `ロータリー感度`.
2. Confirm the default value is `標準 / 2`.
3. Test sensitivity `1`, `2`, and `3`.
4. Observe how often `RotaryDispatched` and `DispatchExecuted` occur for the same physical rotary movement.
5. Confirm releases are filtered and do not trigger dispatch.

## Notes

- The live plugin restart is required for the UI and JSON settings to reflect the latest build.
- Sensitivity `1` should feel the most immediate.
- Sensitivity `3` should feel more conservative and reduce over-triggering.
