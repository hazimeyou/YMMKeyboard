# Rotary Encoder Probe RC1

## Purpose

Add a firmware-level probe for the rotary encoder on `GP0/GP1` so the rotary path can be observed independently from the matrix scan.

## Source of Truth

- `firmware/src/RP2040ZeroCode/code.py`
- `encoder_handler.pins = ((board.GP0, board.GP1, None, False),)`
- `ENC_CW` emits switch `36`
- `ENC_CCW` emits switch `37`

## Firmware Probe

- Input pins:
  - `GP0` = encoder A
  - `GP1` = encoder B
- Pull mode:
  - input pull-up
- Decode style:
  - quadrature state transition decode
- Diagnostics:
  - `ROTARY_RAW a=<0/1> b=<0/1> ab=<0..3>`
  - `ROTARY_EDGE old=<0..3> new=<0..3> oldA=<0/1> oldB=<0/1> newA=<0/1> newB=<0/1>`
  - `ROTARY_DECODE old=<n> new=<n> delta=<+1/-1/0> accum=<n>`
  - `ROTARY_STEP direction=CW mapped=SW36 sent=<true/false>`
  - `ROTARY_STEP direction=CCW mapped=SW37 sent=<true/false>`

## HID Payload

- Rotary clockwise:
  - `SW36:P`
  - `SW36:R`
- Rotary counter-clockwise:
  - `SW37:P`
  - `SW37:R`
- HID report length:
  - fixed `63` bytes

## Current Status

- Matrix push-in is confirmed at `SW35`.
- Rotary motion on `GP0/GP1` is electrically validated.
- Immediate-step validation is confirmed in `rotary-host-receive-validation-rc5`.
- `ROTARY_STEP` and host `SW36` / `SW37` reception are confirmed.
- Latest follow-up RC: `rotary-host-receive-validation-rc5`, which adds explicit `ROTARY_STEP` / `ROTARY_STEP_RESULT` CDC logs and verifies host receive.

## Next Verification

- Build the firmware.
- Flash one device.
- Observe `ROTARY_RAW`, `ROTARY_EDGE`, and `ROTARY_DECODE` in CDC.
- Observe `SW36` / `SW37` in host HID capture.
