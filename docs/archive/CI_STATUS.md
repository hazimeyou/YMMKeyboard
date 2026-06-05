# CI_STATUS

## 現在の CI 構成

`.github/workflows/release.yml` の 1 本のみが確認できました。

### トリガー

- `push` の tag `v*`

### 実行環境

- `windows-latest`

### 実施内容

1. `actions/checkout@v4`
2. `actions/setup-dotnet@v3`
3. `VERSION` を tag 名から設定
4. `fetch-ymm4-libs.ps1` で YMM4 参照 DLL を取得
5. `dotnet build YMMKeyboardPlugin.slnx`
6. `dotnet publish ymm-plugin/src/YMMKeyboardPlugin/YMMKeyboardPlugin.csproj`
7. `LICENSE` と `readme.txt` を同梱
8. ZIP 化して `YMMKeyboardPlugin.ymme` にリネーム
9. `ncipollo/release-action@v1` で draft prerelease を作成

## 自動ビルド

- プラグインのリリース用ビルドは自動化されています。
- ただし **プルリクエスト用の検証 CI は未確認** です。
- **ファームウェアの CI ビルドは未設定** です。

## 自動テスト

- `release.yml` には `dotnet test` がありません。
- したがって CI 上での単体テスト実行は行われていません。

## リリース作成

- `ncipollo/release-action` で draft / prerelease を作成します。
- 生成物は `YMMKeyboardPlugin.ymme` です。

## 監査結果

| 項目 | 状態 | コメント |
|---|---|---|
| GitHub Actions | あり | release 用 1 本のみ |
| 自動ビルド | あり | プラグインのみ |
| 自動テスト | なし | `dotnet test` 未実行 |
| リリース作成 | あり | draft prerelease |
| ファームウェア CI | なし | ローカルビルドのみ |

## 改善候補

1. PR 用の `build + test` ワークフローを追加
2. ファームウェアの `cmake --build` を CI に追加
3. 生成物のハッシュ検証を追加
4. YMM4 DLL 取得を固定バージョン化
