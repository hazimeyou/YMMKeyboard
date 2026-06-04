# Matrix Scan Probe RC1

## Purpose

Validate the real KMK/CircuitPython matrix layout from `code.py` against the TinyUSB firmware.

This replaces the earlier standalone `GPIO29` probe, which was not correct because `GPIO29` is a matrix row line.

## Source Layout

- Columns: `GP2, GP8, GP7, GP6, GP5, GP4, GP3`
- Rows: `GP28, GP27, GP26, GP15, GP14, GP29`
- Diode orientation: `COL2ROW`

## Firmware Target

- `FW_VERSION=matrix-scan-probe-rc1`
- `FW_FEATURES=FW_INFO,HID_STATUS,MATRIX_SCAN,MATRIX_KEY,MATRIX_HID`

## Scan Strategy

- Columns are driven as outputs.
- Rows are inputs with pull-ups.
- Pressed state is active-low.
- One column is driven low at a time and all rows are sampled.
- Matrix coordinates are reported as `K_<row>_<col>`.

## CDC Logs

Expected CDC events:

- `MATRIX_SCAN cols=7 rows=6`
- `MATRIX_EDGE row=<r> col=<c> pressed=<true/false> keyId=K_<r>_<c>`
- `MATRIX_KEY row=<r> col=<c> pressed=<true/false> keyId=K_<r>_<c>`
- `MATRIX_HID row=<r> col=<c> pressed=<true/false> payload=K_<r>_<c>`

## HID Payload

- Host-facing HID payload is the same `K_<r>_<c>` string.
- Host classification should treat `K_*` as matrix traffic, not `SW_*`.

## Current State

- The matrix scan probe code has been added to firmware.
- The host probe classification has been updated to recognize `K_*` as `MATRIX`.
- Flash / runtime validation still needs to be performed once `RPI-RP2` is visible again.

## Next Validation

1. Flash one board only when `RPI-RP2` appears.
2. Run `CdcTraceCapture` and press matrix keys.
3. Run `HidConsoleProbe` and confirm `K_*` reports.
4. Decide whether the issue is wiring, scan logic, debounce, or HID transport.

## Latest Validation Attempt

- `MATRIX_SCAN cols=7 rows=6`: observed
- `MATRIX_EDGE`: not observed
- `MATRIX_KEY`: not observed
- `MATRIX_HID`: not observed
- `HidConsoleProbe` `ReadSuccessCount`: `0`
- `K_*`: not observed

This means the probe infrastructure is running, but the current key press set did not produce a detectable matrix transition yet.

## Follow-up

The next step is the electrical probe in `docs/matrix-electrical-probe.md`, which logs row state and candidate transitions directly.
