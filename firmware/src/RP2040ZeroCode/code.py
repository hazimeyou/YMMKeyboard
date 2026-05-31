import binascii
import board
import microcontroller
import usb_hid

from kmk.kmk_keyboard import KMKKeyboard
from kmk.keys import make_key
from kmk.modules import Module
from kmk.modules.encoder import EncoderHandler
from kmk.modules.layers import Layers
from kmk.scanners import DiodeOrientation

DEVICE_UID = binascii.hexlify(microcontroller.cpu.uid).decode("utf-8")


def _find_custom_hid():
    for dev in usb_hid.devices:
        try:
            if dev.usage_page == 0xFF00 and dev.usage == 0x0001:
                return dev
        except AttributeError:
            pass
    return None


CUSTOM_HID = _find_custom_hid()


def emit_event(state, switch_id):
    # PC plugin parses this text format: UID:P:SW_01
    line = f"{DEVICE_UID}:{state}:SW_{switch_id:02d}"
    hid_line = f"YMMK:{line}"

    # Serial (existing path)
    print(line)

    # HID (new path): ASCII + zero padding into 64-byte report
    if CUSTOM_HID is not None:
        try:
            payload = hid_line.encode("ascii", "ignore")
            if len(payload) > 64:
                payload = payload[:64]
            report = payload + bytes(64 - len(payload))
            CUSTOM_HID.send_report(report)
        except Exception:
            # Keep serial path alive even when HID send fails
            pass


class EventEmitter(Module):
    def during_bootup(self, keyboard):
        pass

    def before_matrix_scan(self, keyboard):
        pass

    def after_matrix_scan(self, keyboard):
        pass

    def before_hid_send(self, keyboard):
        pass

    def after_hid_send(self, keyboard):
        pass

    def process_key(self, keyboard, key, is_pressed, int_coord):
        if int_coord is not None:
            emit_event("P" if is_pressed else "R", int_coord)
        return key


keyboard = KMKKeyboard()
keyboard.debug_enabled = False

# RP2040 Zero pin map
keyboard.col_pins = (board.GP2, board.GP8, board.GP7, board.GP6, board.GP5, board.GP4, board.GP3)
keyboard.row_pins = (board.GP28, board.GP27, board.GP26, board.GP15, board.GP14, board.GP29)
keyboard.diode_orientation = DiodeOrientation.COL2ROW

# Switch IDs (01-35)
keyboard.coord_mapping = [
    1, 2, 3, 4, 5, 6,
    8, 9, 10, 11, 12, 13,
    15, 16, 17, 18, 19, 20,
    22, 23, 24, 25, 26, 27,
    29, 30, 31, 32, 33, 34,
    35,
]


def _emit_tap(sw_id):
    emit_event("P", sw_id)
    emit_event("R", sw_id)


ENC_CW = make_key(names=("ENC_CW",), on_press=lambda *args: _emit_tap(36))
ENC_CCW = make_key(names=("ENC_CCW",), on_press=lambda *args: _emit_tap(37))

encoder_handler = EncoderHandler()
encoder_handler.pins = ((board.GP0, board.GP1, None, False),)
encoder_handler.map = [((ENC_CW, ENC_CCW),),]

event_emitter = EventEmitter()
keyboard.modules = [Layers(), event_emitter, encoder_handler]

# Keep matrix keys from sending normal keyboard HID keycodes.
SERIAL_ONLY = make_key(names=("SERIAL_ONLY",), on_press=lambda *args: None, on_release=lambda *args: None)
keyboard.keymap = [[SERIAL_ONLY] * 35]

if __name__ == "__main__":
    print(f"UID:{DEVICE_UID}")
    keyboard.go()
