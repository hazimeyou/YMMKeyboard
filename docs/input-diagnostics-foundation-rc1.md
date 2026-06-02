# Input Diagnostics Foundation RC1

## 到達点

Input Diagnostics Foundation RC1 は、入力送信を変更せずに入力処理を観測・記録・再現できる状態を基準点とする。

- `InputReceived` を記録できる
- `InputFiltered` を記録できる
- `InputMapped` を記録できる
- `MacroResolved` を記録できる
- `DispatchPrepared` を記録できる
- sample JSON を viewer で読み込める
- CI で sample replay を再現できる

## 追加ファイル

- `docs/input-diagnostics.md`
- `tools/YMMKeyboard.InputDiagnosticsViewer`
- `scripts/verify-input-diagnostics.ps1`
- `.github/workflows/input-diagnostics.yml`
- `samples/input-diagnostics/single-input.json`
- `samples/input-diagnostics/mapped-input.json`
- `samples/input-diagnostics/macro-input.json`
- `samples/input-diagnostics/rejected-input.json`
- `ymm-plugin/src/YMMKeyboardPlugin/Diagnostics/InputDiagnosticEvent.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/Diagnostics/InputDiagnosticReport.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/Diagnostics/InputDiagnosticSummary.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/Diagnostics/InputDiagnosticWriter.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/Diagnostics/InputDiagnostics.cs`

## 診断イベント一覧

- `InputReceived`
- `InputFiltered`
- `InputMapped`
- `MacroResolved`
- `DispatchPrepared`

## samples 構成

```text
samples/
  input-diagnostics/
    single-input.json
    mapped-input.json
    macro-input.json
    rejected-input.json
```

## CI 構成

- `.github/workflows/input-diagnostics.yml`
- 入口は `scripts/verify-input-diagnostics.ps1`
- CI は sample replay のみを実行する

## 制約事項

- 実機検証は開始しない
- `ActualDispatch` は変更しない
- YMM 操作は変更しない
- HID 処理は変更しない
- COM 処理は変更しない
- Protocol は変更しない
- `InputDiagnosticReport` schema は固定
- `InputDiagnosticEvent` schema は固定
- `InputDiagnosticsViewer` の出力形式は固定

## 未着手事項

- 実機入力の再現
- 仮想入力生成
- 実機検証
- Input Simulation Foundation RC1

## 既知警告

- `HidDeviceProbe.cs` の obsolete warnings x2

## 次フェーズ候補

- `Input Simulation Foundation RC1`

## 参照

- [Input Diagnostics](input-diagnostics.md)
