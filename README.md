# YMMKeyboard

YMMKeyboard は、YukkuriMovieMaker 4 (YMM4) と連携する自作キーボードプロジェクトです。

このリポジトリでは、ハードウェア・ファームウェア・PC側処理・YMM4プラグインを一元管理しています。

## 特徴

- RP2040 ベースのコントローラ
- USB HID による入力連携
- Matrix keyboard 対応
- Rotary encoder 対応
- YMM4 操作対応
- Legacy COM diagnostic fallback 対応

## 必要環境

- Windows 10 または Windows 11
- YukkuriMovieMaker 4
- RP2040 ベースのデバイス
- USB 接続環境

## インストール方法

- プラグイン: `ymm-plugin/src/YMMKeyboardPlugin/bin/Debug/net10.0-windows/YMMKeyboardPlugin.dll`
- ファームウェア: `firmware/src/RP2040TinyUsb/build/ymm_keyboard_fw.uf2`

配布物を展開したあと、YMM4 のプラグイン配置先に `YMMKeyboardPlugin.dll` を配置し、RP2040 デバイスへ `ymm_keyboard_fw.uf2` を書き込みます。

## 使用方法

1. RP2040 デバイスへファームウェアを書き込みます。
2. YMM4 を起動します。
3. プラグインが認識されていることを確認します。
4. キー入力を行い、Matrix 入力が反映されることを確認します。
5. Rotary encoder を回して、対応操作が反映されることを確認します。

## 既知の制限

- RP2040 ファームウェア前提で動作します。
- Legacy COM diagnostic fallback は補助的な確認用途です。
- 一部の機能や見せ方は今後の公開後に拡張される可能性があります。

## ライセンス

本リポジトリは MIT License です。詳細は [LICENSE](LICENSE) を参照してください。

## リポジトリ構成

- `hardware/` : 基板・筐体・部品・ハード補足資料
- `firmware/` : マイコン側実装
- `driver/` : PC側通信/制御関連
- `ymm-plugin/` : YMM4 プラグイン本体
- `docs/release-prep/` : 公開準備用の release note / freeze 記録

## ビルド

```powershell
dotnet build "YMMKeyboardPlugin.slnx"
```

プラグイン本体は `ymm-plugin/src/YMMKeyboardPlugin/` にあります。
