# Matrix Reverse Direction Probe RC1

## Purpose

Confirm whether the matrix electrical direction should be treated as row-driven / column-read by observing idle and pressed-state column snapshots.

## Layout Under Test

- Rows: `GP28, GP27, GP26, GP15, GP14, GP29`
- Columns: `GP2, GP8, GP7, GP6, GP5, GP4, GP3`
- Reverse probe model: `ROW=output / COL=input pull-up`

## Firmware Version

- `FW_VERSION=matrix-reverse-direction-probe-rc1`
- `FW_FEATURES=FW_INFO,HID_STATUS,REV_ROW_CONFIG,REV_COL_CONFIG,REV_SCAN_FRAME,REV_COL_EDGE,REV_MATRIX_CANDIDATE`

## Expected CDC Output

- `REV_ROW_CONFIG GP28 OUTPUT`
- `REV_ROW_CONFIG GP27 OUTPUT`
- `REV_ROW_CONFIG GP26 OUTPUT`
- `REV_ROW_CONFIG GP15 OUTPUT`
- `REV_ROW_CONFIG GP14 OUTPUT`
- `REV_ROW_CONFIG GP29 OUTPUT`
- `REV_COL_CONFIG GP2 INPUT_PULLUP`
- `REV_COL_CONFIG GP8 INPUT_PULLUP`
- `REV_COL_CONFIG GP7 INPUT_PULLUP`
- `REV_COL_CONFIG GP6 INPUT_PULLUP`
- `REV_COL_CONFIG GP5 INPUT_PULLUP`
- `REV_COL_CONFIG GP4 INPUT_PULLUP`
- `REV_COL_CONFIG GP3 INPUT_PULLUP`
- `REV_SCAN_FRAME ROW=<0-5> COLS=<7 bits>`

## Scan Cadence

The firmware advances one row per scan tick, emitting one `REV_SCAN_FRAME` line per driven row so the idle column snapshot is easier to observe line by line.

## Current Firmware State

- The latest firmware build has been regenerated after the reverse-direction adjustment.
- The 1-board flash has been performed on the current reverse probe image.
- A 60-second capture was taken after flash with a sequence of long key presses.

## Latest Runtime Result

- `REV_ROW_CONFIG` observed
- `REV_COL_CONFIG` observed
- `REV_SCAN_FRAME` observed
- Idle `COLS` was consistently `1111111` across rows
- `REV_COL_EDGE` observed: `28`
- `REV_MATRIX_CANDIDATE` observed: `14`

## Observed Row/Col Activity

The pressed-key capture produced edges and candidates at these coordinates:

- `row=0 col=1`
- `row=0 col=4`
- `row=0 col=6`
- `row=1 col=6`
- `row=4 col=1`

## Interpretation

- The reverse pull-up path is alive because idle `COLS` is stable at `1111111`.
- The matrix is seeing real electrical transitions in reverse mode.
- The next step is to map these row/col coordinates to physical keys and compare them against the KMK layout.

## Next Validation

1. Correlate the observed row/col coordinates with the physical key positions in the matrix layout.
2. Reconnect the reverse-direction probe findings to `MATRIX_KEY` / HID path validation.

## Outcome Targets

- Best: pressed keys produce stable, repeatable `REV_COL_EDGE` and `REV_MATRIX_CANDIDATE` coordinates.
- Acceptable: transitions are visible and reproducible even if the physical key-to-coordinate mapping still needs one more pass.
- Blocker: no `REV_SCAN_FRAME` or no `REV_ROW_CONFIG` / `REV_COL_CONFIG`.
