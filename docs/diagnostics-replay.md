# Diagnostics Replay Foundation RC1

## Overview

This tool merges device, plugin, input, macro, and dispatch diagnostics into a single ordered timeline.

## Tool

- `tools/YMMKeyboard.DiagnosticsReplay`

## Inputs

- `samples/device-inspector/latest.json`
- `samples/plugin-diagnostics/latest.json`
- `samples/input-diagnostics/single-input.json`
- `tmp/macro-diagnostics/macro-diagnostics-*.json`
- `tmp/dispatch-diagnostics/dispatch-diagnostics-*.json`

## Output

Reports are written to:

- `tmp/diagnostics-replay/`

Files:

- `replay-report.json`
- `replay-report.md`

## CI

Run:

```powershell
./scripts/verify-diagnostics-replay.ps1
```

The script performs build, macro diagnostics verification, dispatch diagnostics verification, replay generation, and `issues=0` checks.
