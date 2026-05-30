# YMMKeyboard

YMMKeyboard は、YukkuriMovieMaker との連携を目的とした自作物理キーボードプロジェクトです。

このリポジトリには、基板設計、ファームウェア、PC側ドライバー、YMM4プラグイン、開発補助ツールをまとめて管理しています。

## リポジトリ構成

- `hardware/` - 基板、筐体、部品表、配線図
- `firmware/` - マイコン側ファームウェア
- `driver/` - PC側通信処理・ドライバー相当部分
- `ymm-plugin/` - YukkuriMovieMaker 用プラグイン
- `tools/` - 開発・診断用ツール
- `docs/` - 仕様書・設計資料

## ビルド例

```powershell
dotnet build "YMMKeyboardPlugin.slnx"
```

プラグイン本体プロジェクトは `ymm-plugin/src/YMMKeyboardPlugin/` 配下にあります。
