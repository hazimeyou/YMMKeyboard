# Matrix HID Minimal Send Probe RC1

## Purpose

Reduce the matrix HID path to the same `TEST_HID_` prefix that the host already reads successfully, so we can isolate whether the problem is payload shape, timing, or the matrix path itself.

## Probe Rules

- Use the canonical matrix scan direction
- On press only, send `TEST_HID_KEY_<counter>`
- Send the same 63-byte zero-padded payload 3 times with a short delay
- Do not send a HID release report
- Keep CDC logging for both the matrix event and the HID summary

## Expected Signals

- CDC:
  - `MATRIX_KEY row=<r> col=<c> keyId=K_<r>_<c> state=P sent=true`
  - `MATRIX_HID_MINIMAL row=<r> col=<c> keyId=K_<r>_<c> state=P payload=TEST_HID_KEY_<counter> attempts=3 successCount=<n> sent=<true/false>`
- HID host:
  - `TEST_HID_*`

## Goal

Confirm whether a matrix-triggered `TEST_HID_` payload is visible on the host.

## Latest Result

- `TEST_HID_KEY_0001` to `TEST_HID_KEY_0004` were observed on the host as `TEST_HID_*` reports.
- `readSuccessCount > 0` on the host probe.
- This means the matrix-triggered minimal test payload is visible on the host.
- The remaining difference versus the original `K_*` path is now narrowed to payload format and/or when we switch back from the minimal test prefix to the original matrix payload.

## Stepdown Follow-up

- The next probe steps down the payload family in order:
  - `TEST_HID_KEY_0001`
  - `TEST_KEY_0001`
  - `KEY_0001`
  - `K_2_2_P`
  - `K_2_2:P`
- The goal is to find the first payload shape that stops being received or classified on the host.
