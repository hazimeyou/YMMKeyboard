# Hardware Validation RC1 Report

## Summary

- Date: 2026-06-02 21:22 JST
- Operator: Codex
- Host machine: `WEEEEEEI`
- OS version: `Microsoft Windows 10.0.26200`
- Repository state: working tree clean before this phase
- Validation phase: USB Enumeration Validation
- Outcome: observed with mismatches

## Scope

This report covers the first real hardware observation only.

Observed tools:

- `DeviceInspector`
- plugin diagnostics
- `DiagnosticsComparer`

Not performed:

- HID send
- COM send
- macro execution
- YMM operations
- firmware changes
- stash application

## Procedure

1. Connected the RP2040 hardware.
2. Ran `DeviceInspector` with JSON output to `tmp/device-inspector/latest.json`.
3. Started YMM4 with plugin diagnostics redirected to `tmp/plugin-diagnostics/`.
4. Captured plugin diagnostics to `tmp/plugin-diagnostics/latest.json`.
5. Ran `DiagnosticsComparer` and wrote `tmp/diagnostics-comparer/report.md`.

## Device Inspector

Command used:

```powershell
dotnet run --project tools/YMMKeyboard.DeviceInspector/YMMKeyboard.DeviceInspector.csproj -c Release -- --json --output tmp/device-inspector/latest.json
```

Summary:

- HID count: `41`
- COM count: `2`
- JSON output: `tmp/device-inspector/latest.json`

Observed YMM device:

| Field | Value |
|---|---|
| VID | `0x2E8A` |
| PID | `0x101F` |
| Manufacturer | `YMMKeyboard` |
| ProductName | `YMM HID` |
| Serial | `50443404287E991C` |
| UsagePage | `0x0000` |
| Usage | `0x0000` |
| ReportLength | `Input=65 / Output=65` |

Observation:

- The expected formal identity `0x2E8A:0x4020` was not observed in this run.
- The observed YMM device was classified as `temporary`.
- HID usage information was missing for the YMM device.

## Plugin Diagnostics

Captured to:

- `tmp/plugin-diagnostics/latest.json`

Observed YMM candidate:

| Field | Value |
|---|---|
| Transport | `HID` |
| VID | `0x2E8A` |
| PID | `0x101F` |
| Manufacturer | `YMMKeyboard` |
| ProductName | `YMM HID` |
| Serial | `50443404287E991C` |
| UsagePage | `0x0000` |
| Usage | `0x0000` |
| Selected | `true` |
| MatchScore | `7365` |
| MatchReasons | `connectionMode=HID`, `vid=2E8A`, `pid=101F`, `manufacturer~YMMKeyboard` |
| RejectReasons | `none` |

Rejected candidates:

- non-YMM HID devices present on the host
- `COM1`
- `COM9`
- configured `COM7` not enumerated

Observation:

- Plugin selection matched the temporary YMM HID device, not the formal `2E8A:4020` identity.

## Comparer

Command used:

```powershell
dotnet run --project tools/YMMKeyboard.DiagnosticsComparer/YMMKeyboard.DiagnosticsComparer.csproj -c Release -- --inspector tmp/device-inspector/latest.json --plugin tmp/plugin-diagnostics/latest.json --format markdown --output tmp/diagnostics-comparer/report.md
```

Summary:

- matchedHid: `9`
- matchedCom: `2`
- selectedCandidates: `1`
- totalIssues: `13`
- report: `tmp/diagnostics-comparer/report.md`

Observed comparer issues relevant to this phase:

- `ScoreMismatch` for the YMM candidate
- `MissingHidUsage` for the YMM candidate
- plugin-only candidate entries for non-YMM devices and COM ports

Observation:

- DeviceInspector, plugin diagnostics, and comparer do not yet agree on the formal expected identity for this run.

## Result

- USB Enumeration Validation: observed
- Success conditions met: not fully
- Serial missing: no
- Usage missing: yes
- Candidate rejected: yes
- Identity mismatch: yes

## Artifacts

- `tmp/device-inspector/latest.json`
- `tmp/device-inspector/device-inspector_20260602_212258.log`
- `tmp/plugin-diagnostics/latest.json`
- `tmp/diagnostics-comparer/report.md`

## Notes

- This is the first real hardware observation.
- No remediation was performed.
- Firmware identity review is the next phase.
