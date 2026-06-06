# RP2040 TinyUSB 独自ファーム（C）

このフォルダは `CircuitPython` ではなく、`pico-sdk + TinyUSB` で実装した  
YMMキーボード向けの独自ファームです。

## 現在の実装

- USB CDC（シリアル）出力
- Vendor HID（Report ID=1, 63-byte fixed payload）出力
- 5秒ごとに診断イベント `SW_00` の `P/R` を送信
- 3秒ごとに `HB:<uid>` を CDC に送信

## 前提

- Windows
- CMake 3.13以上
- Ninja（推奨）
- ARM GCC（`arm-none-eabi-gcc`）
- `pico-sdk`

## 自動セットアップ（推奨）

リポジトリ直下で実行:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\scripts\setup-rp2040-build-env.ps1
```

このスクリプトは以下を実行します。

- `tmp\pico\pico-sdk` を自動取得
- `PICO_SDK_PATH` を自動設定（プロセス内）
- `firmware\src\RP2040TinyUsb\build` を生成

## 自動ビルド（推奨）

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\scripts\build-rp2040-tinyusb.ps1
```

生成物:

- `firmware\src\RP2040TinyUsb\build\ymm_keyboard_fw.uf2`

## 手動ビルド

```powershell
$env:PICO_SDK_PATH="C:\path\to\pico-sdk"
cd firmware\src\RP2040TinyUsb
cmake -S . -B build -G Ninja
cmake --build build
```

## 書き込み

1. RP2040 Zero を `BOOTSEL` でマスストレージ起動
2. `ymm_keyboard_fw.uf2` をドラッグ&ドロップ

## 期待されるPC側挙動

- CDC: `UID` / `HB` / `...:SW_00` が出力される
- HID: `YMMK:<uid>:P:SW_00` / `YMMK:<uid>:R:SW_00` が送信される

## 次の実装予定

- 実機配線に合わせたマトリクススキャン
- ロータリーエンコーダ対応（`SW_36` / `SW_37`）
- デバウンスと押下状態管理

