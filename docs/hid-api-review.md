# HID API Review

## Current API

`ymm-plugin/src/YMMKeyboardPlugin/Hid/HidDeviceProbe.cs` の `EnumerateAll()` で、`HidSharp.HidDevice` の obsolete 属性付きプロパティを参照している。

該当箇所:

- `ProductName = d.ProductName ?? string.Empty` at [HidDeviceProbe.cs#L17](/C:/Users/yu-za-hazimeyou/source/repos/YMMKeyboard/ymm-plugin/src/YMMKeyboardPlugin/Hid/HidDeviceProbe.cs#L17)
- `Manufacturer = d.Manufacturer ?? string.Empty` at [HidDeviceProbe.cs#L18](/C:/Users/yu-za-hazimeyou/source/repos/YMMKeyboard/ymm-plugin/src/YMMKeyboardPlugin/Hid/HidDeviceProbe.cs#L18)

build 時の警告:

- `HidDevice.ProductName` obsolete warning
- `HidDevice.Manufacturer` obsolete warning

## Recommended API

HidSharp 2.1.0 の assembly では、以下のメソッドが obsolete ではない。

- `GetProductName()`
- `GetManufacturer()`

`HidSharp.XML` でも、これらは通常の public メソッドとして記載されている一方、`ProductName` と `Manufacturer` のプロパティ側は `<exclude />` 扱いになっている。

## Non-deprecated Reason

調査した HidSharp 2.1.0 では、`ProductName` と `Manufacturer` は `ObsoleteAttribute` が付いたプロパティで、`GetProductName()` と `GetManufacturer()` が後継の参照手段になっている。

プロパティにメッセージは付いていないため、パッケージ内部の移行案内は弱いが、コンパイラ警告はこの属性由来で発生している。

将来削除予定の明示メッセージは今回のパッケージ調査では見つからなかった。

## Impact Scope

この警告は `HidDeviceProbe.cs` のみで発生している。

影響があるのは次の診断系データ生成:

- `YMMKeyboardPlugin.Diagnostics.PluginConnectionDiagnosticCollector`
- plugin diagnostics JSON
- diagnostics comparer の入力
- DeviceInspector との比較ベースライン

関連する他の実装では、すでに `GetProductName()` / `GetManufacturer()` を使っているか、今回の警告対象とは別の読み出し経路を使っている。

具体例:

- `tools/YMMKeyboard.DeviceInspector/Program.cs`
- `ymm-plugin/src/YMMKeyboardPlugin/Hid/HidKeyboardLink.cs`

## Migration Difficulty

低から中程度。

理由:

- 参照箇所は `HidDeviceProbe.cs` の 2 行のみ
- データ型 `HidDeviceInfo` の構造は維持できる
- 既存の JSON 形式や比較ロジックを変えずに置換できる可能性が高い

## Risk

- 置換後に空文字の扱いが変わる可能性がある
- 一部の HID デバイスでは、プロパティとメソッドの戻り値差が出る可能性がある
- ただし今回の対象は列挙・診断用途であり、入力送信やファームウェア動作には影響しない

## Suggested Timing

Hardware Validation RC2 の開始前、または RC2 で取得した実機観測が安定した後に移行するのがよい。

理由:

- 今は formal identity の観測を優先したい
- 警告の解消は機能追加ではなく保守作業
- 実機検証と同時に API 変更を入れると、観測差分の切り分けがやや難しくなる

## Recommendation

対応は必要だが、RC2 の観測開始を妨げるレベルではない。

優先度は「中」。

理由:

- build は成功している
- 警告は 2 件だけ
- 警告元は 1 ファイルに限定されている
- ただし、将来の HidSharp 更新で削除対象になる可能性があるため、RC2 後に整理するのが安全

## Cleanup Applied

- `HidDeviceProbe.cs` の obsolete プロパティ参照を `GetProductName()` / `GetManufacturer()` に置換済み
- 挙動の fallback は維持
- 期待される obsolete warning は 2 件とも解消済み
