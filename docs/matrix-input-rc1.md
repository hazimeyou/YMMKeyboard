# Matrix Input RC1

## 目的

`matrix-reverse-direction-probe-rc1` で成立した reverse scan を正式な入力スキャンとして採用し、実キー押下を `MATRIX_KEY` と HID report に流す。

## 前提

- 正式採用: `ROW=output / COL=input pull-up`
- 非採用: `COL=output / ROW=input pull-up`
- reverse 方向では idle `COLS=1111111`
- `REV_COL_EDGE` / `REV_MATRIX_CANDIDATE` は観測済み

## 実装方針

- reverse scan を canonical scan として継続する
- `MATRIX_KEY row=<r> col=<c> keyId=K_<r>_<c> state=P/R` を CDC に出す
- HID には `K_<r>_<c>:P` / `K_<r>_<c>:R` を送る
- key label 変換は行わない

## 最近の実機確認

- `MATRIX_KEY` が CDC で観測された
- `hidSendAttemptCount` がキー押下に応じて増加した
- `hidReadyTrueCount` も送信試行に追随した
- `hidReadyFalseCount` は 0 のままだった
- `hidReportCallCount` が増加した
- `lastSendResult=true` が観測された

## 次の観測

- `MATRIX_KEY observed`
- `K_* HID report observed`
- その後に Plugin `InputReceived` へ進む

## Latest Matrix HID Host Correlation

- Latest host correlation run: `matrix-hid-host-correlation-rc1`
- CDC observed `MATRIX_KEY` and `HID_DIAG ... sendResult=true`
- HID probe `readSuccessCount=0`
- `K_*` was not observed on the host in that run
- see [Matrix HID Host Correlation RC1](./matrix-hid-host-correlation.md)

## Latest Minimal Send Probe

- A minimal matrix HID probe now sends `TEST_HID_MATRIX_<counter>` on press only.
- The probe keeps CDC matrix logging and uses the same `send_hid_report()` path as the forced test traffic.
- The goal of the next run is to confirm whether a matrix-triggered `TEST_HID_` payload is visible on the host.
- See [Matrix HID Minimal Send Probe RC1](./matrix-hid-minimal-send-probe.md).


## Payload Stepdown Follow-up

- The next probe steps the payload family down from `TEST_HID_` to `K_*`.
- The stepdown order is documented in [Matrix HID Payload Stepdown RC1](./matrix-hid-payload-stepdown.md).

## Latest Host Result

- All five payload variants were received on the host when sent one per press.
- The observed host classification was:
  - `TEST_HID`
  - `TEST_KEY`
  - `KEY`
  - `K_UNDERSCORE`
  - `K_COLON`
- This means the host can receive the original `K_*` family when the payload is stepped down one press at a time.

## Formal Payload Follow-up

- The matrix input path now returns to the formal payload `K_<row>_<col>:P/R`.
- The formal payload flow is documented in [Matrix Input Formal Payload RC1](./matrix-input-formal-payload-rc1.md).
- The formal HID report length is now fixed to 63 bytes so the on-wire report shape matches the working variant-per-press transport path.
- The fixed-length formal payload was validated on the host as `K_COLON`.
- The next live check is plugin-side `InputReceived`; see [Plugin InputReceived RC1](./plugin-inputreceived-rc1.md).
