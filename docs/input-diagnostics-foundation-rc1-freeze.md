# Input Diagnostics Foundation RC1 Freeze

## Freeze metadata

- RC version: `Input Diagnostics Foundation RC1`
- Build: `Success`
- Viewer: `issues=0`
- Samples: `Success`
- Actual dispatch changed: `false`
- YMM operation changed: `false`
- Hardware validation: `not started`
- Known warnings: `HidDeviceProbe.cs obsolete warnings x2`

## Verification evidence

- `dotnet build YMMKeyboardPlugin.slnx -c Release`
  - pass
- `./scripts/verify-input-diagnostics.ps1`
  - pass

## Frozen surface

The following are frozen until the next phase boundary:

- `InputDiagnosticReport` schema
- `InputDiagnosticEvent` schema
- `InputDiagnosticsViewer` output format
- `samples/input-diagnostics/` baseline samples
- existing send path
- YMM operation path

## Tag candidates

- Recommended: `v0.3.0-input-diagnostics-foundation-rc1`
- Alternate: `input-diagnostics-foundation-rc1`

## Workflow state

- The standard local check is `./scripts/verify-input-diagnostics.ps1`
- CI runs `.github/workflows/input-diagnostics.yml`
- Detailed usage lives in [input-diagnostics.md](input-diagnostics.md)

## Notes

- This freeze records a hardware-free baseline.
- The next phase should start from this point without changing the frozen surface.
