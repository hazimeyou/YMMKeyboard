# Matrix Input Formal Payload RC1

## Purpose

Restore the matrix input path to the formal payload shape `K_<row>_<col>:P/R` and confirm that both press and release are visible on the host.

## Formal Payload

- Press: `K_<row>_<col>:P`
- Release: `K_<row>_<col>:R`
- HID report length is fixed to 63 bytes, even when the payload string itself is shorter.

## Rules

- Send only on stable state changes.
- Do not send duplicate reports for the same state.
- Keep CDC logging for both `MATRIX_KEY` and the formal payload line.

## Goal

Confirm that the formal matrix payload is observable on the host probe as `K_COLON`.

## Validation Result

- `K_<row>_<col>:P/R` was observed on the host as `K_COLON`.
- The working run used a fixed 63-byte HID report length.
- This closes the `reportLength=7` hypothesis for the formal payload path.

## Next Phase

- The next live check is plugin-side `InputReceived`.
- That follow-up is documented in [Plugin InputReceived RC1](./plugin-inputreceived-rc1.md).
