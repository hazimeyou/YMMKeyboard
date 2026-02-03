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

# =====================================================
# デバイス固有 UID
# =====================================================
DEVICE_UID = binascii.hexlify(
    microcontroller.cpu.uid
).decode("utf-8")

i = 0


# =====================================================
# Serial UID Broadcaster (KMK Module)
# =====================================================
class SerialCommunication(Module):
    def __init__(self):
        self.last_send = 0
        self.first = True

    # ---- KMK必須フック（空実装） ----
    def during_bootup(self, keyboard): pass
    def before_hid_send(self, keyboard): pass
    def after_hid_send(self, keyboard): pass
    def before_matrix_scan(self, keyboard): pass
    def after_matrix_scan(self, keyboard): pass
    # --------------------------------

    def on_runtime_loop(self, keyboard):
        now = time.monotonic()
        print(f"UID:{DEVICE_UID}")
        # 接続直後に複数回送信（取りこぼし防止）
        if supervisor.runtime.serial_connected and self.first:
            self.first = False
            for _ in range(3):
                print(f"UID:{DEVICE_UID}")
                time.sleep(0.1)

        # 1秒ごとにブロードキャスト
        if now - self.last_send >= 1.0:
            print(f"UID:{DEVICE_UID}")
            self.last_send = now

    def process_key(self, keyboard, key, is_pressed, int_coord):
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
    SerialCommunication(),  # ← UIDブロードキャスト
]

keyboard.extensions.append(MediaKeys())

# ---- Matrix ----
keyboard.col_pins = (
    board.GP0, board.GP1, board.GP2,
    board.GP3, board.GP4, board.GP5, board.GP6
)

keyboard.row_pins = (
    board.GP28, board.GP27, board.GP26,
    board.GP15, board.GP14, board.GP29
)

keyboard.diode_orientation = DiodeOrientation.COL2ROW

# ---- Encoder ----
encoder_handler.pins = (
    (board.GP8, board.GP7, None, False),
)
encoder_handler.map = [
    ((KC.RIGHT, KC.LEFT),),
]

# ---- Keymap ----
keyboard.keymap = [
    [KC.A] * 42
]

# =====================================================
# Main
# =====================================================
if __name__ == "__main__":
    keyboard.debug_enabled = False
    keyboard.go()
