# Diagnostics Foundation RC2

## 到達点

RC2 では、実機なしで diagnostics の整合性を確認できる状態を基準点にした。

- `DeviceInspector` のサンプル JSON を固定化
- `Plugin Diagnostics` のサンプル JSON を固定化
- `DiagnosticsComparer` で `issues=0` を確認可能
- `ProtocolSimulator` で replay `issues=0` を確認可能
- CI で同じ検証を再現可能

## 構成図

```text
samples/device-inspector/latest.json
samples/plugin-diagnostics/latest.json
          │
          ├── DiagnosticsComparer ──> samples/comparer/report.md
          │
          └── ProtocolSimulator ────> tmp/diagnostics-ci/protocol-simulator-report.md

scripts/verify-diagnostics.ps1
          ├── dotnet build YMMKeyboardPlugin.slnx -c Release
          ├── DiagnosticsComparer
          └── ProtocolSimulator

.github/workflows/diagnostics.yml
          └── scripts/verify-diagnostics.ps1
```

## 利用ツール一覧

- `scripts/verify-diagnostics.ps1`
- `tools/YMMKeyboard.DeviceInspector`
- `tools/YMMKeyboard.DiagnosticsComparer`
- `tools/YMMKeyboard.ProtocolSimulator`

## samples 構成

```text
samples/
  device-inspector/
    latest.json
  plugin-diagnostics/
    latest.json
  comparer/
    report.md
```

## CI 構成

- `.github/workflows/diagnostics.yml`
- `push` と `pull_request` で実行
- 入口は `scripts/verify-diagnostics.ps1`

## 制約事項

- 実機なし検証のみを対象にする
- HID / COM の実列挙は行わない
- 入力送信や YMM 連携は行わない
- `Device Identity` / `Device Protocol` / comparer schema / simulator schema / sample JSON schema は凍結対象とする

## 未着手事項

- 実機 HID 列挙の再確認
- 実機 COM 列挙の再確認
- 入力送信前段階の可視化
- Hardware Validation

## 次フェーズ候補

### 推奨

- `Input Diagnostics Foundation`

### 代替候補

- `Protocol Simulation Expansion`

## 参照

- [Diagnostics Workflow](diagnostics-workflow.md)
- [Diagnostics Cheat Sheet](diagnostics-cheatsheet.md)
