# Rotary Direction & Detent Finalization RC1

## Purpose

The rotary encoder electrical path is confirmed, and immediate host receive validation has already succeeded.
This RC switches back to detent aggregation so the rotary can be treated as a practical input source rather than a per-transition diagnostic stream.

## Current Known Direction

- `delta=+1` -> `CW` -> `SW36`
- `delta=-1` -> `CCW` -> `SW37`

## Detent Mode

- Detent threshold: `2`
- Immediate mode: disabled
- Accumulator resets after a step is emitted

## Firmware Diagnostics

- `ROTARY_DECODE old=<n> new=<n> delta=<+1/-1/0> accum=<n> threshold=<n>`
- `ROTARY_STEP immediate=false delta=<+1/-1> direction=<CW/CCW> mapped=<SW36/SW37> accumBefore=<n> threshold=<n>`
- `ROTARY_STEP_RESULT mapped=<SW36/SW37> payload=<...> sent=<true/false>`

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

- One click should produce roughly one `SW36` or `SW37` detent step.
- If multiple outputs appear per click, the threshold should be increased.
- If no output appears, the threshold should be lowered or the electrical decode should be revisited.

