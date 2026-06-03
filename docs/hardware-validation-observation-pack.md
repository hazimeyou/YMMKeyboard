# Hardware Validation RC2 Observation Pack

## Purpose

実機接続時に、何を保存し、何を読み取り、何を成功判定に使うかを固定する。

この文書は観測の標準化のみを目的とし、実機操作そのものはここでは行わない。

## Save Locations

- `tmp/device-inspector/latest.json`
- `tmp/plugin-diagnostics/latest.json`
- `tmp/diagnostics-comparer/report.md`

## DeviceInspector

保存:

- `tmp/device-inspector/latest.json`

取得:

- `VID`
- `PID`
- `Manufacturer`
- `ProductName`
- `Serial`
- `UsagePage`
- `Usage`

記録メモ:

- 取得値は実機観測の一次ソースとして扱う
- `Serial` は Board UID 由来かを確認する
- `UsagePage` / `Usage` が `0` または missing の場合でも、RC2 では観測結果として記録する

## Plugin Diagnostics

保存:

- `tmp/plugin-diagnostics/latest.json`

取得:

- `selectedCandidate`
- `rejectedCandidates`
- `matchScore`
- `matchReasons`
- `rejectReasons`

記録メモ:

- `selectedCandidate` が formal candidate かを確認する
- `rejectedCandidates` は候補がなぜ外れたかを追跡するために残す
- `matchScore` は comparer の差分確認で使用する
- `matchReasons` / `rejectReasons` は identity 一致・不一致の根拠として残す

## DiagnosticsComparer

保存:

- `tmp/diagnostics-comparer/report.md`

取得:

- issue count
- issue types

記録メモ:

- `IdentityMismatch` の有無を最優先で確認する
- `MissingHidUsage` は RC2 では許容し、失敗扱いにしない

## Hardware Validation Report

保存:

- `docs/hardware-validation-rc2-report.md`

記録:

- flash result
- enumeration result
- comparer result
- observations

## Success Criteria

Identity:

- `VID = 0x2E8A`
- `PID = 0x4020`
- `Manufacturer = YMMKeyboard`
- `ProductName = YMMKeyboard RP2040`

Plugin:

- formal candidate selected

Comparer:

- `IdentityMismatch = 0`

## Permitted Observations

以下は失敗扱いにしない。

- `MissingHidUsage`
- `UsagePage = 0`
- `Usage = 0`

これらは観測結果としてそのまま記録する。

## Notes

- HID入力送信はまだ行わない
- COM送信はまだ行わない
- Macro実行はまだ行わない
- YMM操作はまだ行わない
- AutoFilter 検証はまだ行わない

## What to Record During RC2

1. Flash の成否と、書き込んだ firmware 版数を記録する。
2. DeviceInspector の JSON を保存する。
3. Plugin Diagnostics の JSON を保存する。
4. DiagnosticsComparer の Markdown を保存する。
5. 成功条件に対する観測結果を `docs/hardware-validation-rc2-report.md` にまとめる。

## Expected Output Shape

RC2 実施時に迷わないため、以下の順で確認する。

1. Flash result
2. Enumeration result
3. Plugin result
4. Comparer result
5. Observations

## Completion Rule

この Observation Pack があれば、実機接続時に保存するものと確認するものを迷わず実施できる状態になること。
