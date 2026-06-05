# HARDWARE

## 対象ハードウェア

- RP2040-Zero 系ボード
- YMMKeyboard 系 PCB
- Rotary encoder 付きキー入力基板
- USB 接続の実機キーボード制御デバイス

## 現在の有効な実装

### RP2040 TinyUSB ファームウェア

現在の C ファームでは、以下のピン構成を使っています。

#### 入力ピン

- `GPIO0` : ロータリーエンコーダ A
- `GPIO1` : ロータリーエンコーダ B
- `GPIO2` : 行列スキャン列 1
- `GPIO3` : 行列スキャン列 2
- `GPIO4` : 行列スキャン列 3
- `GPIO5` : 行列スキャン列 4
- `GPIO6` : 行列スキャン列 5
- `GPIO7` : 行列スキャン列 6
- `GPIO8` : 行列スキャン列 7
- `GPIO14` : 行列スキャン行 5
- `GPIO15` : 行列スキャン行 4
- `GPIO26` : 行列スキャン行 1
- `GPIO27` : 行列スキャン行 2
- `GPIO28` : 行列スキャン行 3
- `GPIO29` : 行列スキャン行 6

#### 出力ピン

- 現行アプリ実装では、GPIO出力を使う独自機能はありません。
- USB HID / USB CDC は USB デバイス機能として扱っています。

#### 使用していない周辺機能

- I2C: 未使用
- SPI: 未使用
- UART: 未使用
- ADC: 未使用
- PWM: 未使用
- PIO: 未使用

### CircuitPython 旧系実装

旧系 `code.py` では以下を使っています。

- `keyboard.col_pins`:
  - `GP2, GP8, GP7, GP6, GP5, GP4, GP3`
- `keyboard.row_pins`:
  - `GP28, GP27, GP26, GP15, GP14, GP29`
- `EncoderHandler`:
  - `GP0, GP1`

## スイッチ割り当ての考え方

- `SW01` から `SW35` は行列スキャン由来
- `SW36` / `SW37` はロータリーエンコーダ由来
- 旧系では `SW35` と `SW36/SW37` の並びに注意が必要です

## 接続デバイス

### USB

- `USB CDC`: デバッグ/ログ出力
- `USB HID`: ベンダー定義 HID

### 実機確認で見えている識別子

- `YMM RP2040 Control Keyboard`
- `YMMKeyboard Project`
- Vendor ID: `0x2E8A`
- Product ID: `0x4020`

## KiCad 側の状況

現行リポジトリには複数の PCB 設計が存在します。

- `hardware/pcb/KiCad/YMMkeyboard/`
- `hardware/pcb/KiCad/YMMkeyboardMX/`
- `hardware/pcb/KiCad/YMMkeyboardRP/YMMkeyboardRP/`
- `hardware/pcb/KiCad/testboard/testboard/`

### 観察できる要素

- RP2040 周辺の `USB_D+`, `USB_D-`, `QSPI_*`, `GPIO26_ADC0` などのネットが存在
- エンコーダ部品 `RotaryEncoder_Switch` の参照が複数ある
- `OLED` 記載がある設計もあるが、現行ファームでは未使用

## 調査上の注意

- 実際の GPIO 配線は `GP*` のコード参照と KiCad 設計を一致させる必要があります。
- 現行 C ファームは実機入力スキャンの最小実装段階なので、基板の確定配線に合わせた再確認が必要です。
