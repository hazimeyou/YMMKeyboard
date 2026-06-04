# Matrix HID Payload Stepdown RC1

## Purpose

Step the matrix-triggered HID payload down from the known-working `TEST_HID_` family toward the original `K_*` family and identify the first format that fails on the host.

## Payload Sequence

- Press 1: `TEST_HID_KEY_0001`
- Press 2: `TEST_KEY_0001`
- Press 3: `KEY_0001`
- Press 4: `K_2_2_P`
- Press 5: `K_2_2:P`
- Press 6: `TEST_HID_KEY_0002`

## Per-Press Rule

- Emit exactly one variant per press.
- Release does not send HID data.
- After press 5, wrap back to A with the next counter value.

## Probe Rules

- On each matrix press, emit exactly one payload variant.
- Keep CDC logging for the matrix event and per-press send result.
- Keep the host probe classification aligned with the payload families above.

## Goal

Determine whether the host still receives the report as the payload moves from `TEST_HID_` to `TEST_KEY_`, `KEY_`, and finally `K_*`.
