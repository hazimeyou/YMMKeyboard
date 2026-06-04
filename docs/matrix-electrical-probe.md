# Matrix Electrical Probe RC1

## Goal

Observe matrix electrical behavior directly from TinyUSB firmware and relate it to the KMK layout in `code.py`.

## Firmware Version

- `FW_VERSION=matrix-reverse-direction-probe-rc1`
- `FW_FEATURES=FW_INFO,HID_STATUS,REV_ROW_CONFIG,REV_COL_CONFIG,REV_SCAN_FRAME,REV_COL_EDGE,REV_MATRIX_CANDIDATE`

## Scan Model

- Rows are outputs.
- Columns are inputs with pull-ups.
- Active-low detection is used.
- One row is driven low at a time.
- The current validation build emits one `REV_SCAN_FRAME` per scan tick so the idle column snapshot can be read per row.

## CDC Output

Expected lines:

- `REV_ROW_CONFIG GP<xx> OUTPUT`
- `REV_COL_CONFIG GP<xx> INPUT_PULLUP`
- `REV_SCAN_FRAME ROW=<0-5> COLS=<7 bits>`

## Interpretation

- `REV_SCAN_FRAME` is the per-row column snapshot.
- If idle `COLS` changes by row, the reverse scan is alive and directional.
- If all rows look identical, the pull-up / direction / wiring model still needs adjustment.

## Current State

- The latest build has already been regenerated for this phase.
- The current reverse-direction capture showed stable idle `COLS=1111111` across rows.
- The pressed capture produced `REV_COL_EDGE` and `REV_MATRIX_CANDIDATE` entries, so the reverse scan is electrically responsive.

## Observed Activity

Observed row/col transitions during the pressed capture included:

- `row=0 col=1`
- `row=0 col=4`
- `row=0 col=6`
- `row=1 col=6`
- `row=4 col=1`

## Next Validation

1. Map the observed row/col coordinates back to physical key positions.
2. Compare those coordinates against the KMK `coord_mapping` table.
3. Use the reverse-direction model as the canonical matrix scan path for the next phase.

## Outcome Targets

- At minimum: `REV_ROW_CONFIG` and `REV_COL_CONFIG` are visible
- Preferred: `REV_SCAN_FRAME` differs by row
- Best case: the observed idle pattern matches the expected reverse matrix direction model
