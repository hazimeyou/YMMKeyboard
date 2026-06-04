# KMK Matrix Analysis RC1

## Source Files

- `firmware/src/RP2040ZeroCode/code.py`
- `firmware/src/RP2040ZeroCode/boot.py`

## Matrix Structure

- Rows: `GP28, GP27, GP26, GP15, GP14, GP29`
- Columns: `GP2, GP8, GP7, GP6, GP5, GP4, GP3`
- Diode orientation: `COL2ROW`

## KMK Keyboard Setup

- `KMKKeyboard()` is used.
- `keyboard.col_pins` and `keyboard.row_pins` are explicitly assigned.
- `keyboard.coord_mapping` is a 35-switch matrix map.
- `keyboard.keymap` is set to a single layer of `SERIAL_ONLY`, so normal keyboard HID output is intentionally suppressed in favor of the custom serial/HID event path.

## coord_mapping

The mapping table is arranged in scan order with gaps in the numeric switch IDs:

- Row 0: `1, 2, 3, 4, 5, 6`
- Row 1: `8, 9, 10, 11, 12, 13`
- Row 2: `15, 16, 17, 18, 19, 20`
- Row 3: `22, 23, 24, 25, 26, 27`
- Row 4: `29, 30, 31, 32, 33, 34`
- Row 5: `35`

## Keymap

- The top layer maps all 35 matrix positions to `SERIAL_ONLY`.
- That means the physical matrix is not supposed to emit normal keyboard HID keycodes through KMK.
- The custom `emit_event()` path is the intended observable path.

## Encoder

- `GP0`, `GP1`
- `ENC_CW` emits switch `36`
- `ENC_CCW` emits switch `37`

## Conclusion

The KMK layout is definitely matrix-based and not a single-pin button layout.
The correct firmware probe must watch matrix row/column transitions, not only a standalone `GPIO29` level.

## Direction Note

The current TinyUSB validation phase is using `matrix-direction-validation-rc2` to confirm the idle row snapshot per driven column before re-attempting any key press validation.
The latest firmware build has been regenerated for the one-column-per-tick scan cadence, and the next step is to re-flash one board and re-take the idle snapshot capture.
