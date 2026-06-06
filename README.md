# YMMKeyboard

YMMKeyboard は、YukkuriMovieMaker 4 (YMM4) と連携する自作キーボードプロジェクトです。

このリポジトリでは、ハードウェア・ファームウェア・PC側処理・YMM4プラグインを一元管理しています。

## 特徴

- RP2040 ベースのコントローラ
- USB HID による入力連携
- CherryMX互換キーに対応
- キーはホットスワップ対応のソケットを実装
- YMM4 を選択してなくても入力操作対応


## 必要環境

- Windows 11
- YukkuriMovieMaker 4
- YMMKeyboard本体
- USB 接続環境

## インストール方法

- プラグイン: リリースリンク
- ファームウェア: リリースリンク

プラグインは`YMMKeyboardPlugin,ymme`を起動してください。
ファームウェアのアップデートは、未定です

## 使用方法

各ドキュメントを参照してください。

- [プラグイン](./ymm-plugin/src/YMMKeyboardPlugin/README.md)

- [ファームウェア](./firmware/src/RP2040TinyUsb/README.md)

## リポジトリ構成

- `hardware/` : 基板・筐体・部品・ハード補足資料
- `firmware/` : マイコン側実装
- `ymm-plugin/` : YMM4 プラグイン本体

## ライセンス

本リポジトリは MIT License です。詳細は [LICENSE](LICENSE) を参照してください。