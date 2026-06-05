# Rotary Step Immediate RC3

## Purpose

The rotary electrical probe confirmed that `GP0 / GP1` raw state and quadrature decode work, but the detent aggregation did not produce `ROTARY_STEP`.
This RC bypasses detent aggregation and emits `SW36` / `SW37` immediately for each non-zero decode delta.

## Diagnostic Mode

- Immediate step mode: enabled
- Detent threshold: ignored for this RC

## Firmware Diagnostics

The firmware emits:

- `ROTARY_RAW a=<0/1> b=<0/1> ab=<0..3>`
- `ROTARY_EDGE old=<0..3> new=<0..3> oldA=<0/1> oldB=<0/1> newA=<0/1> newB=<0/1>`
- `ROTARY_DECODE old=<n> new=<n> delta=<+1/-1/0> accum=<n> threshold=<n>`
- `ROTARY_STEP immediate=true delta=<+1/-1> direction=CW mapped=SW36 ...`
- `ROTARY_STEP immediate=true delta=<+1/-1> direction=CCW mapped=SW37 ...`
- `ROTARY_STEP_RESULT mapped=SW36 sent=<true/false> pressSent=<true/false> releaseSent=<true/false>`
- `ROTARY_STEP_RESULT mapped=SW37 sent=<true/false> pressSent=<true/false> releaseSent=<true/false>`

## HID Payload

- Clockwise:
  - `SW36:P`
  - `SW36:R`
- Counter-clockwise:
  - `SW37:P`
  - `SW37:R`
- HID report length:
  - fixed `63` bytes

## Expected Result

- `ROTARY_STEP` appears for each non-zero decode delta.
- `SW36` / `SW37` appear in host HID capture.
