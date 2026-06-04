# Matrix Row/Col Mapping RC1

## Purpose

Map the reverse-direction electrical probe results back to the KMK `coord_mapping` ordering from `code.py`.

## KMK Reference

- Matrix rows: `GP28, GP27, GP26, GP15, GP14, GP29`
- Matrix columns: `GP2, GP8, GP7, GP6, GP5, GP4, GP3`
- Diode orientation: `COL2ROW`
- `coord_mapping` is a 35-switch flat list in scan order.

## Observed Reverse Probe Coordinates

The following coordinates were observed during the reverse-direction capture:

- `row=0 col=1`
- `row=0 col=4`
- `row=0 col=6`
- `row=1 col=6`
- `row=4 col=1`

## Mapping Table

| observed row | observed col | KMK mapping | key label | physical position | note |
| -----------: | -----------: | ----------- | --------- | ----------------- | ---- |
| 0 | 1 | `SW_02` | unknown | first matrix row, second active position | repeatable edge/candidate pair |
| 0 | 4 | `SW_05` | unknown | first matrix row, fifth active position | repeatable edge/candidate pair |
| 0 | 6 | unassigned / not confirmed in KMK audit | unknown | electrical top-right column line | electrically real, KMK label not yet confirmed |
| 1 | 6 | unassigned / not confirmed in KMK audit | unknown | second matrix row, last electrical column | electrically real, KMK label not yet confirmed |
| 4 | 1 | `SW_30` | unknown | fifth matrix row, second active position | repeatable edge/candidate pair |

## Notes

- The `SW_xx` values above are derived from the KMK scan-order list in `code.py`.
- The reverse probe confirms these coordinates are electrically reachable.
- The positions labeled "unassigned / not confirmed" are valid electrical positions, but we have not yet proven a stable KMK switch label for them from the current evidence.

## Conclusion

The reverse-direction probe is aligned with the KMK matrix model well enough to proceed to a `matrix-input-rc1` phase, but the remaining work is to translate the observed coordinates into the final `MATRIX_KEY` naming and HID path.
