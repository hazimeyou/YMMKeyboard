# CDC Trace Capture

## Purpose

Capture the firmware runtime CDC output directly from the COM port, without YMM4 or the plugin.

This lets us separate:

- firmware runtime not reaching the print sites
- firmware runtime printing, but the trace not being captured
- host-side HID receive issues

## Tool

- `tools/YMMKeyboard.CdcTraceCapture`

## Usage

Recommended default:

```powershell
dotnet run --project tools/YMMKeyboard.CdcTraceCapture -- --vid 2E8A --pid 4020 --duration-sec 30 --timeout-ms 500
```

Optional explicit port selection:

```powershell
dotnet run --project tools/YMMKeyboard.CdcTraceCapture -- --port COM12 --duration-sec 30 --timeout-ms 500
```

## Outputs

The tool writes to:

- `tmp/cdc-trace-capture/cdc-trace.log`
- `tmp/cdc-trace-capture/cdc-trace.json`

## What it captures

- COM port enumeration
- selected COM port
- line-by-line CDC output
- keyword hits:
  - `FW_INFO`
  - `FW_ID`
  - `FW_VERSION`
  - `FW_FEATURES`
  - `HID_STATUS`
  - `HID_TEST`
  - `HID_DIAG`
  - `HB:`
  - `P/R:`
  - `SW_`

## JSON Fields

The JSON report includes these firmware-version fields when present:

- `firmwareInfoDetected`
- `firmwareId`
- `firmwareVersion`
- `firmwareBuildTime`
- `firmwareFeatures`

## Interpretation

- `FW_INFO` present, but `HID_STATUS` absent:
  - the firmware runtime is alive, but the HID diagnostics path is not being executed
- `HID_STATUS` present, but `HID_TEST` absent:
  - forced HID test path is not being entered
- `HID_TEST sent=false`:
  - `tud_hid_ready()` or `tud_hid_report()` is failing
- `HID_TEST sent=true`, but `HidConsoleProbe` still reads nothing:
  - HID host receive path or interface handling remains suspect

## Next Step

Use the trace to decide whether the next fix belongs in:

- firmware runtime
- USB / TinyUSB send path
- host-side HID reading