# Rotary Host Receive Validation RC5

## Purpose

The immediate rotary path already produces `ROTARY_STEP` in CDC and sends `SW36` / `SW37` HID payloads.
This RC verifies that host-side `HidConsoleProbe` receives those payloads.

## CDC Diagnostics

- `ROTARY_STEP immediate=true delta=<+1/-1> direction=CW mapped=SW36 accumBefore=<n> threshold=<n>`
- `ROTARY_STEP immediate=true delta=<+1/-1> direction=CCW mapped=SW37 accumBefore=<n> threshold=<n>`
- `ROTARY_STEP_RESULT mapped=SW36 payload=SW36:P sent=<true/false>`
- `ROTARY_STEP_RESULT mapped=SW36 payload=SW36:R sent=<true/false>`
- `ROTARY_STEP_RESULT mapped=SW37 payload=SW37:P sent=<true/false>`
- `ROTARY_STEP_RESULT mapped=SW37 payload=SW37:R sent=<true/false>`

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
- `ROTARY_STEP_RESULT` shows `sent=true`.
- `HidConsoleProbe` reports `SW36` / `SW37` payloads as received.

## Observed Result

- `ROTARY_STEP` appeared in CDC for both clockwise and counter-clockwise motion.
- `ROTARY_STEP_RESULT` was emitted alongside the send path.
- `HidConsoleProbe` received `SW36:P` and `SW37:P` payloads and classified them correctly.
- Host receive for `SW36` / `SW37` is confirmed.

This RC is now the host-receive baseline for the raw SW36:P/R and SW37:P/R rotary payload contract.
