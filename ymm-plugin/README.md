# ymm-plugin

YukkuriMovieMaker 4 向けプラグイン関連を配置します。

## 構成

- `src/` : プラグイン本体ソース
- `tests/` : テスト
- `libs/` : YMM4 参照 DLL など
- `samples/` : サンプル/補助データ

## 現在の要点

- `YMMT読み込み` で `.ymmt` 読み込みとテンプレート実行の両方に対応
- 設定UIでテンプレート一覧を選択可能
- `TestEvent` は最小動作 (MessageBox) のみ
