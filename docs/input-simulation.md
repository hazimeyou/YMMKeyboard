# Input Simulation Foundation RC1

## Overview

This phase adds a read-only simulator that generates virtual inputs and replays input diagnostics without touching HID, COM, or YMM dispatch paths.

The target chain is:

`InputReceived -> InputFiltered -> InputMapped -> MacroResolved -> DispatchPrepared`

## CLI

- `tools/YMMKeyboard.InputSimulator`
- `--scenario <file>` generates a single simulated run
- `--replay <file>` validates an existing input-diagnostics JSON file
- `--batch <dir>` runs every scenario in a directory

Example:

```powershell
dotnet run --project tools\YMMKeyboard.InputSimulator -- --scenario samples\input-scenarios\single-key.json
```

## Scenario Format

Each scenario is a small JSON file.

```json
{
  "transportType": "hid",
  "input": "KEY_A"
}
```

Supported samples:

- `samples/input-scenarios/single-key.json`
- `samples/input-scenarios/modifier-key.json`
- `samples/input-scenarios/macro-trigger.json`
- `samples/input-scenarios/invalid-input.json`

## Output

The simulator writes:

- `tmp/input-diagnostics/input-diagnostics-*.json`
- `tmp/input-simulator/simulation-report.json`
- `tmp/input-simulator/simulation-report.md`

## CI

The verification entry point is:

```powershell
./scripts/verify-input-simulation.ps1
```

The script performs:

1. build
2. batch simulation
3. viewer replay
4. simulator replay
5. `issues=0` checks

## Constraints

- no HID send
- no COM send
- no YMM operation
- no real-device access
- no DeviceIdentity change
- no Protocol change
