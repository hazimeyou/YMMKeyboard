import board
import microcontroller
import binascii
import time
import supervisor

from kmk.kmk_keyboard import KMKKeyboard
from kmk.keys import KC
from kmk.scanners import DiodeOrientation
from kmk.modules.layers import Layers
from kmk.modules import Module
from kmk.modules.encoder import EncoderHandler
from kmk.extensions.media_keys import MediaKeys
from kmk.extensions.rgb import RGB  # LED用に追加

# =====================================================
# デバイス固有 UID
# =====================================================
DEVICE_UID = binascii.hexlify(
    microcontroller.cpu.uid
).decode("utf-8")

# =====================================================
# Serial UID Broadcaster
# =====================================================
class SerialCommunication(Module):
    def __init__(self):
        self.last_send = 0
        self.first = True

    def during_bootup(self, keyboard): pass
    def before_hid_send(self, keyboard): pass
    def after_hid_send(self, keyboard): pass
    def before_matrix_scan(self, keyboard): pass
    def after_matrix_scan(self, keyboard): pass

    def on_runtime_loop(self, keyboard):
        now = time.monotonic()
        # 接続直後の送信
        if supervisor.runtime.serial_connected and self.first:
            self.first = False
            for _ in range(3):
                print(f"UID:{DEVICE_UID}")
                time.sleep(0.1)

        # 1秒ごとの生存確認
        if now - self.last_send >= 1.0:
            print(f"UID:{DEVICE_UID}")
            self.last_send = now

    def process_key(self, keyboard, key, is_pressed, int_coord):
        # キー入力イベントを送信
        if key:
            state = "P" if is_pressed else "R"
            print(f"{DEVICE_UID}:{state}:SW_{int_coord:02d}")
        return key

# =====================================================
# KMK Keyboard setup
# =====================================================
keyboard = KMKKeyboard()
encoder_handler = EncoderHandler()

keyboard.modules = [
    Layers(),
    encoder_handler,
    SerialCommunication(),
]

keyboard.extensions.append(MediaKeys())

# ---- RGB LED (GP9) ----
# ピン9にRGBがつながっているとのことなので追加しておきます
rgb = RGB(pixel_pin=board.GP9, num_pixels=1, val_limit=255, hue_default=0, sat_default=255, val_default=100)
keyboard.extensions.append(rgb)

# ---- Matrix (キースイッチ) ----
# 配線情報に基づいて修正
# ダイオードなし側（列）: GP2(エンコーダーSW), GP8, 7, 6, 5, 4, 3
keyboard.col_pins = (
    board.GP2,  # Col 0: エンコーダーのスイッチ
    board.GP8,  # Col 1
    board.GP7,  # Col 2
    board.GP6,  # Col 3
    board.GP5,  # Col 4
    board.GP4,  # Col 5
    board.GP3   # Col 6
)

# ダイオードあり側（行）: GP28, 27, 26, 15, 14, GP29(エンコーダーSW)
keyboard.row_pins = (
    board.GP28, # Row 0
    board.GP27, # Row 1
    board.GP26, # Row 2
    board.GP15, # Row 3
    board.GP14, # Row 4
    board.GP29  # Row 5: エンコーダーのスイッチ
)

keyboard.diode_orientation = DiodeOrientation.COL2ROW

# ---- Encoder (つまみ回転) ----
# GP0 と GP1 が回転検知
encoder_handler.pins = (
    (board.GP0, board.GP1, None, False),
)

# 回転時の動作（左回転=左矢印, 右回転=右矢印）
encoder_handler.map = [
    ((KC.LEFT, KC.RIGHT),),
]

# ---- Keymap ----
# 7列 x 6行 = 42キー分のマップ
# とりあえず全て「A」にしていますが、あとで好きなキーに変えてください
keyboard.keymap = [
    [KC.A] * 42
]

# =====================================================
# Main
# =====================================================
if __name__ == "__main__":
    keyboard.debug_enabled = False
    keyboard.go()