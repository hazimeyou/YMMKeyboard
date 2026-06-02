# Dispatch Diagnostics Foundation RC1

## Overview

This phase makes dispatch planning, action generation, validation, rejection, and readiness visible without sending anything to YMM or to hardware.

## Tool

- `tools/YMMKeyboard.DispatchDiagnosticsViewer`

## Recorded Events

- `DispatchPlanCreated`
- `DispatchActionGenerated`
- `DispatchValidation`
- `DispatchRejected`
- `DispatchReady`

## Sample Scenarios

- `samples/dispatch-scenarios/single-action.json`
- `samples/dispatch-scenarios/multi-action.json`
- `samples/dispatch-scenarios/invalid-dispatch.json`
- `samples/dispatch-scenarios/empty-dispatch.json`

## Output

Reports are written to:

- `tmp/dispatch-diagnostics/`

Files:

- `dispatch-diagnostics-*.json`
- `dispatch-diagnostics-*.md`

## CI

Run:

```powershell
./scripts/verify-dispatch-diagnostics.ps1
```

The script performs build, scenario replay, report generation, and `issues=0` checks.
