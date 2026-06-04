# Button Path Validation RC1

## Goal

Confirm whether the real button input `Key1` reaches the host as an HID `SW_00` report.

## Known Good Baseline

- `FW_VERSION=lifecycle-diagnostics-rc1`
- `HID_STATUS` observed
- `HID_TEST` observed
- forced `TEST_HID_*` host receive success
- CDC/HID simultaneous visibility success
- `bothPresentCount=17`

## Scope

This phase focuses only on the button report path:

`Key1` -> `SW_00` -> HID report -> host receive

Descriptor investigations are paused for this phase.

## Hardware Mapping

- `GPIO29`: row line in the matrix, not a standalone button input
- `col_pins`: `GP2, GP8, GP7, GP6, GP5, GP4, GP3`
- `row_pins`: `GP28, GP27, GP26, GP15, GP14, GP29`
- `diode_orientation`: `COL2ROW`

## Capture Locations

- `tmp/cdc-trace-capture/`
- `tmp/button-hid-trace/`

## How to Run

1. Start `CdcTraceCapture` against `COM12`.
2. Start `HidConsoleProbe` against `VID=2E8A PID=4020` with `--output-dir tmp/button-hid-trace`.
3. Press `Key1` two or three times during the capture window.

Example:

```powershell
dotnet run --project tools/YMMKeyboard.CdcTraceCapture -- --port COM12 --duration-sec 30
dotnet run --project tools/YMMKeyboard.HidConsoleProbe -- --vid 2E8A --pid 4020 --timeout-ms 500 --duration-sec 30 --output-dir tmp/button-hid-trace
```

## What to Record

CDC side:

- `SW_00`
- `HID_DIAG:button=SW_00`
- `sendResult=true/false`

HID side:

- `timestamp`
- `length`
- `classification`
- `ascii`
- `hex`

## Classification Rules

- `TEST_HID_*` -> `TEST_HID`
- `SW_00` / `YMMK:...:SW_00` -> `SW`
- everything else -> `OTHER`

## Current Conclusion

The forced test path is confirmed end to end.

The standalone `GPIO29` probe was not the correct model for `Key1` because `GPIO29` is part of the matrix row scan. The focus now moves from single-pin probing to matrix scan probing. The latest shared capture before this change showed:

- CDC observed `SW_00` and `HID_DIAG` send success for the periodic diagnostic traffic
- host HID traffic still classified only `TEST_HID_*`
- no host-side matrix `K_*` report was observed yet

The firmware has now been switched to a matrix probe so we can confirm the first matrix switch directly.

## Latest Attempt Result

- `FW_VERSION=gpio29-state-probe-rc1`: observed on the previous build
- `FW_FEATURES=FW_INFO,HID_STATUS,GPIO_DIAG,GPIO_EDGE,GPIO_STABLE,BUTTON_GPIO29`: observed on the previous build
- CDC `SW_00`: not observed in the latest button-path capture
- HID `SW_*`: not observed in the latest button-path capture
- HID `TEST_HID_*`: not observed in the latest button-path capture

The latest run therefore did not yet confirm a live button press reaching the HID path, which is why we are moving to a matrix scan probe based on the `code.py` layout.

## Next Step

Run the matrix scan probe on real hardware, then decide whether the issue is wiring, row/column selection, debounce, or HID transport.
