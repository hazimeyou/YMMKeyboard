# Diagnostics Cheat Sheet

RC2 の標準確認はこれです。

```powershell
./scripts/verify-diagnostics.ps1
```

## 何を見るか

- `issues=0`
- `InspectorOnly`
- `PluginOnly`
- `Rejected`
- `Selected`
- `HidVisibleButNotEvaluated`
- `ComVisibleButNotEvaluated`

## 主なファイル

- `samples/device-inspector/latest.json`
- `samples/plugin-diagnostics/latest.json`
- `samples/comparer/report.md`
- `tmp/diagnostics-comparer/report.md`

## 手動比較

必要なときだけ短く実行します。

```powershell
dotnet run --project tools/YMMKeyboard.DiagnosticsComparer/YMMKeyboard.DiagnosticsComparer.csproj -c Release -- --inspector samples/device-inspector/latest.json --plugin samples/plugin-diagnostics/latest.json --format markdown --output tmp/diagnostics-comparer/report.md
```

## 判定の見方

- `issues=0` なら RC2 の再現検証は通過
- `InspectorOnly` / `PluginOnly` は片側だけにある候補
- `Rejected` は plugin 側で選ばれなかった候補
- `Selected` は plugin が選択した候補
- `HidVisibleButNotEvaluated` / `ComVisibleButNotEvaluated` は列挙されたが比較対象外の候補

## 境界

- ここでやるのは実機なしの検証だけ
- 実機の HID / COM 列挙や入力送信はまだ後段
- 詳細手順は [docs/diagnostics-workflow.md](docs/diagnostics-workflow.md) を見る
