import board
import microcontroller
import binascii
from kmk.kmk_keyboard import KMKKeyboard
from kmk.keys import KC
from kmk.scanners import DiodeOrientation
from kmk.modules.layers import Layers
from kmk.modules import Module
from kmk.modules.encoder import EncoderHandler
from kmk.extensions.media_keys import MediaKeys

# --- 1. デバイス固有IDの取得 ---
device_uid = binascii.hexlify(microcontroller.cpu.uid).decode('utf-8')

class DeviceIdReporter(Module):
    def process_key(self, keyboard, key, is_pressed, int_coord):
        if key:
            state = "P" if is_pressed else "R"
            print(f"UID:{device_uid} | SW_{int_coord:02d} | {state}")
        return key

keyboard = KMKKeyboard()
encoder_handler = EncoderHandler()
keyboard.modules = [Layers(), encoder_handler, DeviceIdReporter()]
keyboard.extensions.append(MediaKeys())

# --- 2. 最新リストに基づくピン設定 ---
# 列 (C0-C5, Btn): GP0, 1, 2, 3, 4, 5, 6
keyboard.col_pins = (board.GP0, board.GP1, board.GP2, board.GP3, board.GP4, board.GP5, board.GP6)

# 行 (R0-R4, BtnRow): GP28, 27, 26, 15, 14, 29
keyboard.row_pins = (board.GP28, board.GP27, board.GP26, board.GP15, board.GP14, board.GP29)

# 「14〜28がダイオードの先」ならこれ！
keyboard.diode_orientation = DiodeOrientation.COL2ROW

# --- 3. エンコーダー設定 ---
encoder_handler.pins = ((board.GP8, board.GP7, None, False),)
encoder_handler.map = [((KC.AUDIO_VOL_DOWN, KC.AUDIO_VOL_UP),)]

# --- 4. キーマップ ---
keyboard.keymap = [[KC.A] * 42]

if __name__ == '__main__':
    print(f"--- 1号機 診断開始 (ID: {device_uid}) ---")
    keyboard.go()