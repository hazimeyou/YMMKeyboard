# Input Diagnostics

`YMMKeyboardPlugin` の入力処理を観測するための診断です。

このフェーズでは入力送信は変更しません。

## 観測イベント

- `InputReceived`
- `InputFiltered`
- `InputMapped`
- `MacroResolved`
- `DispatchPrepared`

## JSON schema

```text
generatedAt
appVersion
pluginVersion
machineName
osVersion
source
summary
events[]
```

### summary

- `eventCount`
- `macroCount`
- `mappedActionCount`
- `rejectedCount`
- `issuesCount`

### events[]

各イベントは `eventType` を持ち、必要な項目だけを埋める。

- `InputReceived`
  - `timestamp`
  - `transportType`
  - `sourceDevice`
  - `rawInput`
  - `inputId`
- `InputFiltered`
  - `filterName`
  - `accepted`
  - `rejectReason`
- `InputMapped`
  - `inputId`
  - `mappedAction`
  - `mappingSource`
- `MacroResolved`
  - `macroName`
  - `stepCount`
  - `resolutionResult`
- `DispatchPrepared`
  - `dispatchType`
  - `target`
  - `payloadSummary`

## Viewer

```powershell
dotnet run --project tools/YMMKeyboard.InputDiagnosticsViewer/YMMKeyboard.InputDiagnosticsViewer.csproj -c Release -- --input samples/input-diagnostics/single-input.json --format markdown --output tmp/input-diagnostics-viewer/single-input.md
```

対応形式:

- `text`
- `markdown`
- `json`

## Samples

```text
samples/input-diagnostics/
  single-input.json
  mapped-input.json
  macro-input.json
  rejected-input.json
```

## 保存先

実行時の出力は `tmp/input-diagnostics/` を使う。

- `input-diagnostics-*.json`

## 境界

- ここで扱うのは観測・記録・再現だけ
- 実際の YMM 送信は行わない
- 実機検証はまだ後段
