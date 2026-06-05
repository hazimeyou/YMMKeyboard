# Rotary Detent Quality RC2

## Purpose

The rotary electrical path is confirmed, and the immediate mode baseline already proved that `SW36` / `SW37` can reach the host.
This RC measures how noisy a single physical click is, so we can tune detent aggregation toward one click ≈ one event.

## Current Known Direction

- `delta=+1` -> `CW` -> `SW36`
- `delta=-1` -> `CCW` -> `SW37`

## Detent Quality Diagnostics

The firmware emits:

- `ROTARY_DECODE old=<n> new=<n> delta=<+1/-1/0> accum=<n> threshold=<n>`
- `ROTARY_CLICK_ANALYSIS direction=<CW/CCW> edgeCount=<n> deltaCount=<n> threshold=<n>`
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

## Threshold

- Current threshold: `2`
- The firmware is prepared to compare against `3` and `4` if the click analysis shows the current value is still too chatty.

## Expected Result

- For each physical click, the click analysis should show a small, stable number of edges and decode deltas.
- If the event count per click is too high, raise the threshold.
- If clicks are missed, lower the threshold.

