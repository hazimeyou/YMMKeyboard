import board
from kmk.kmk_keyboard import KMKKeyboard
from kmk.keys import KC
from kmk.scanners import DiodeOrientation

keyboard = KMKKeyboard()

# 回路図に基づくピン設定
keyboard.row_pins = (board.GP29, board.GP28)
keyboard.col_pins = (board.GP15, board.GP14)

# ダイオードの向き (回路図の D1, D2 等の向き)
keyboard.diode_orientation = DiodeOrientation.COL2ROW

# キーマップ設定 (2x2マトリックス)
# [Row0-Col0, Row0-Col1],
# [Row1-Col0, Row1-Col1]
keyboard.keymap = [
    [KC.A, KC.B],
    [KC.C, KC.D],
]

if __name__ == '__main__':
    keyboard.go()