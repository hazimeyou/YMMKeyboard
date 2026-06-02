# Macro Diagnostics Foundation RC1

## Overview

This phase makes macro lookup, binding, expansion, validation, and plan creation visible without sending anything to YMM or to hardware.

## Tool

- `tools/YMMKeyboard.MacroDiagnosticsViewer`

## Recorded Events

- `MacroLookup`
- `MacroBinding`
- `MacroExpanded`
- `MacroValidation`
- `MacroPlanCreated`

## Sample Scenarios

- `samples/macro-scenarios/single-macro.json`
- `samples/macro-scenarios/nested-macro.json`
- `samples/macro-scenarios/invalid-macro.json`
- `samples/macro-scenarios/large-macro.json`
- `samples/macro-scenarios/missing-macro.json`

## Output

Reports are written to:

- `tmp/macro-diagnostics/`

Files:

- `macro-diagnostics-*.json`
- `macro-diagnostics-*.md`

## CI

Run:

```powershell
./scripts/verify-macro-diagnostics.ps1
```

The script performs build, scenario replay, report generation, and `issues=0` checks.
