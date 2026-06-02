# FEATURE_STATUS

## 完成

| 機能 | 状態 | 根拠 |
|---|---|---|
| YMM4 プラグインの基本起動 | 完成 | `MyToolPlugin` が `Keymacro` を生成して初期化 |
| COM 接続 | 完成 | `SerialKeyboardLink` でポート接続/切断/読み取りを実装 |
| HID 接続 | 完成 | `HidKeyboardLink` で HID デバイス列挙/読み取りを実装 |
| 設定保存/復元 | 完成 | `YMMKeyboardSettings` が JSON で保存 |
| 設定 UI | 完成 | 接続モード、COM、HID フィルタ、診断、マッピングUIを実装 |
| キー割り当て編集 | 完成 | `KeyboardMappingWindow` と `MappingConverter` |
| テストイベント | 完成 | `TestEvent.Execute` |
| YMMT 読み込み | 完成 | `LoadYmmtCatalogAction` とテストあり |
| シーク操作 | 完成 | `KeyboardAction` が反射/Window command 両対応 |
| 自動ビルド | 完成 | RP2040 C ファームの build script を整備 |
| UF2 出力 | 完成 | `build-rp2040-tinyusb.ps1` で生成確認済み |

## 動作確認済み

| 機能 | 状態 | 根拠 |
|---|---|---|
| `MappingConverter.NormalizeActionName` | 動作確認済み | 単体テストあり |
| `SwitchLayout.NormalizeCombination` | 動作確認済み | 単体テストあり |
| `YmmtCatalogLoader.Load/TryLoad` | 動作確認済み | 単体テストあり |
| C ファームのビルド | 動作確認済み | `ymm_keyboard_fw.uf2` 生成確認 |
| HID 診断レポート出力 | 動作確認済み | 設定画面から出力確認 |

## 未確認

| 機能 | 状態 | コメント |
|---|---|---|
| 実機の全キー入力 | 未確認 | 配線確定と全スイッチ実押下が必要 |
| 実機側の長時間安定運用 | 未確認 | 連続稼働テスト未実施 |
| 複数 HID デバイス同時環境 | 未確認 | 自動絞り込みの誤選択検証が必要 |
| YMM の実行時 DLL ロック回避 | 未確認 | YMM 起動中は配置先コピーが失敗する |

## 開発途中

| 機能 | 状態 | コメント |
|---|---|---|
| RP2040 C ファームの実機入力スキャン | 開発途中 | 行列スキャンの最小実装はあるが、確定配線の検証が必要 |
| HID 生データの受信解析 | 開発途中 | 診断ログはあるが、受信ゼロ時の切り分け改善余地あり |
| 低遅延シークの追い込み | 開発途中 | 反射呼び出しの最適化余地あり |
| 自動絞り込み精度 | 開発途中 | Usage/文字列の確定条件をさらに絞る余地あり |

## 廃止候補

| 機能 | 状態 | コメント |
|---|---|---|
| CircuitPython 旧系ファーム | 廃止候補 | 現在は C / TinyUSB 系を優先した方が安定 |
| 古い診断用定期 `SW_00` パルス | 廃止候補 | 本番運用では誤トリガー要因になりやすい |

## テスト状況

- 単体テストは `MappingConverter` / `SwitchLayout` / `YmmtCatalogLoader` に集中しています。
- HID・COM 実通信の自動テストはありません。
- ファームウェアの実機テストは手動確認ベースです。
