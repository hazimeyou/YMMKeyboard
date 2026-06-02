# TECH_DEBT

## 主要な技術的負債

| 優先度 | 内容 | 根拠 | リスク |
|---|---|---|---|
| P1 | 実機入力の確定配線が未固定 | `firmware/src/RP2040TinyUsb/src/main.c` の GPIO 定義は仮配置を含む | 反応しない、誤配線時の切り分けが難しい |
| P1 | デバッグイベントが本番経路に混在 | 定期 `SW_00` パルスが残っている | 誤トリガー、ログノイズ、想定外動作 |
| P1 | YMM 起動中の DLL コピー失敗が警告止まり | `YMMKeyboardPlugin.csproj` の `ContinueOnError="WarnAndContinue"` | 古い DLL が残り、修正が反映されない |
| P2 | HID 受信ゼロ時の診断が弱い | `raw_report_samples=0` のときの切り分け導線が不足 | 配線/ファーム/列挙のどこで止まるか不明瞭 |
| P2 | 自動絞り込みが誤選択しうる | 同一 VID の他デバイス混在時に誤認識可能 | 別デバイスを掴む可能性 |
| P2 | HidSharp の obsolete 警告 | `ProductName` / `Manufacturer` が古い API | 将来の互換性リスク |
| P3 | ログコメントの文字化け痕跡 | `PluginLogger.cs` などのコメント痕跡 | 保守性低下 |
| P3 | `D:\` 直書き配置 | `YMMKeyboardPlugin.csproj` で `D:\code.py` 等を直接コピー | 環境依存、誤配置の可能性 |

## ビルド由来の負債

- プラグインビルド時、YMM 起動中は `user\plugin\YMMKeyboardPlugin\*.dll` のコピーがロックで失敗する。
- RP2040 ファームのビルドはローカルの `cmake` / `ninja` / `arm-none-eabi-gcc` / `python` に依存する。
- `YMM4` 参照 DLL は最新リリース取得方式のため、再現性が外部要因に左右される。

## 監査で見つかった TODO / FIXME 相当

- `firmware/src/RP2040TinyUsb/src/main.c`
  - 行列スキャンの説明コメントが残る
- `ymm-plugin/src/YMMKeyboardPlugin/Hid/HidKeyboardLink.cs`
  - 診断/除外ロジックが複雑化
- `firmware/src/RP2040ZeroCode/code.py`
  - デバッグ出力と本番処理が同居

## 改善優先順

1. 実機配線の確定とピン定義の固定
2. デバッグ動作と本番動作の切り分け
3. DLL 配置失敗の明示化
4. HID 選択ロジックの確定条件追加
5. 自動テストの増設
