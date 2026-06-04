# Multiple Key Mapping RC1

## Goal

Expand the confirmed single-key baseline into a small multi-key mapping set, while keeping the `K_r_c` payload naming and the existing HID transport path unchanged.

## Live Device

- Device UID: `2c307c903f53b257`
- Matrix scan: reverse-direction canonical path from the current working baseline
- Payload format: `K_<row>_<col>:P/R`

## Current Mapping Plan

The current live device mapping is configured in row-major switch order:

| Matrix key | Switch | Action |
|---|---:|---|
| `K_0_1` | `SW02` | `A` |
| `K_0_4` | `SW05` | `B` |
| `K_0_6` | `SW07` | `D` |
| `K_1_6` | `SW14` | `E` |
| `K_4_1` | `SW30` | `C` |

## Status

- Live settings updated for the confirmed device UID.
- The mapping is limited to the currently measured matrix positions.
- Key label translation is not being introduced yet.

## Intended Validation

Each mapped key should be pressed three times and checked for:

- `InputReceived`
- `InputMapped`
- `DispatchPrepared`
- `DispatchExecuted`
- actual YMM UI action

## Known Limitations

- This phase does not add macro behavior.
- This phase does not change firmware structure.
- This phase does not introduce key-label translation beyond the explicit `K_r_c` matrix names.
- Only the measured subset of keys is being mapped first.

