# Matrix Direction Validation RC2

## Purpose

This phase established the baseline forward scan behavior and served as the comparison point for the later reverse-direction probe.

## Layout

- Columns: `GP2, GP8, GP7, GP6, GP5, GP4, GP3`
- Rows: `GP28, GP27, GP26, GP15, GP14, GP29`
- Orientation: `COL2ROW`

## Firmware Version

- `FW_VERSION=matrix-direction-validation-rc2`
- `FW_FEATURES=FW_INFO,HID_STATUS,ROW_CONFIG,COL_CONFIG,SCAN_FRAME`

## Expected CDC Output

- `ROW_CONFIG GP28 INPUT_PULLUP`
- `ROW_CONFIG GP27 INPUT_PULLUP`
- `ROW_CONFIG GP26 INPUT_PULLUP`
- `ROW_CONFIG GP15 INPUT_PULLUP`
- `ROW_CONFIG GP14 INPUT_PULLUP`
- `ROW_CONFIG GP29 INPUT_PULLUP`
- `COL_CONFIG GP2 OUTPUT`
- `COL_CONFIG GP8 OUTPUT`
- `COL_CONFIG GP7 OUTPUT`
- `COL_CONFIG GP6 OUTPUT`
- `COL_CONFIG GP5 OUTPUT`
- `COL_CONFIG GP4 OUTPUT`
- `COL_CONFIG GP3 OUTPUT`
- `SCAN_FRAME COL=<0-6> ROWS=<6 bits>`

## Scan Cadence

The firmware advances one column per scan tick, emitting one `SCAN_FRAME` line per driven column so the idle row snapshot is easier to observe line by line.

## Current Firmware State

- The latest firmware build has been regenerated after the one-column-per-tick adjustment.
- The phase is superseded by the reverse-direction decision, but the log remains useful as the forward-direction comparison baseline.

## Interpretation

- If `ROWS` changes by column, the scan is alive and directionally meaningful.
- In the recorded baseline run, all columns reported `ROWS=000000`, which is why the reverse-direction probe was introduced.
- This phase does not require key presses.

## Next Validation

1. Compare the forward baseline against the reverse-direction probe.
2. Use the reverse-direction result as the candidate canonical scan model.
3. Proceed to `matrix-input-rc1` only after the row/col mapping is fixed.

## Outcome Targets

- Best: different `ROWS` patterns across columns
- Acceptable: stable `ROWS` that match the expected idle state
- Blocker: no `SCAN_FRAME` or no `ROW_CONFIG` / `COL_CONFIG`
