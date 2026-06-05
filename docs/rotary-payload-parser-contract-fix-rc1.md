# Rotary Payload Parser Contract Fix RC1

## Purpose

Bring plugin-side rotary parsing in line with the firmware payload contract.

## Contract

Firmware now emits raw rotary payloads:

- `SW36:P`
- `SW36:R`
- `SW37:P`
- `SW37:R`

The plugin now accepts both the legacy `...:SW_36` style and the raw firmware `SW36:P/R` style.

## Normalization

Internally, the plugin normalizes the rotary IDs so they continue to flow through the existing `SwitchLayout` and rotary filtering path.

This keeps the following path intact:

- `InputReceived`
- `RotaryAccumulated`
- `RotaryFiltered`
- `RotaryDispatched`
- `RotaryIgnoredRelease`
- `InputMapped`
- `DispatchPrepared`
- `DispatchExecuted`

## Notes

- Matrix formal payload handling remains unchanged.
- The goal of this RC is parser compatibility, not firmware redesign.
