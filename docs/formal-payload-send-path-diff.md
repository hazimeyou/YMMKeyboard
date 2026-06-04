# Formal Payload Send Path Diff RC1

## Summary

The matrix formal payload path now sends `K_<row>_<col>:P/R` with a fixed HID report length of 63 bytes, matching the successful variant-per-press transport shape.

## Difference Identified

- Variant-per-press used a 63-byte report payload buffer and host received all variants.
- Formal payload was previously sending the string length only, which produced `reportLength=7` for payloads like `K_0_1:P`.
- That mismatch is now removed in firmware:
  - the payload string remains short
  - the HID report length is fixed to 63 bytes
  - the report ID remains `1`

## Current Expectation

- `payload` example: `K_0_1:P`
- `stringLength`: `7`
- `reportLength`: `63`
- on-wire HID report length: `64` including report ID

## Next Check

Re-run CDC + HidConsoleProbe and confirm that the fixed-length formal payload is visible on the host as `K_COLON`.

## Validation Result

- The fixed-length formal payload was observed on the host probe.
- `K_0_1:P` and `K_0_1:R` were received with `classification=K_COLON`.
- This confirms that the earlier `reportLength=7` path was the mismatch, not the `K_*` payload shape itself.
