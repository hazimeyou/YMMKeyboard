# Diagnostics Workflow

This document describes the RC2 diagnostics flow.

The goal is to validate diagnostics without hardware:

- compare `samples/device-inspector/latest.json` and `samples/plugin-diagnostics/latest.json`
- verify `DiagnosticsComparer` reports `issues=0`
- verify `ProtocolSimulator` reports replay `issues=0`
- keep hardware-dependent validation for later phases

## Current phase

- Diagnostics Foundation RC2

## What is in scope

- `DeviceInspector` JSON shape
- plugin diagnostics JSON shape
- comparer output
- protocol simulation replay
- CI verification using checked-in samples

## What is not in scope

- HID enumeration against real hardware
- COM enumeration against real hardware
- input dispatch to YMM
- device connection changes

## Sample artifacts

- `samples/device-inspector/latest.json`
- `samples/plugin-diagnostics/latest.json`
- `samples/comparer/report.md`

The sample files are the canonical inputs for CI and local verification.

## Local verification

Run the full RC2 check from the repository root:

```powershell
./scripts/verify-diagnostics.ps1
```

The script performs these steps:

1. `dotnet build YMMKeyboardPlugin.slnx -c Release`
2. `DiagnosticsComparer` against the sample JSON files
3. `ProtocolSimulator` against the same sample JSON files
4. validation that both tools report `issues=0`

## Direct tool commands

Comparer:

```powershell
dotnet run --project tools/YMMKeyboard.DiagnosticsComparer/YMMKeyboard.DiagnosticsComparer.csproj -c Release -- --inspector samples/device-inspector/latest.json --plugin samples/plugin-diagnostics/latest.json --format markdown --output tmp/diagnostics-ci/report.md
```

Simulator:

```powershell
dotnet run --project tools/YMMKeyboard.ProtocolSimulator/YMMKeyboard.ProtocolSimulator.csproj -c Release -- --inspector samples/device-inspector/latest.json --plugin samples/plugin-diagnostics/latest.json --format markdown --output tmp/diagnostics-ci/protocol-simulator-report.md
```

## CI

GitHub Actions runs the same verification through `scripts/verify-diagnostics.ps1`.

Workflow:

- `.github/workflows/diagnostics.yml`

That workflow runs on `push` and `pull_request`.

## Expected result

Successful RC2 verification means:

- `dotnet build YMMKeyboardPlugin.slnx -c Release` succeeds
- `DiagnosticsComparer` reports `issues=0`
- `ProtocolSimulator` reports replay `issues=0`
- the sample JSON files are sufficient for repeatable verification

## Next phase boundary

Hardware validation starts only after RC2 is complete and the simulation path is stable.
