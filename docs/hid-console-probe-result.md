# HID Console Probe Result

## Run Info

- Date: 2026-06-03
- Operator: Codex
- Board count: 1
- `VID/PID`: `2E8A:4020`
- `--index`: `0`
- `--timeout-ms`: `500`
- `--duration-sec`: `16`

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

- `readAttemptCount`: `32`
- `readSuccessCount`: `0`
- `readTimeoutCount`: `32`
- `lastException`: `TimeoutException`

## Report Dump

```text
none
```

## Conclusion

- `readSuccessCount = 0`
- report was not received in the standalone console probe
- this strongly suggests the firmware side is not emitting a usable HID input report, or the send path is not being triggered
