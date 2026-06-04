# HID Console Probe Result

## Current State

- latest correlation run completed
- YMM4 / Plugin were not used
- forced `TEST_HID_*` traffic was received by the host
- this run did not surface any `SW_*` HID reports in the probe output

## Run Info

- Date: 2026-06-04
- Operator: Codex
- Board count: 1
- `VID/PID`: `2E8A:4020`
- `--index`: `0`
- `--timeout-ms`: `500`
- `--duration-sec`: `30`

## Device Enumeration

- `path`: `\\?\hid#vid_2e8a&pid_4020&mi_02#9&a1bc4f3&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}`
- `productName`: `YMM Control HID`
- `manufacturer`: `YMMKeyboard`
- `serial`: `504434042060791C`
- `usagePage`: `0x0000`
- `usage`: `0x0000`
- `maxInputReportLength`: `64`
- `maxOutputReportLength`: `64`
- `maxFeatureReportLength`: `0`

## Open Result

- `selectedPath`: `\\?\hid#vid_2e8a&pid_4020&mi_02#9&a1bc4f3&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}`
- `openSucceeded`: `true`
- `readLoopStarted`: `true`

## Read Result

- `readAttemptCount`: `61`
- `readSuccessCount`: `31`
- `readTimeoutCount`: `30`
- `lastException`: `TimeoutException`

## First Report

- `firstReportKind`: `TEST_HID`
- `firstReportLength`: `64`
- `firstReportHex`: `01 54 45 53 54 5F 48 49 44 5F 35 37 35 30`
- `firstReportAscii`: `TEST_HID_5750`

## Report Kind Summary

- `TEST_HID`: `31`
- `SW`: `0`
- `OTHER`: `0`

## Report Dump

```text
report_received
```

## Conclusion

- `readSuccessCount > 0`
- the standalone console probe received HID reports
- the received reports in this run were `TEST_HID_*`
- no `SW_*` HID report was classified by the probe in this run
- the firmware-to-host HID send path is working for the forced test report
- the button-triggered `SW_00` path is still not confirmed on the HID probe side

## Latest Matrix Correlation

- The forced `TEST_HID_*` path remains confirmed.
- In the latest matrix correlation run, `K_*` was not received; `readSuccessCount=0` and `firstReportKind` remained empty.
- The latest matrix host result is captured in [Matrix HID Host Correlation RC1](./matrix-hid-host-correlation.md).

## Matrix Minimal Probe Note

- The current host probe already classifies `TEST_HID_*` traffic.
- The next matrix probe uses `TEST_HID_MATRIX_<counter>` so it can be observed with the same classification path.
- If the next run still shows `readSuccessCount=0`, the issue is not the prefix classification.

## Latest Matrix Minimal Probe

- The latest matrix minimal probe run received `TEST_HID_KEY_<counter>` reports on the host.
- `readSuccessCount > 0`.
- The probe classified these as `TEST_HID`.
- This is a strong indication that the host can receive matrix-triggered HID traffic when the payload is shaped like the forced test path.

## Payload Stepdown Plan

- The next probe steps the payload family down in this order:
  - `TEST_HID_KEY_0001`
  - `TEST_KEY_0001`
  - `KEY_0001`
  - `K_2_2_P`
  - `K_2_2:P`
- The probe classification now separates:
  - `TEST_HID`
  - `TEST_KEY`
  - `KEY`
  - `K_UNDERSCORE`
  - `K_COLON`
  - `OTHER`

## Variant Per Press Plan

- The latest probe sends one variant per press.
- The per-press sequence is:
  - press 1 -> `TEST_HID_KEY_0001`
  - press 2 -> `TEST_KEY_0001`
  - press 3 -> `KEY_0001`
  - press 4 -> `K_2_2_P`
  - press 5 -> `K_2_2:P`

## Latest Variant Per Press Result

- The host received all five payload variants.
- Report kinds observed:
  - `TEST_HID`
  - `TEST_KEY`
  - `KEY`
  - `K_UNDERSCORE`
  - `K_COLON`
- `readSuccessCount > 0` for the run.

## Formal Payload Follow-up

- The matrix input path now returns to the formal payload `K_<row>_<col>:P/R`.
- The formal payload flow is documented in [Matrix Input Formal Payload RC1](./matrix-input-formal-payload-rc1.md).
- The formal payload send path now keeps the HID report length fixed at 63 bytes so it matches the working transport shape from the variant-per-press probe.
- The diff is documented in [Formal Payload Send Path Diff RC1](./formal-payload-send-path-diff.md).
- Validation result:
  - `K_0_1:P` -> `K_COLON`
  - `K_0_1:R` -> `K_COLON`
  - `readSuccessCount > 0`
