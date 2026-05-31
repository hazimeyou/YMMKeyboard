# YMMKeyboard

YMMKeyboard は、YukkuriMovieMaker 4 (YMM4) と連携する自作キーボードプロジェクトです。

このリポジトリでは、ハードウェア・ファームウェア・PC側処理・YMM4プラグイン・補助ツールを一元管理しています。

## リポジトリ構成

- `hardware/` : 基板・筐体・部品・ハード補足資料
- `firmware/` : マイコン側実装
- `driver/` : PC側通信/制御関連
- `ymm-plugin/` : YMM4 プラグイン本体とテスト
- `tools/` : 検証ツールと運用スクリプト
- `docs/` : 全体仕様、運用メモ、検証手順

## ビルド

```powershell
dotnet build "YMMKeyboardPlugin.slnx"
```

プラグイン本体は `ymm-plugin/src/YMMKeyboardPlugin/` にあります。
