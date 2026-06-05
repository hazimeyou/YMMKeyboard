# BUILD_ENVIRONMENT

## 概要

このリポジトリは、`YukkuriMovieMaker 4` 連携のための複数実装を同居させた構成です。

- `ymm-plugin/`: YMM4 向け .NET/WPF プラグイン
- `firmware/`: RP2040 向けファームウェア
- `tools/`: 診断ツールとビルド補助スクリプト
- `hardware/`: KiCad を中心としたハードウェア資料
- `driver/`: PC側通信/制御の補助資料

## フォルダツリー

```text
YMMKeyboard/
  .github/
    workflows/release.yml
  driver/
  firmware/
    src/
      RP2040ZeroCode/
      RP2040TinyUsb/
  hardware/
    pcb/
      KiCad/
      gerber/
  tools/
    HidProbeConsole/
    KeyboardEventStateTester/
    scripts/
  ymm-plugin/
    src/YMMKeyboardPlugin/
    tests/YMMKeyboardPlugin.Tests/
  YMMKeyboardPlugin.slnx
  Directory.Build.props
```

## 主要ソース一覧

- `ymm-plugin/src/YMMKeyboardPlugin/Plugin/MyToolPlugin.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/Key/Keymacro.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/Hid/HidKeyboardLink.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/SerialKeyboard/SerialKeyboardLink.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/Settings/YMMKeyboardSettings.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/Views/YMMKeyboardSettingsPanel.xaml(.cs)`
- `ymm-plugin/src/YMMKeyboardPlugin/Actions/KeyboardAction.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/Actions/LoadYmmtCatalogAction.cs`
- `firmware/src/RP2040ZeroCode/code.py`
- `firmware/src/RP2040ZeroCode/boot.py`
- `firmware/src/RP2040TinyUsb/src/main.c`
- `firmware/src/RP2040TinyUsb/src/usb_descriptors.c`
- `tools/HidProbeConsole/Program.cs`
- `tools/KeyboardEventStateTester/Program.cs`
- `tools/scripts/setup-rp2040-build-env.ps1`
- `tools/scripts/build-rp2040-tinyusb.ps1`
- `tools/scripts/fetch-ymm4-libs.ps1`
- `.github/workflows/release.yml`

## ビルド環境

### YMM4 プラグイン

- フレームワーク: `net10.0-windows`
- UI: `WPF`
- 言語機能: `Nullable enable`, `ImplicitUsings enable`
- 対応プラットフォーム: `AnyCPU`, `x64`
- 参照先 YMM4 DLL:
  - `YMM4DirPath` が未指定なら `$(USERPROFILE)\Desktop\YukkuriMovieMaker_v4_Lite\`
  - それも無ければ `ymm-plugin/libs/YMM4`
- 主要パッケージ:
  - `System.IO.Ports 10.0.2`
  - `HidSharp 2.1.0`
- 出力後の配置:
  - `$(YMM4DirPath)\user\plugin\$(AssemblyName)`

### RP2040 TinyUSB ファームウェア

- フレームワーク: `pico-sdk + TinyUSB`
- 言語: C11 / C++17
- ボード定義: `waveshare_rp2040_zero`
- 依存ライブラリ:
  - `pico_stdlib`
  - `tinyusb_device`
  - `tinyusb_board`
  - `hardware_gpio`
  - `hardware_timer`
- ビルド設定:
  - `PICO_STDIO_USB=0`
  - `PICO_STDIO_UART=0`
- ビルド生成物:
  - `firmware/src/RP2040TinyUsb/build/ymm_keyboard_fw.uf2`
- 自動化:
  - `setup-rp2040-build-env.ps1` が `pico-sdk` を `tmp/pico/pico-sdk` に取得
  - `build-rp2040-tinyusb.ps1` が `cmake` / `ninja` / `arm-none-eabi-gcc` / `python` を自動探索
  - `uf2conv.py` で UF2 変換を補完

### CircuitPython 旧系ファームウェア

- `firmware/src/RP2040ZeroCode/code.py`
- `firmware/src/RP2040ZeroCode/boot.py`
- `KMK` ベース
- `usb_hid` を使うベンダー定義 HID を利用
- 実体は CircuitPython ボード上の `board.GP*` を直接参照

## 使用フレームワーク

- .NET 10 / WPF
- YukkuriMovieMaker 4 プラグイン API
- CircuitPython + KMK
- pico-sdk + TinyUSB
- KiCad
- GitHub Actions

## コンパイラ設定

- Plugin:
  - `net10.0-windows`
  - `x64` ビルドが主用途
- Firmware:
  - `CMAKE_C_STANDARD=11`
  - `CMAKE_CXX_STANDARD=17`
  - `PICO_BOARD=waveshare_rp2040_zero`
  - `Ninja` 優先、無ければ Visual Studio 17 2022 で代替

## ライブラリ依存関係の流れ

- YMM4 の DLL は `tools/scripts/fetch-ymm4-libs.ps1` で最新リリースから取得する運用
- .NET 側の NuGet は `System.IO.Ports` と `HidSharp`
- CircuitPython 側は `KMK` と各種組み込みモジュール
- C ファーム側は `pico-sdk` が外部取得

## 不要ファイル候補

以下は生成物または作業用ファイルなので、通常はソース管理対象外です。

- `firmware/src/RP2040TinyUsb/build/`
- `tmp/`
- `YMMKeyboardPlugin/obj/`
- `YMMKeyboardPlugin/bin/`
- `publish/`
- `*.zip`
- `*.uf2`
- `*.uf2` を含む一時配布物

## 備考

- `Directory.Build.props` で `YMM4DirPath` を上書きしているため、ローカル環境に依存します。
- CI での YMM4 DLL 取得は最新リリース依存のため、参照 DLL のバージョンは固定されていません。
