# Device Protocol

この文書は、ファームウェアと `YMMKeyboardPlugin` の間で使う通信・判定ルールを整理したものです。

## 1. 通信の基本方針

- 入力送信は **読み取り専用の診断基盤** と **実入力基盤** を分けて扱う。
- `YMMKeyboard.DeviceInspector` は接続確認のみを担当し、入力送信は行わない。
- プラグイン本体は既存の動作を維持し、設定ファイルの形式も変えない。

## 2. 文字列フォーマット

### 2.1 Serial / CDC

ファームウェアからは次の行が出ます。

- `UID:{uid}`
- `{uid}:P:SW_xx`
- `{uid}:R:SW_xx`
- `HB:{uid}`
- `HID_SEND_FAIL`

### 2.2 HID

HID の vendor-defined report では、ASCII テキストとして次の形式を流します。

- `YMMK:{uid}:P:SW_xx`
- `YMMK:{uid}:R:SW_xx`

TinyUSB 系では `Report ID=1` を使い、ペイロードは `63 bytes` にそろえています。

旧系 CircuitPython でも、互換のために `YMMK:` プレフィックスを維持しています。

## 3. 接続判定仕様

### 3.1 HID

`YMMKeyboardPlugin` は次の順序で候補を絞ります。

1. 明示指定された `VID`
2. 明示指定された `PID`
3. `ProductName` の部分一致
4. `Manufacturer` の部分一致
5. 何も指定がない場合の暗黙推定
6. `UsagePage=0xFF00` と `Usage=0x0001` の優先
7. `Input/Output report length` のスコアリング

暗黙推定では、`YMM` らしい HID を優先し、一般的なキーボード系を弱く減点します。

### 3.2 COM

COM モードでは次の流れです。

1. `PortName` を選択する
2. `SerialPort` を `115200` で開く
3. DTR/RTS を立てる
4. `UID:` や `uid:P:SW_xx` の行を読む

このとき、入力送信は行いません。

## 4. 診断出力

`YMMKeyboard.DeviceInspector` は次をログに出します。

- HID の `VID`
- HID の `PID`
- `ProductName`
- `Manufacturer`
- `UsagePage`
- `Usage`
- `DevicePath`
- COM ポート一覧
- Serial の読み取り結果

## 5. 互換性

設定ファイルは既存のキーを維持します。

- `ConnectionMode`
- `PortName`
- `HidVendorIdHex`
- `HidProductIdHex`
- `HidProductNameFilter`
- `HidManufacturerFilter`

## 6. 既存ファームとの差分

| 項目 | 暫定/旧系 | 正式系 |
|---|---|---|
| USB VID/PID | `2E8A:101F` | `2E8A:4020` |
| Manufacturer | `YMMKeyboard` | `YMMKeyboard Project` |
| Product | `YMM HID` | `YMM RP2040 Control Keyboard` |
| CDC 名 | 実装依存 | `YMM Serial Bridge` |
| HID 名 | 実装依存 | `YMM Control HID` |
| 判定優先度 | ログや文字列一致に依存しがち | `VID/PID` を第一条件に固定 |

## 7. 次の拡張余地

- デバイス別の識別結果を JSON で保存する
- 直近の接続履歴を残す
- HID/COM の照合結果をワンクリックでエクスポートする

## 8. JSON 出力仕様

`YMMKeyboard.DeviceInspector` で `--json` を指定すると、標準出力とテキストログに加えて JSON レポートを出力します。

### 8.1 出力項目

JSON には次の項目を含めます。

- `generatedAt`
- `machineName`
- `osVersion`
- `appVersion`
- `hidDevices`
- `comPorts`
- `serialProbeResults`
- `matchedYmmKeyboardCandidates`
- `warnings`

### 8.2 出力先

- `--output <path>` を指定した場合は、そのパスに出力します。
- 指定がない場合は `tmp/device-inspector/` に timestamp 付きファイル名で出力します。

### 8.3 使い方

```powershell
dotnet run --project tools/YMMKeyboard.DeviceInspector/YMMKeyboard.DeviceInspector.csproj -c Release -- --json
dotnet run --project tools/YMMKeyboard.DeviceInspector/YMMKeyboard.DeviceInspector.csproj -c Release -- --json --output tmp/device-inspector/latest.json
```

## 9. Plugin 側の接続判定 JSON

`YMMKeyboardPlugin` は起動時および接続スキャン時に、診断用 JSON を `tmp/plugin-diagnostics/` に保存します。

### 9.1 主な項目

- `generatedAt`
- `appVersion`
- `pluginVersion`
- `ymmVersion`
- `machineName`
- `osVersion`
- `scanMode`
- `configuredDeviceIdentity`
- `detectedHidDevices`
- `detectedComPorts`
- `connectionCandidates`
- `selectedCandidate`
- `rejectedCandidates`
- `warnings`
- `errors`

### 9.2 connectionCandidates の項目

- `transportType`
- `vid`
- `pid`
- `productName`
- `manufacturer`
- `serial`
- `comPort`
- `usagePage`
- `usage`
- `matchScore`
- `matchReasons`
- `rejectReasons`
- `selected`

### 9.3 目的

- DeviceInspector の JSON と同じ観点で比較する
- 「見えているが掴めない」状況の理由を追跡する
- 既存の入力送信や自動操作は変更しない
