# HID Send Path Investigation RC1

## Summary

This investigation is intended to determine whether the current failure is on the firmware side or the host side:

- Device detection: OK
- Candidate selection: OK
- HID open: OK
- HID read: still failing

At the time of this report, the firmware diagnostics have been added and the firmware builds successfully, but the live flash step is blocked because the `RPI-RP2` BOOTSEL drive is not visible.

## Runtime Observation

Current host-side observations remain:

- `selectedCandidate = HID:2E8A:4020`
- `selectedPath == openedPath`
- `openSucceeded = true`
- `readLoopStarted = true`
- `readSuccessCount = 0`
- `readTimeoutCount > 0`
- `raw_report_samples = 0`
- `InputReceived = 0`
- `InputMapped = 0`
- `DispatchPrepared = 0`

## Firmware Diagnostics Added

The firmware `main.c` was extended to emit the following diagnostics over CDC:

### HID_STATUS

Periodically reports:

- `ready=<true/false>`
- `sendCount=<n>`
- `sendFailCount=<n>`
- `lastSendResult=<true/false>`
- `hidReadyTrueCount=<n>`
- `hidReadyFalseCount=<n>`
- `hidReportCallCount=<n>`
- `hidReportSuccessCount=<n>`
- `hidReportFailCount=<n>`
- `reportId=<n>`
- `reportLength=<n>`
- `descriptorLength=<n>`

### HID_DIAG

On key event emission, reports:

- `button=SW_00`
- `pressed=<true/false>`
- `hidReady=<true/false>`
- `reportLength=<n>`
- `reportId=<n>`
- `sendResult=<true/false>`

## Report Information

Source-defined report details:

- `reportId = 1`
- `reportLength = 63`
- `descriptorLength = 63`

These values match the current HID report descriptor and send path in `firmware/src/RP2040TinyUsb/src/usb_descriptors.c` and `firmware/src/RP2040TinyUsb/src/main.c`.

## Build Result

- `./tools/scripts/build-rp2040-tinyusb.ps1` succeeded.
- `firmware/src/RP2040TinyUsb/build/ymm_keyboard_fw.uf2` was regenerated successfully.
- The new diagnostics strings are present in the build artifacts.

## Flash Status

- Flash not completed in this investigation run.
- Reason: `RPI-RP2` BOOTSEL drive is not currently visible, so there is no mount point for UF2 copy.

## Conclusion

The investigation is currently blocked before live firmware confirmation. We have enough evidence to say:

- host-side detection and candidate selection are working
- the HID read path is opening the correct path
- the next required proof is a live flash followed by CDC observation of `HID_STATUS` / `HID_DIAG`

At present, the question "firmware is not sending" vs "host is not receiving" is **not yet fully resolved** because the new diagnostic firmware has not been flashed live.

## Next Step

1. Put exactly one RP2040 into BOOTSEL mode.
2. Confirm that `RPI-RP2` appears.
3. Copy `firmware/src/RP2040TinyUsb/build/ymm_keyboard_fw.uf2` to the drive.
4. Reconnect and capture CDC output plus `hid_runtime_summary.txt`.
