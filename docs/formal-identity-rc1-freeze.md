# Formal Identity RC1 Freeze

## 目的

YMMKeyboard の正式 identity 適用状態を RC1 として固定する。

このフェーズでは追加実装を行わず、整理・記録・コミットのみを行う。

## Formal Identity

| 項目 | 値 |
|---|---|
| VID | `0x2E8A` |
| PID | `0x4020` |
| Manufacturer | `YMMKeyboard` |
| ProductName | `YMMKeyboard RP2040` |
| Serial | Board UID |
| UsagePage | `0xFF00` |
| Usage | `0x0001` |

## Legacy / Temporary Identity

以下は互換対象外として扱う。

- `2E8A:101F`
- `YMM HID`

## 変更ファイル

正式 identity RC1 の適用対象は次のとおり。

- `firmware/src/RP2040TinyUsb/src/usb_descriptors.c`
- `firmware/src/RP2040TinyUsb/src/main.c`
- `tools/YMMKeyboard.DeviceInspector/DeviceIdentity.cs`
- `tools/YMMKeyboard.DeviceInspector/Program.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/Settings/YMMKeyboardSettings.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/Hid/HidKeyboardLink.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/Diagnostics/PluginConnectionDiagnosticCollector.cs`
- `tools/YMMKeyboard.DiagnosticsComparer/Program.cs`
- `docs/device-identity.md`
- `docs/firmware-identity-review.md`
- `docs/firmware-identity-rc1.md`

## Legacy Identity Removed

- `PID 0x101F` を active path から削除
- `YMM HID` を active path から削除
- `temporary` identity classification を削除

## Verification Result

実施した確認:

- `dotnet build YMMKeyboardPlugin.slnx -c Release`
- `scripts/verify-hardware-preparation.ps1`
- `scripts/verify-hardware-dry-run.ps1`
- `scripts/verify-diagnostics.ps1`
- `scripts/verify-input-diagnostics.ps1`
- `scripts/verify-input-simulation.ps1`
- `scripts/verify-macro-diagnostics.ps1`
- `scripts/verify-dispatch-diagnostics.ps1`
- `scripts/verify-diagnostics-replay.ps1`

結果:

- build success
- hardware preparation success
- hardware dry run success
- diagnostics verification success
- input diagnostics verification success
- input simulation verification success
- macro diagnostics verification success
- dispatch diagnostics verification success
- diagnostics replay verification success

## Known Warnings

- `YMMKeyboardPlugin` の `HidDeviceProbe.cs` に既存の obsolete warning がある
- 現時点で error はない

## Hardware Validation Status

`not started`

## Next Phase

Hardware Validation RC2

RC2 では実機観測を行い、formal identity の実機表示を確認する。
