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
