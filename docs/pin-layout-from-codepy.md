# Pin Layout Audit from `code.py`

## Source Files

- `firmware/src/RP2040ZeroCode/code.py`
- `firmware/src/RP2040ZeroCode/boot.py`

No other Python files were present in `firmware/src/RP2040ZeroCode/` at the time of audit.

## Pin Summary

### Matrix

- `keyboard.col_pins = (board.GP2, board.GP8, board.GP7, board.GP6, board.GP5, board.GP4, board.GP3)`
- `keyboard.row_pins = (board.GP28, board.GP27, board.GP26, board.GP15, board.GP14, board.GP29)`
- `keyboard.diode_orientation = DiodeOrientation.COL2ROW`

### Encoder

- `encoder_handler.pins = ((board.GP0, board.GP1, None, False),)`
- `ENC_CW` emits switch `36`
- `ENC_CCW` emits switch `37`

### Other I/O

- No explicit `NeoPixel`, `I2C`, `SPI`, or `UART` pin assignments were found in `code.py`
- `boot.py` did not add any additional pin assignments during this audit

## Key1 Candidate

The `code.py` file uses a 7x6 matrix and a `coord_mapping` table for switch IDs `1-35`.

The first switch ID in the mapping table is `1`, which corresponds to the first matrix coordinate in KMK scan order. The code does not label that coordinate as a direct standalone GPIO input.

## GPIO29 Correctness

- `GPIO29` is present in `keyboard.row_pins`
- `GPIO29` is therefore part of the matrix scan, not a dedicated single-button input
- The earlier standalone `GPIO29` raw-input probe was not the correct model for `Key1`

## TinyUSB Comparison

Current TinyUSB firmware probe:

- uses `BUTTON_GPIO = 29`
- treats `GPIO29` as a standalone active-low button

That is a mismatch with the KMK/CircuitPython layout, where `GPIO29` participates in row scanning.

## Recommended Next Probe

Replace the standalone GPIO29 probe with a minimal matrix scan probe that:

- drives `col_pins = GP2, GP8, GP7, GP6, GP5, GP4, GP3`
- reads `row_pins = GP28, GP27, GP26, GP15, GP14, GP29`
- detects the first matrix coordinate corresponding to switch `1`

The current firmware target for this work is `matrix-scan-probe-rc1`.

## Latest Result

- `MATRIX_SCAN` is active in firmware
- `MATRIX_KEY` has not yet been observed during the current capture
- `GPIO29` remains a matrix row line, not a standalone key input

The next cut is to probe a smaller, more targeted matrix transition rather than treating the board as a single input.

The new target is `matrix-electrical-probe-rc1`, which logs `ROW_STATE`, `ROW_EDGE`, and `MATRIX_CANDIDATE` directly.

## Conclusion

`GPIO29` is not the whole story. It is only one row line in the matrix.

The next firmware probe should be a matrix scan probe, not a single-pin GPIO probe.
