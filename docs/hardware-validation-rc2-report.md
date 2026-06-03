# Hardware Validation RC2 Report

## Summary

- Date: 2026-06-02 23:37 JST
- Operator: Codex
- Host machine: `WEEEEEEI`
- OS version: `Microsoft Windows 10.0.26200`
- Repository state: working tree had existing untracked docs from prior phases; no tracked source changes were made in this phase
- Validation target: RC2 Execution
- Outcome: formal HID device is present again and plugin selected the formal candidate

## Flash Result

- Flash success: yes
- Firmware version: `1.0.0+a408dd6e12cce0f0bb18a4c7fd5536abd95dad9e`
- Build timestamp: `2026-06-02 22:36:06 +09:00`
- UF2 path: `firmware/src/RP2040TinyUsb/build/ymm_keyboard_fw.uf2`
- Notes:
  - `RPI-RP2` was visible and the UF2 was copied successfully.
  - The cleanly rebuilt UF2 was used for the final flash in this phase.
  - The device re-enumerated as the formal `2E8A:4020` identity after reconnect.
  - YMM was closed once to release the plugin DLL lock, then restarted cleanly.

## Enumeration Result

### DeviceInspector

- Output path: `tmp/device-inspector/latest.json`
- HID count: `41`
- COM count: `2`
- Formal `2E8A:4020` present: yes
- Matched YMM keyboard candidates: `1`

### Plugin Diagnostics

- Output path: `tmp/plugin-diagnostics/latest.json`
- Snapshot timestamp: `2026-06-02 23:37 JST`
- scanMode: `startup`
- configuredDeviceIdentity: `connectionMode=Hid`, `hidVendorId=2E8A`, `hidProductId=4020`, `hidProductNameFilter=YMM Control HID`, `hidManufacturerFilter=YMMKeyboard`, `portName=COM7`
- rawHidEnumeration:
  - `totalDeviceCount=41`
  - `successCount=41`
  - `failedCount=0`
  - `skippedCount=0`
  - `2E8A:4020 present: yes`
- detectedHidDevices: `41`
- selectedCandidate: `HID:2E8A:4020`
- rejectedCandidates:
  - `COM1`
  - `COM10`
  - `COM7`
  - `COM7` serial candidate
- matchScore:
  - `COM1 = 10`
  - `COM10 = 10`
  - `COM7 = 10`
  - `COM7 serial = 0`
- matchReasons:
  - `COM1 = detectedComPort`
  - `COM10 = detectedComPort`
  - `COM7 = detectedComPort`
  - `COM7 serial = configuredPort`
- rejectReasons:
  - `COM1 = selectionIsManagedBySerialTransport`
  - `COM10 = selectionIsManagedBySerialTransport`
  - `COM7 = selectionIsManagedBySerialTransport`
  - `COM7 serial = mode=HID`

### DiagnosticsComparer

- Output path: `tmp/diagnostics-comparer/report.md`
- Snapshot timestamp: `2026-06-02 23:37 JST`
- Issue count: `13`
- Issue types:
  - `PluginOnly`
  - `InspectorWarning`
  - `PluginWarning`
- IdentityMismatch: `clear in this snapshot`
- MissingHidUsage: tolerated as an observation even though the formal candidate was selected

## Observations

- What matched:
  - `HidDeviceProbe` no longer collapses the entire HID list to empty when one device fails.
  - Plugin diagnostics now carries `rawHidEnumeration` with device counts and per-device records.
  - The plugin-side `detectedHidDevices` list is populated again.
  - The formal `2E8A:4020` HID device is visible again in both DeviceInspector and plugin diagnostics.
  - Plugin diagnostics selected the formal candidate.
- What did not match:
  - `MissingHidUsage` and zero `UsagePage` / `Usage` still remain as observations.
  - Comparer still reports plugin-only candidate differences for other devices.
- What was tolerated as observation:
  - `MissingHidUsage`
  - `UsagePage = 0`
  - `Usage = 0`
- Any environmental notes:
  - `RPI-RP2` was visible long enough for the flash to complete.
  - A clean firmware rebuild was completed locally before the final flash.
  - The plugin DLL lock was released by closing YMM before the reconnect snapshot was taken.

## Result

- Pass / Fail / Blocked: `Pass`
- Reason: the formal HID device is present again, plugin selected the formal candidate, and the remaining comparer issues are non-blocking observations.
- Follow-up action:
  - Keep the raw HID diagnostics in place.
  - If desired, continue with the next phase: Input Validation RC1.

## Notes

- HID送信テストは未実施。
- COM送信テストは未実施。
- Macro実行は未実施。
- YMM操作は未実施。
- AutoFilter検証は未実施。
