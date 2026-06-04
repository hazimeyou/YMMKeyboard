# Matrix HID Payload Variant Per Press RC1

## Purpose

Send exactly one payload variant per matrix press so we can step from the known-working `TEST_HID_` family down toward the original `K_*` payload shape.

## Variant Order

- Press 1 -> A `TEST_HID_KEY_0001`
- Press 2 -> B `TEST_KEY_0001`
- Press 3 -> C `KEY_0001`
- Press 4 -> D `K_2_2_P`
- Press 5 -> E `K_2_2:P`
- Press 6 -> A `TEST_HID_KEY_0002`

## Probe Rule

- On press only, send one payload variant.
- Release does not send HID data.
- Keep CDC logging for `PAYLOAD_VARIANT_PRESS` and `MATRIX_KEY`.

## Goal

Determine the first payload family that stops being received or classified on the host.

## Latest Result

- Press 1 -> `TEST_HID_KEY_0001` observed as `TEST_HID`
- Press 2 -> `TEST_KEY_0001` observed as `TEST_KEY`
- Press 3 -> `KEY_0001` observed as `KEY`
- Press 4 -> `K_2_2_P` observed as `K_UNDERSCORE`
- Press 5 -> `K_2_2:P` observed as `K_COLON`
- `readSuccessCount > 0`
- The host received all five payload variants in this run.
