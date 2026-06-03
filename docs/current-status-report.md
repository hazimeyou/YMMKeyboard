# YMMKeyboard Current Status Report

## 1. プロジェクト全体状況

| 項目 | 状態 | 要約 |
|---|---|---|
| Project Audit | Completed | ベースライン整理と追跡方針の確立は完了。 |
| Diagnostics Foundation RC2 | Completed | 診断基盤は確立済み。各種 verify も通過している。 |
| Input Diagnostics RC1 | Completed | 入力診断の枠組みは用意済み。ただし実入力の到達は未解決。 |
| Input Simulation RC1 | Completed | 入力シミュレーション基盤は完了済み。 |
| Macro & Dispatch Diagnostics RC1 | Completed | Macro / Dispatch 診断は完了済み。 |
| Unified Diagnostics Replay | Completed | 再現・比較の流れは整備済み。 |
| Hardware Validation RC2 | Completed | formal identity の実機観測と候補選定は成功。現在は受信経路の追跡が残っている。 |

## 2. 現在成功していること

- `dotnet build YMMKeyboardPlugin.slnx -c Release` は成功している。
- `./scripts/verify-hardware-preparation.ps1` は成功している。
- `./scripts/verify-hardware-dry-run.ps1` は成功している。
- formal identity は firmware / plugin / inspector / comparer に反映済み。
- DeviceInspector で `2E8A:4020` が検出できている。
- DeviceInspector では `Manufacturer=YMMKeyboard`、`Serial=50443404287E991C` を確認できている。
- Plugin Diagnostics で `selectedCandidate = HID:2E8A:4020` まで到達している。
- raw HID enumeration は動作しており、`2E8A:4020` は列挙対象に入っている。
- firmware の clean build と UF2 生成は成功している。
- CDC 側では `HB:` と `P/R:SW_00` の出力が観測できている。

## 3. 現在のブロッカー

- `InputReceived` が入力診断パイプラインに到達していない。
- `InputMapped` と `DispatchPrepared` も `0` のまま。
- `raw_report_samples = 0` のままなので、HID の raw report を host 側で観測できていない。
- `selectedPath == openedPath` で path は一致しているが、read loop は timeout のみで成功していない。
- `HID_STATUS` / `HID_DIAG` を使った追加診断は firmware 側に入っているが、CDC 上ではまだ確認できていない。
- 次の live flash に必要な `RPI-RP2` ドライブは、現時点では常時見えていない。

## 4. 原因候補

1. firmware の HID 送信経路が実際には report を出していない。
2. `tud_hid_ready()` が false のまま、もしくは `tud_hid_report()` が失敗している。
3. report は送られているが、host 側の parse / 受信条件で落ちている。
4. path / interface の選択は現状で一致しているため低優先だが、別経路の取り違えがまだ残っている可能性はある。

## 5. 実機確認結果

### 現在の最新観測

| 項目 | 値 |
|---|---|
| VID | `0x2E8A` |
| PID | `0x4020` |
| ProductName | `YMM Control HID` |
| Manufacturer | `YMMKeyboard` |
| Serial | `50443404287E991C` |
| selectedCandidate | `HID:2E8A:4020` |
| comparer result | `IdentityMismatch` は解消済み。`issues=13`、`MissingHidUsage` は観測扱い。 |

### 追加の runtime 観測

- `selectedPath` と `openedPath` は一致している。
- `openSucceeded=True`。
- `readLoopStarted=True`。
- `readAttemptCount=285`。
- `readSuccessCount=0`。
- `readTimeoutCount=284`。
- `raw_report_samples=0`。

## 6. 残課題一覧

### Firmware

- HID report が host に届かない理由の特定。
- `HID_STATUS` / `HID_DIAG` を使った送出状態の可視化確認。

### Plugin

- raw HID 受信が timeout で止まる理由の切り分け。
- `openedPath` と report 受信の実効経路の対応確認。

### Diagnostics

- `InputReceived` を 1 件以上発生させる。
- `InputMapped` と `DispatchPrepared` を 1 件以上観測する。

### YMM Integration

- YMM 側の入力経路が実機レポートを受けているかを確認する。
- Macro / Dispatch 側に流れる前段で止まっていないかを確認する。

### Hardware Validation

- BOOTSEL / `RPI-RP2` が見えたタイミングでのみ live flash を実施する運用を維持する。
- 今後の実機観測は 1 台ずつに限定する。

## 7. リスク一覧

- HID report format の不一致。
- Windows HID enumeration の見え方の差分。
- path selection の取り違え。
- `tud_hid_ready()` の状態が見えていないことによる診断の遅れ。
- BOOTSEL ドライブ非表示による再フラッシュ停滞。
- YMM 起動状態による plugin DLL ロックの再発。

## 8. 次にやるべきこと

### P1

- `InputReceived` が発生する条件を確定する。
- `HID_STATUS` / `HID_DIAG` を観測して、firmware の送出段階を 1 回で切り分ける。

### P2

- raw HID report が 1 件も来ない理由を、firmware / host のどちらにあるかで確定する。
- 必要なら plugin diagnostics の追加観測を取り直す。

### P3

- Input Mapping / Dispatch / Macro の次段階に進む前提を整える。
- BOOTSEL 再フラッシュが必要なら、`RPI-RP2` が見えるタイミングに合わせて 1 台だけ実施する。

## 9. 推定完成度

| 領域 | 推定完成度 |
|---|---:|
| Diagnostics | 90% |
| Firmware Identity | 100% |
| Hardware Validation | 80% |
| Input Validation | 20% |
| Macro Validation | 10% |
| YMM Integration | 25% |
| Overall | 60% |

## 10. 結論

認識と identity 整理は成功している。`2E8A:4020` は検出でき、Plugin は formal candidate を選べている。一方で、実入力はまだ `InputReceived` に到達しておらず、`raw_report_samples=0` のまま止まっている。次にやるべきことは、HID 送出経路が本当に report を出していないのか、それとも host 側で受信できていないのかを 1 回で切り分けること。
