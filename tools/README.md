# tools

開発補助ツールと運用スクリプトを配置します。

## 構成

- `KeyboardEventStateTester/` : キーイベント状態確認ツール
- `scripts/` : ビルド/検証補助スクリプト

## 運用

再現調査時は、ツール実行手順と結果を `docs/` に記録してください。

## RP2040 Cファーム自動ビルド

1. 初回セットアップ
`powershell -ExecutionPolicy Bypass -File .\tools\scripts\setup-rp2040-build-env.ps1`

2. ビルド
`powershell -ExecutionPolicy Bypass -File .\tools\scripts\build-rp2040-tinyusb.ps1`
