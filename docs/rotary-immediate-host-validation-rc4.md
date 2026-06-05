# Rotary Immediate Host Validation RC4

## Purpose

The rotary encoder electrical path is already confirmed. This RC makes `ROTARY_STEP` explicit in CDC before HID send and emits a post-send result line so host visibility can be checked independently.

## Firmware Diagnostics

- `ROTARY_RAW a=<0/1> b=<0/1> ab=<0..3>`
- `ROTARY_EDGE old=<0..3> new=<0..3> oldA=<0/1> oldB=<0/1> newA=<0/1> newB=<0/1>`
- `ROTARY_DECODE old=<n> new=<n> delta=<+1/-1/0> accum=<n> threshold=<n>`
- `ROTARY_STEP immediate=true delta=<+1/-1> direction=CW mapped=SW36 accumBefore=<n> threshold=<n>`
- `ROTARY_STEP immediate=true delta=<+1/-1> direction=CCW mapped=SW37 accumBefore=<n> threshold=<n>`
- `ROTARY_STEP_RESULT mapped=SW36 sent=<true/false> pressSent=<true/false> releaseSent=<true/false>`
- `ROTARY_STEP_RESULT mapped=SW37 sent=<true/false> pressSent=<true/false> releaseSent=<true/false>`

## HID Payload

- Clockwise:
  - `SW36:P`
  - `SW36:R`
- Counter-clockwise:
  - `SW37:P`
  - `SW37:R`
- Report length:
  - fixed `63` bytes

## Expected Result

- `ROTARY_STEP` appears in CDC.
- `ROTARY_STEP_RESULT` reports success.
- `SW36` / `SW37` appear in `HidConsoleProbe`.

