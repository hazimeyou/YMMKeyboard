# Device Identity

この文書は、`RP2040-Zero` / `RP2040 TinyUSB` / `YMMKeyboardPlugin` 間で使う識別情報を固定するための基準です。

## 正式識別情報

| 項目 | 値 |
|---|---|
| VID | `0x2E8A` |
| PID | `0x4020` |
| Manufacturer | `YMMKeyboard Project` |
| Product | `YMM RP2040 Control Keyboard` |
| CDC Interface | `YMM Serial Bridge` |
| HID Interface | `YMM Control HID` |
| HID Usage Page | `0xFF00` |
| HID Usage | `0x0001` |
| HID Report ID | `1` |
| HID Payload | `63 bytes` |

## 暫定識別情報

過去の調査と旧ファームウェア実装では、次のような値が観測・利用されていました。

| 項目 | 値 | 補足 |
|---|---|---|
| VID | `0x2E8A` | 同一 |
| PID | `0x101F` | 旧系の識別値として利用されていた |
| Manufacturer | `YMMKeyboard` | 旧ログで観測された値 |
| Product | `YMM HID` | 旧ログで観測された値 |
| HID 形式 | `YMMK:<uid>:P:SW_xx` / `YMMK:<uid>:R:SW_xx` | 旧系でもテキスト互換を維持 |

## 既存ファームウェアとの差分表

| 観点 | 旧系 / 暫定 | 正式系 |
|---|---|---|
| 実装言語 | CircuitPython + KMK | C + Pico SDK + TinyUSB |
| HID 送信 | `usb_hid` の custom device | `tud_hid_report()` |
| VID/PID | `2E8A:101F` | `2E8A:4020` |
| Manufacturer | `YMMKeyboard` | `YMMKeyboard Project` |
| Product | `YMM HID` | `YMM RP2040 Control Keyboard` |
| CDC | 実装依存 | `YMM Serial Bridge` |
| HID Interface | 実装依存 | `YMM Control HID` |
| Report ID | `1` | `1` |
| Payload 長 | `64 bytes` 系 | `63 bytes` 系 |
| 安定性 | 旧ログの継続利用向け | 接続判定の固定化向け |

## 運用ルール

1. 正式系を優先する。
2. 暫定系は診断互換のために残す。
3. `YMMKeyboardPlugin` の設定値は、既存の JSON 互換を壊さない。
4. 接続判定は `VID/PID` を第一条件にし、補助条件として `Product` / `Manufacturer` を使う。

## メモ

- `docs/device-protocol.md` は、実際の通信メッセージと接続判定の仕様をまとめた補助資料です。
- 診断時は `YMMKeyboard.DeviceInspector` を使うと、HID/COM/Serial の列挙結果をまとめて確認できます。
