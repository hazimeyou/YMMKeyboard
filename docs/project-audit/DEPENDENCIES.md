# DEPENDENCIES

## .NET / YMM プラグイン側

| ライブラリ | バージョン | 用途 | 更新状況 |
|---|---:|---|---|
| `YukkuriMovieMaker.Plugin` | 最新リリース取得 | YMM4 プラグイン API | 外部最新依存、未固定 |
| `YukkuriMovieMaker.Controls` | 最新リリース取得 | YMM4 UI 参照 | 外部最新依存、未固定 |
| `YukkuriMovieMaker` | 最新リリース取得 | YMM4 本体 API 参照 | 外部最新依存、未固定 |
| `System.IO.Ports` | `10.0.2` | COM ポート列挙/接続 | 固定済み |
| `HidSharp` | `2.1.0` | HID 列挙/読み取り | 固定済み |
| `xunit` | `2.9.3` | 単体テスト | 固定済み |
| `xunit.runner.visualstudio` | `3.1.4` | テスト実行 | 固定済み |
| `Microsoft.NET.Test.Sdk` | `17.14.1` | テスト実行基盤 | 固定済み |
| `coverlet.collector` | `6.0.4` | カバレッジ収集 | 固定済み |

## RP2040 C ファーム側

| ライブラリ | バージョン | 用途 | 更新状況 |
|---|---|---|---|
| `pico-sdk` | 外部 Git clone | RP2040 開発基盤 | 自動取得、ブランチ固定なし |
| `TinyUSB` | `pico-sdk` 同梱版 | USB CDC/HID | `pico-sdk` に追随 |
| `arm-none-eabi-gcc` | 14.2 系 | クロスコンパイル | ローカル環境依存 |
| `CMake` | 4.x 系確認 | 生成/ビルド | ローカル環境依存 |
| `Ninja` | ローカル環境依存 | 生成/ビルド | 優先使用、無い場合は代替 |
| `python` | ローカル環境依存 | UF2 変換 | ローカル環境依存 |

## CircuitPython 旧系ファーム側

| ライブラリ | バージョン | 用途 | 更新状況 |
|---|---|---|---|
| `KMK` | 固定なし | キーマトリクス/エンコーダ | bundle から取得、未固定 |
| `usb_hid` | CircuitPython 組み込み | USB HID 出力 | CircuitPython 同梱 |
| `board` | CircuitPython 組み込み | GPIO 定義 | CircuitPython 同梱 |
| `microcontroller` | CircuitPython 組み込み | UID 取得 | CircuitPython 同梱 |
| `supervisor` | CircuitPython 組み込み | 時間/実行管理 | CircuitPython 同梱 |

## 補助ツール

| ライブラリ/ツール | バージョン | 用途 | 更新状況 |
|---|---|---|---|
| `HidProbeConsole` | ローカルソース | HID 生データ診断 | ソース管理内 |
| `KeyboardEventStateTester` | ローカルソース | 単純状態遷移テスト | ソース管理内 |
| `uf2conv.py` | `microsoft/uf2` 系 | `.bin` -> `.uf2` 変換 | コピー取得 |

## 更新方法

- YMM4 DLL は `tools/scripts/fetch-ymm4-libs.ps1` で最新リリースから取得
- Pico SDK は `tools/scripts/setup-rp2040-build-env.ps1` で `tmp/pico/pico-sdk` に clone
- UF2 変換は `tools/scripts/rp2040_tools/` の Python スクリプトを利用

## 留意点

- `YMM4` 参照 DLL は CI でも「最新リリース」を採るため、ビルドの再現性は高くありません。
- CircuitPython / KMK はリポジトリ内でバージョン固定されていません。
