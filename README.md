# YMMKeyboard

YMMKeyboard は、シリアル接続された外部キーボードを YukkuriMovieMaker 4 から扱うためのプラグインです。
YMM 上の UI キーボード操作と、実機からの COM ポート入力を分けて設定できます。

## ドキュメント

- プラグイン本体の説明: [`YMMKeyboardPlugin/README.md`](/YMMKeyboardPlugin/README.md)

## できること

- COM ポートを設定画面から選択できる
- 接続ボタンでシリアル監視を開始し、UID を取得できる
- 接続解除ボタンで監視を停止できる
- 起動時に接続する COM ポートを複数指定できる
- UI キーボード用と実機用で別々にキー割り当てを持てる
- 実機は UID ごとに別の割り当てを保存できる
- 単独キーだけでなく、複数キーの組み合わせにも動作を割り当てられる
- 設定はプラグイン DLL と同じフォルダー配下の `settings/YMMKeyboardSettings.json` に保存される

## 現在の主なアクション

- `None`
- `TestEvent`
- `PlusSeekFrame`
- `MinusSeekFrame`

必要なアクションは [`YMMKeyboardPlugin/Mapping/MappingConverter.cs`](/YMMKeyboardPlugin/Mapping/MappingConverter.cs) に追加して拡張できます。

## フォルダー構成

- `YMMKeyboardPlugin`
  YMM プラグイン本体
- `RP2040ZeroCode`
  実機側コード
- `libs`
  ビルド時に参照する YMM 関連 DLL
- `KiCad`, `gerber`, `jww`
  ハードウェア設計関連
- `txt`
  ymmeに同梱するファイル
## プラグインの主な構成

- [`YMMKeyboardPlugin/Plugin/MyToolPlugin.cs`](/C:/Users/hazimeyou/source/repos/hazimeyou/YMMKeyboard/YMMKeyboardPlugin/Plugin/MyToolPlugin.cs)
  YMM に登録するプラグイン本体
- [`YMMKeyboardPlugin/Views/YMMKeyboardSettingsPanel.xaml`](/C:/Users/hazimeyou/source/repos/hazimeyou/YMMKeyboard/YMMKeyboardPlugin/Views/YMMKeyboardSettingsPanel.xaml)
  COM ポート設定とキー設定ウィンドウ起動用の設定画面
- [`YMMKeyboardPlugin/Views/KeyboardMappingWindow.xaml`](/C:/Users/hazimeyou/source/repos/hazimeyou/YMMKeyboard/YMMKeyboardPlugin/Views/KeyboardMappingWindow.xaml)
  単独キーと複数キーの割り当てを編集する画面
- [`YMMKeyboardPlugin/Views/KeyboardView.xaml`](/C:/Users/hazimeyou/source/repos/hazimeyou/YMMKeyboard/YMMKeyboardPlugin/Views/KeyboardView.xaml)
  YMM 内から手動実行できる UI キーボード
- [`YMMKeyboardPlugin/Settings/YMMKeyboardSettings.cs`](/C:/Users/hazimeyou/source/repos/hazimeyou/YMMKeyboard/YMMKeyboardPlugin/Settings/YMMKeyboardSettings.cs)
  設定の読み書きと保存先管理
- [`YMMKeyboardPlugin/Key/Keymacro.cs`](/C:/Users/hazimeyou/source/repos/hazimeyou/YMMKeyboard/YMMKeyboardPlugin/Key/Keymacro.cs)
  実機の接続管理とキーイベント処理
- [`YMMKeyboardPlugin/SerialKeyboard/SerialKeyboardLink.cs`](/C:/Users/hazimeyou/source/repos/hazimeyou/YMMKeyboard/YMMKeyboardPlugin/SerialKeyboard/SerialKeyboardLink.cs)
  シリアル通信の受信ループ

## セットアップ

### 1. 必要なもの

- .NET SDK 10
- YukkuriMovieMaker 4
- シリアル接続できる実機

### 2. ビルド

通常はリポジトリ直下で次を実行します。

```powershell
dotnet build "YMMKeyboardPlugin\YMMKeyboardPlugin.csproj" --no-restore /p:YMM4DirPath=
```

YMM4 のプラグインフォルダーへ自動コピーしたい場合は、`YMM4DirPath` に YMM4 のルートを指定します。

```powershell
dotnet build "YMMKeyboardPlugin\YMMKeyboardPlugin.csproj" /p:YMM4DirPath="C:\Path\To\YMM4"
```

### 3. 配置

ビルドした `YMMKeyboardPlugin.dll` を YMM4 の `user\plugin\YMMKeyboardPlugin` に配置します。

`YMM4DirPath` を指定してビルドした場合は、ビルド後に自動でコピーされます。

## 使い方

### COM ポート設定

1. YMM の設定画面からキーボードプラグイン設定を開く
2. COM ポートをプルダウンから選ぶ
3. 必要に応じて `起動時接続` に追加する
4. `接続` を押して UID を取得する
5. UID が認識されたらキー割り当て画面で編集する

### キー割り当て

1. 設定画面からキー割り当てウィンドウを開く
2. `UIキーボード` か実機 UID を選ぶ
3. 単独キーはそのままキーを選んで設定する
4. 組み合わせを設定する場合は `複数キー編集モード` をオンにする
5. 2つ以上のキーを選んでアクションを設定する

### UI キーボードからの実行

- 通常モードでは、押したキーの単独設定を実行します
- `複数キー実行モード` では、複数キーを選んでまとめて実行できます

## 設定ファイル

設定ファイルはプラグイン DLL があるフォルダー配下に作成されます。

- フォルダー: `settings`
- ファイル名: `YMMKeyboardSettings.json`

保存内容:

- 現在選択中の COM ポート
- 起動時接続する COM ポート一覧
- 既知 UID 一覧
- UI キーボード用の単独キー割り当て
- UI キーボード用の複数キー割り当て
- 実機 UID ごとの単独キー割り当て
- 実機 UID ごとの複数キー割り当て

## シリアル入力形式

現在の受信形式は次の前提です。

```text
<uid>:<state>:SW_<number>
```

例:

```text
504434042839481c:P:SW_12
504434042839481c:R:SW_12
```

- `uid`
  デバイス固有 ID
- `state`
  `P` で押下、`R` で離上
- `SW_<number>`
  スイッチ番号

## 実装メモ

- 実機の複数キー判定は [`YMMKeyboardPlugin/Key/Keymacro.cs`](/C:/Users/hazimeyou/source/repos/hazimeyou/YMMKeyboard/YMMKeyboardPlugin/Key/Keymacro.cs) で、単独キーを少し遅延させてから確定しています。
  同時押しを優先するための処理なので、反応速度を詰める場合はこの遅延時間の見直しが必要です。
- 設定保存は独自 JSON 方式です。
  保存失敗時は現状 `Debug.WriteLine` ベースなので、将来的には設定画面へ保存エラー表示を出したいです。
- 実機の COM ポート自動接続は複数指定できますが、存在しないポートや使用中ポートに対する UI 表示はまだ改善余地があります。
- アクション種別はまだ少なめです。
  本番アクションを増やす場合は `MappingConverter` を入口に追加していく想定です。

## 難しい部分のメモ

- WPF の XAML ファイルは、環境によっては編集中やビルド中に一時ロックされることがあります。
  編集できない場合は YMM 本体、Visual Studio、XAML デザイナの状態を確認してください。
- 一部ファイルは文字コードの都合で、コンソール上では日本語が崩れて見えることがあります。
  実ファイル自体を UTF-8 に寄せていく余地がありますが、現段階では機能優先で進めています。
- UI キーボードと実機入力は設定を分離しています。
  どちらの動作か分からなくなったら、まず `UIキーボード` と `UID` の編集対象が合っているか確認してください。

## 注意
## - 現在の実装は開発中のものであり、予告なく仕様変更や不具合が発生する可能性があります。
## - このプラグイン及び実機側コードはAIによる生成物であり、動作や品質を完全には保証できません。

## ライセンス

ライセンスは [`LICENSE.txt`](/C:/Users/hazimeyou/source/repos/hazimeyou/YMMKeyboard/LICENSE.txt) を参照してください。
