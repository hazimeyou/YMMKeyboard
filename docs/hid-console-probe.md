# HID Console Probe

## 目的

`YMM4` と `YMMKeyboard` plugin を経由せず、Windows 上の独立したコンソールアプリで RP2040 の HID report を直接読む。

これにより次を切り分ける。

- firmware が report を送っていない
- plugin の受信処理が report を取り逃がしている

## ツール

- `tools/YMMKeyboard.HidConsoleProbe`

## 対象

- `VID = 0x2E8A`
- `PID = 0x4020`

## CLI

```powershell
dotnet run --project tools/YMMKeyboard.HidConsoleProbe -- --vid 2E8A --pid 4020 --timeout-ms 500 --duration-sec 30
```

### 主な引数

- `--vid`
- `--pid`
- `--index`
- `--timeout-ms`
- `--duration-sec`
- `--output-dir`

## 出力

保存先:

- `tmp/hid-console-probe/`

生成物:

- `hid-console-probe.log`
- `hid-console-probe.json`

## 記録内容

### HID 列挙

各 device について記録する。

- `path`
- `productName`
- `manufacturer`
- `serial`
- `usagePage`
- `usage`
- `maxInputReportLength`
- `maxOutputReportLength`
- `maxFeatureReportLength`

### Raw Read

選択した device を open して report を読む。

- `openSucceeded`
- `readLoopStarted`
- `readAttemptCount`
- `readSuccessCount`
- `readTimeoutCount`
- `lastException`

### Report Dump

report を受信した場合は hex dump と ASCII decode を残す。

例:

```text
[12:34:56.789] len=63 data=01 53 57 5F 30 30 ... ascii=SW_00
```

## 判定

- `readSuccessCount = 0`
  - firmware 側が送っていない可能性が高い
- `readSuccessCount > 0`
  - plugin 側の受信経路との差分を疑う

## 注意

- `YMM4` は不要
- plugin は不要
- macro は不要
- input 操作結果の確認は不要
