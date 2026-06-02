# Macro & Dispatch Diagnostics RC1

## Reach

This phase makes macro resolution, dispatch planning, and YMM action planning observable without sending anything to YMM or hardware.

The verified chain is:

- input
- macro resolution
- step expansion
- dispatch plan generation
- YMM action planning

## Added Tools

- `tools/YMMKeyboard.MacroDiagnosticsViewer`
- `tools/YMMKeyboard.DispatchDiagnosticsViewer`
- `tools/YMMKeyboard.DiagnosticsReplay`

## Samples

- `samples/macro-scenarios/`
- `samples/dispatch-scenarios/`
- `samples/device-inspector/latest.json`
- `samples/plugin-diagnostics/latest.json`
- `samples/input-diagnostics/single-input.json`

## Docs

- `docs/macro-diagnostics.md`
- `docs/dispatch-diagnostics.md`
- `docs/diagnostics-replay.md`

## CI

- `scripts/verify-macro-diagnostics.ps1`
- `scripts/verify-dispatch-diagnostics.ps1`
- `scripts/verify-diagnostics-replay.ps1`
- `.github/workflows/macro-diagnostics.yml`
- `.github/workflows/dispatch-diagnostics.yml`
- `.github/workflows/diagnostics-replay.yml`

## Output

- `tmp/macro-diagnostics/`
- `tmp/dispatch-diagnostics/`
- `tmp/diagnostics-replay/`

## Constraints

- no HID send
- no COM send
- no YMM operation
- no real-device access
- no DeviceIdentity change
- no Protocol change
- no dispatch path change
- no macro runtime send path change

## Unstarted

- hardware validation
- HID enumeration validation on real devices
- COM enumeration validation on real devices

## Known Warnings

- none beyond existing repository warnings outside this phase

## Next Candidates

- Macro & Dispatch Diagnostics RC2
- Hardware Validation

