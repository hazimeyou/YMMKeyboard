# Matrix Key Detection Validation RC1

## Goal

Verify whether physical key presses are recognized by the matrix scan probe as `MATRIX_KEY` events.

## Setup

- Firmware: `matrix-scan-probe-rc1`
- Layout source: `firmware/src/RP2040ZeroCode/code.py`
- Columns: `GP2, GP8, GP7, GP6, GP5, GP4, GP3`
- Rows: `GP28, GP27, GP26, GP15, GP14, GP29`
- Diode orientation: `COL2ROW`

## Capture Window

- CDC trace: `tools/YMMKeyboard.CdcTraceCapture -- --port COM12 --duration-sec 60`
- HID probe: `tools/YMMKeyboard.HidConsoleProbe -- --vid 2E8A --pid 4020 --timeout-ms 500 --duration-sec 60`

## User Input Attempted

During the capture window, several keys around the corners and center were pressed in press/release cycles.

## Observed CDC

- `MATRIX_SCAN cols=7 rows=6`: observed repeatedly
- `MATRIX_EDGE`: not observed
- `MATRIX_KEY`: not observed
- `MATRIX_HID`: not observed

## Observed HID

- `OpenSucceeded=True`: yes
- `ReadLoopStarted=True`: yes
- `ReadSuccessCount=0`: yes
- `K_*`: not observed
- `SW_*`: not observed
- `TEST_HID_*`: not observed

## Conclusion

The scan loop is running, but this capture did not confirm any matrix coordinate change. That means the current blocker is upstream of HID send: either the pressed keys did not map to the expected scan coordinates, or the physical matrix contact was not detected by the current scan conditions.

## Next Step

Try a more targeted matrix-key probe:

1. Press and hold a single known physical key longer.
2. Observe whether any `MATRIX_EDGE` appears.
3. If no edge appears, probe scan direction / pull-up / row-column assumptions more directly.
