# Diagnostics Handoff

This page is the single handoff point for the current diagnostics baseline.

## Current Reach

- Diagnostics Foundation RC2
- Input Diagnostics Foundation RC1
- Input Simulation Foundation RC1
- Macro & Dispatch Diagnostics RC1
- Unified Diagnostics Replay

## Main Tools

- `tools/YMMKeyboard.DeviceInspector`
- `tools/YMMKeyboard.DiagnosticsComparer`
- `tools/YMMKeyboard.ProtocolSimulator`
- `tools/YMMKeyboard.InputDiagnosticsViewer`
- `tools/YMMKeyboard.InputSimulator`
- `tools/YMMKeyboard.MacroDiagnosticsViewer`
- `tools/YMMKeyboard.DispatchDiagnosticsViewer`
- `tools/YMMKeyboard.DiagnosticsReplay`

## Verify Scripts

- `scripts/verify-diagnostics.ps1`
- `scripts/verify-input-diagnostics.ps1`
- `scripts/verify-input-simulation.ps1`
- `scripts/verify-macro-diagnostics.ps1`
- `scripts/verify-dispatch-diagnostics.ps1`
- `scripts/verify-diagnostics-replay.ps1`

## CI Workflows

- `.github/workflows/diagnostics.yml`
- `.github/workflows/input-diagnostics.yml`
- `.github/workflows/input-simulation.yml`
- `.github/workflows/macro-diagnostics.yml`
- `.github/workflows/dispatch-diagnostics.yml`
- `.github/workflows/diagnostics-replay.yml`

## Samples

- `samples/device-inspector/latest.json`
- `samples/plugin-diagnostics/latest.json`
- `samples/comparer/report.md`
- `samples/input-diagnostics/`
- `samples/input-scenarios/`
- `samples/macro-scenarios/`
- `samples/dispatch-scenarios/`

## tmp Outputs

- `tmp/diagnostics-ci/`
- `tmp/input-diagnostics/`
- `tmp/input-simulator/`
- `tmp/macro-diagnostics/`
- `tmp/dispatch-diagnostics/`
- `tmp/diagnostics-replay/`

## Safe Confirmation

- `dotnet build YMMKeyboardPlugin.slnx -c Release` succeeded
- all verify scripts succeeded
- working tree is clean

## Known Warnings

- `HidDeviceProbe.cs` obsolete warnings x2

## Uncertain Changes

These changes are intentionally not resolved yet.

- `stash@{0}: wip: defer firmware and hid auto filter local edits`
- firmware identity/protocol changes
- settings UI auto filter changes
- `PluginLogger.cs` minor cleanup

## Next Phase Candidates

- Hardware Validation Preparation
- Firmware Identity Review
- HID Auto Filter Review

Hardware validation is still a later phase.

## Practical Boundary

Everything above can be verified without starting hardware validation. Keep real device checks for the later phase.

