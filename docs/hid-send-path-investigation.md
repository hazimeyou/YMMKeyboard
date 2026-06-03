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

## Next Phase Runbook

This section fixes the exact procedure for the next live investigation run.

### Goal

Determine which side is currently blocking input:

- A: firmware is not sending HID reports
- B: firmware is sending HID reports but the host is not receiving them

### Scope

Allowed:

- one-device BOOTSEL flash
- CDC observation
- `hid_runtime_summary.txt` capture
- `tmp/input-diagnostics/` capture

Not allowed in this run:

- HID send validation beyond the existing firmware diagnostics
- COM command validation
- macro validation
- YMM operation validation
- candidate filter redesign

### Pre-Run Checklist

1. Keep only one target board in scope.
2. Confirm the latest diagnostic firmware exists:
   - `firmware/src/RP2040TinyUsb/build/ymm_keyboard_fw.uf2`
3. Confirm the latest plugin build exists:
   - `ymm-plugin/src/YMMKeyboardPlugin/bin/Release/net10.0-windows/YMMKeyboardPlugin.dll`
4. Close YMM4 before flash.
5. Preserve `stash@{0}` untouched.

### Fixed Execution Order

1. Put exactly one board into BOOTSEL mode.
2. Confirm that `RPI-RP2` appears.
3. Copy `firmware/src/RP2040TinyUsb/build/ymm_keyboard_fw.uf2`.
4. Wait for the BOOTSEL drive to disappear.
5. Reconnect in normal runtime mode.
6. Confirm the device enumerates as `2E8A:4020`.
7. Start CDC capture first.
8. Start YMM4 and let the plugin initialize.
9. Capture `hid_runtime_summary.txt`.
10. Press `Key1` once.
11. Capture CDC output again.
12. Capture the latest input diagnostics JSON.

### Required Artifacts

Save or confirm these outputs for every run:

- CDC log containing:
  - `HB:`
  - `HID_STATUS:`
  - `HID_DIAG:`
- `%LOCALAPPDATA%/YMMKeyboard/_diagnostics/hid_runtime_summary.txt`
- latest file under `tmp/input-diagnostics/`
- if available, refreshed plugin diagnostics JSON

### Read Order

Inspect the artifacts in this exact order:

1. CDC `HID_STATUS`
2. CDC `HID_DIAG`
3. `hid_runtime_summary.txt`
4. `tmp/input-diagnostics/latest`

### Classification Rules

Classify the result using the first matching rule:

1. `HID_STATUS ready=false` most of the time
   - classify as `firmware-not-ready`
2. `HID_DIAG sendResult=false`
   - classify as `firmware-send-failed`
3. `HID_DIAG sendResult=true` and `readSuccessCount=0`
   - classify as `host-not-receiving`
4. `readSuccessCount>0` and `raw_report_samples=0`
   - classify as `host-parse-failed`
5. `InputReceived>0` and `InputMapped=0`
   - classify as `mapping-blocked`
6. `InputMapped>0` and `DispatchPrepared=0`
   - classify as `dispatch-planning-blocked`

### Success Criteria

This run is considered successful if one of the following is proven:

- firmware is not sending HID reports
- firmware is sending HID reports and the host is not receiving them
- firmware is sending and host is receiving, but parsing is failing

The purpose of this run is proof, not repair.

### Stop Conditions

Stop the run and record it as blocked if any of the following happens:

- `RPI-RP2` does not appear
- flash does not complete
- the device no longer enumerates as `2E8A:4020`
- YMM4 cannot load the plugin
- CDC capture cannot be established

### Report Update Rule

After the run, update:

- `docs/hardware-validation-rc2-report.md`
- `docs/input-validation-rc1-report.md`

Record:

- board count used
- flash result
- CDC observation summary
- HID runtime summary
- classification result
- remaining blocker
