# COM/HID Correlation Probe

## Purpose

Capture COM/CDC lines and HID raw reports in a single run, then write a combined timeline to:

- `tmp/com-hid-correlation/correlation.json`
- `tmp/com-hid-correlation/correlation.md`

If you only want interface presence checks, use snapshot mode:

- `tmp/usb-interface-snapshot/snapshot.json`
- `tmp/usb-interface-snapshot/snapshot.md`

## CLI

```powershell
dotnet run --project tools/YMMKeyboard.ComHidCorrelationProbe -- --port COM12 --vid 2E8A --pid 4020 --duration-sec 30
```

Snapshot-only mode:

```powershell
dotnet run --project tools/YMMKeyboard.ComHidCorrelationProbe -- --snapshot-only --duration-sec 30
```

## Captured Fields

- COM
  - `FW_INFO`
  - `HB:`
  - `SW_00`
  - `HID_STATUS`
  - `HID_TEST`
  - `HID_DIAG`
- HID
  - `TEST_HID_*`
  - `SW_*`
  - `OTHER`

## Summary Counters

- `comLineCount`
- `hidReportCount`
- `testHidCount`
- `swReportCount`
- `otherReportCount`
- `swDiagCount`
- `swDiagSentTrueCount`
- `swDiagSentFalseCount`
- `correlatedSwCount`

## Snapshot Counters

- `snapshotCount`
- `hidPresentCount`
- `comPresentCount`
- `bothPresentCount`
- `hidMissingCount`
- `comMissingCount`
