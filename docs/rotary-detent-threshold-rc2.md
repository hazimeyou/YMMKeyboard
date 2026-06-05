# Rotary Detent Threshold RC2

## Purpose

The rotary electrical probe already confirmed that `GP0 / GP1` raw state changes and that quadrature decode can produce `delta=+1/-1`.
This RC lowers the detent aggregation threshold so `ROTARY_STEP` can be observed and the `SW36 / SW37` HID path can be validated.

## Change

- Previous detent threshold: `4`
- New detent threshold: `2`

## Firmware Diagnostics

The firmware now emits:

- `ROTARY_RAW a=<0/1> b=<0/1> ab=<0..3>`
- `ROTARY_EDGE old=<0..3> new=<0..3> oldA=<0/1> oldB=<0/1> newA=<0/1> newB=<0/1>`
- `ROTARY_DECODE old=<n> new=<n> delta=<+1/-1/0> accum=<n> threshold=2`
- `ROTARY_STEP direction=CW mapped=SW36 accumBefore=<n> threshold=2 pressSent=<true/false> releaseSent=<true/false> sent=<true/false>`
- `ROTARY_STEP direction=CCW mapped=SW37 accumBefore=<n> threshold=2 pressSent=<true/false> releaseSent=<true/false> sent=<true/false>`

## HID Payload

- Clockwise detent:
  - `SW36:P`
  - `SW36:R`
- Counter-clockwise detent:
  - `SW37:P`
  - `SW37:R`
- HID report length:
  - fixed `63` bytes

## Expected Result

- `ROTARY_STEP` appears in CDC.
- `SW36` / `SW37` appear in host HID capture.

## Current Status

- Raw rotary input and direction decode are already confirmed.
- This RC only tunes detent aggregation and keeps the same electrical path.

