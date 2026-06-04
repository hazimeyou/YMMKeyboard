# Matrix HID Host Correlation RC1

## Purpose

Confirm whether the canonical matrix input path in `matrix-input-rc1` is visible on the host as `K_*` HID reports.

## Context

- Firmware version: `matrix-input-rc1`
- Matrix detection was already confirmed on CDC with `MATRIX_KEY`
- HID send attempts and `tud_hid_ready()` were already confirmed on firmware

## Run Info

- Date: 2026-06-04
- Operator: Codex
- Board count: 1
- `VID/PID`: `2E8A:4020`
- `--timeout-ms`: `500`
- `--duration-sec`: `60`

## CDC Observation

- `MATRIX_KEY` was observed in the CDC trace
- Example:
  - `MATRIX_KEY row=2 col=2 keyId=K_2_2 state=P sent=true`
- `HID_DIAG` also reported `sendResult=true`
- Firmware-side counters increased as expected:
  - `hidSendAttemptCount`
  - `hidReadyTrueCount`
  - `hidReportCallCount`

## HID Probe Observation

- `openSucceeded`: `true`
- `readLoopStarted`: `true`
- `readAttemptCount`: `119`
- `readSuccessCount`: `0`
- `readTimeoutCount`: `119`
- `firstReportKind`: empty
- `K_*` reports: not observed
- `MATRIX:K_*` reports: not observed
- `SW_*` reports: not observed

## Conclusion

- Firmware-side matrix detection and send attempts are working.
- This host-side correlation run did not receive any HID report at all.
- For this run, the host result is `no_report_received`.
- The next step is to re-check the exact HID payload path or repeat the capture with a smaller key set if needed.

## Next Minimal Probe

- The next run will use `TEST_HID_MATRIX_<counter>` on matrix press only.
- That probe is documented in [Matrix HID Minimal Send Probe RC1](./matrix-hid-minimal-send-probe.md).

## Payload Stepdown Follow-up

- After the minimal probe, the payload family is stepped down toward the original matrix shapes.
- The new sequence is documented in [Matrix HID Payload Stepdown RC1](./matrix-hid-payload-stepdown.md).

## Variant Per Press

- The payload stepdown now sends exactly one variant per press.
- The per-press sequence is documented in [Matrix HID Payload Variant Per Press RC1](./matrix-hid-payload-variant-per-press.md).

## Latest Variant Per Press Result

- All five payload variants were received on the host in order.
- Observed report kinds:
  - `TEST_HID`
  - `TEST_KEY`
  - `KEY`
  - `K_UNDERSCORE`
  - `K_COLON`
- This confirms that the original `K_*` family is visible to the host when sent one variant per press.

## Formal Payload Follow-up

- The matrix input path now returns to the formal payload `K_<row>_<col>:P/R`.
- The formal payload flow is documented in [Matrix Input Formal Payload RC1](./matrix-input-formal-payload-rc1.md).
- The formal payload send path now keeps the HID report length fixed at 63 bytes to match the working transport shape from the variant-per-press probe.

## Minimal Payload Update

- The minimal probe now uses `TEST_HID_KEY_<counter>`.
- The payload is zero-padded to 63 bytes before `tud_hid_report()` is called.
- The goal is to keep the matrix-triggered payload as close as possible to the forced test transport shape.

## Latest Minimal Probe Result

- The latest minimal probe run received `TEST_HID_KEY_<counter>` on the host.
- This confirms that the matrix press path can produce host-visible HID traffic when the payload is forced into the `TEST_HID_` family.
- The original `K_*` payload path remains the next thing to restore incrementally.
