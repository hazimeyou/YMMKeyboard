import board
import microcontroller
import binascii

from kmk.kmk_keyboard import KMKKeyboard
from kmk.keys import KC, make_key
from kmk.scanners import DiodeOrientation
from kmk.modules import Module
from kmk.modules.encoder import EncoderHandler
from kmk.modules.layers import Layers

# =====================================================
# 1. UID取得 (PC側で識別するため)
# =====================================================
DEVICE_UID = binascii.hexlify(
    microcontroller.cpu.uid
).decode("utf-8")

# =====================================================
# 2. シリアル通信モジュール
# (キーが押されたら PC に UID:P:SW_xx を送信)
# =====================================================
class SerialKeys(Module):
    def during_bootup(self, keyboard): pass
    def before_matrix_scan(self, keyboard): pass
    def after_matrix_scan(self, keyboard): pass
    def before_hid_send(self, keyboard): pass
    def after_hid_send(self, keyboard): pass

    def process_key(self, keyboard, key, is_pressed, int_coord):
        # 押された(P) / 離された(R)
        state = "P" if is_pressed else "R"
        
        # マトリックス上のキーの場合
        if int_coord is not None:
            print(f"{DEVICE_UID}:{state}:SW_{int_coord:02d}")
        return key

# =====================================================
# 3. キーボード設定
# =====================================================
keyboard = KMKKeyboard()
keyboard.debug_enabled = False

# ピン設定 (RP2040-Zero)
keyboard.col_pins = (board.GP2, board.GP8, board.GP7, board.GP6, board.GP5, board.GP4, board.GP3)
keyboard.row_pins = (board.GP28, board.GP27, board.GP26, board.GP15, board.GP14, board.GP29)
keyboard.diode_orientation = DiodeOrientation.COL2ROW

# キー座標の割り当て (01〜35)
keyboard.coord_mapping = [
     1,  2,  3,  4,  5,  6,
     8,  9, 10, 11, 12, 13,
    15, 16, 17, 18, 19, 20,
    22, 23, 24, 25, 26, 27,
    29, 30, 31, 32, 33, 34,
    35
]

# =====================================================
# 4. エンコーダー設定
# =====================================================
# 回転時のシリアル出力 (SW_36, SW_37)
ENC_CW = make_key(names=('ENC_CW',), on_press=lambda *args: print(f"{DEVICE_UID}:P:SW_36"))
ENC_CCW = make_key(names=('ENC_CCW',), on_press=lambda *args: print(f"{DEVICE_UID}:P:SW_37"))

encoder_handler = EncoderHandler()
encoder_handler.pins = ((board.GP0, board.GP1, None, False),)
encoder_handler.map = [((ENC_CW, ENC_CCW),),]

# =====================================================
# 5. モジュール登録
# =====================================================
serial_keys = SerialKeys()
keyboard.modules = [Layers(), serial_keys, encoder_handler]

# =====================================================
# 6. キーマップ (必要に応じて書き換えてください)
# =====================================================
# シリアル通信で制御する場合は、全て KC.NO でも動作します
keyboard.keymap = [
    [KC.NO] * 35, # レイヤー0
]

if __name__ == "__main__":
    # 起動時にUIDを表示
    print(f"UID:{DEVICE_UID}")
    keyboard.go()