# Matrix Electrical Probe RC1

## Goal

Observe matrix electrical behavior directly from TinyUSB firmware and relate it to the KMK layout in `code.py`.

## Firmware Version

- `FW_VERSION=matrix-direction-validation-rc2`
- `FW_FEATURES=FW_INFO,HID_STATUS,ROW_CONFIG,COL_CONFIG,SCAN_FRAME`

## Scan Model

- Columns are outputs.
- Rows are inputs with pull-ups.
- Active-low detection is used.
- One column is driven low at a time.
- The current validation build emits one `SCAN_FRAME` per scan tick so the idle row snapshot can be read per column.

## CDC Output

Expected lines:

- `ROW_CONFIG GP<xx> INPUT_PULLUP`
- `COL_CONFIG GP<xx> OUTPUT`
- `SCAN_FRAME COL=<0-6> ROWS=<6 bits>`

## Interpretation

- `SCAN_FRAME` is the per-column row snapshot.
- If `ROWS` changes by column, the scan is alive and directional.
- If all columns look identical, the pull-up / direction / wiring model still needs adjustment.

## Next Validation

1. Flash one board only.
2. Run `CdcTraceCapture` for 60 seconds.
3. Do not press any keys.
4. Inspect `SCAN_FRAME` across all columns.

## Current State

- The latest build has already been regenerated for this phase.
- The remaining step is the next BOOTSEL flash and re-run of the idle capture.

## Outcome Targets

- At minimum: `ROW_CONFIG` and `COL_CONFIG` are visible
- Preferred: `SCAN_FRAME` differs by column
- Best case: the observed idle pattern matches the expected matrix direction model
