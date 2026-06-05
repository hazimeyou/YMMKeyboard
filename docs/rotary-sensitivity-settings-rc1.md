# Rotary Sensitivity Settings RC1

## Goal

Keep the firmware rotary path simple while letting the plugin absorb per-device detent differences through a user-adjustable sensitivity setting.

## Confirmed Input Path

- `SW35` = rotary push switch
- `SW36` / `SW37` = rotary motion source
- `SW36` / `SW37` host receive is already confirmed

## Plugin Behavior

The plugin now counts only rotary press events:

- `SW36:P` and `SW37:P` are counted
- `SW36:R` and `SW37:R` are ignored for dispatch
- direction changes reset the active rotary counter

The parser accepts both the legacy `...:SW_36` style and the raw firmware `SW36:P/R` style.

## Sensitivity Values

| Value | Label | Meaning |
|---|---|---|
| `1` | High | Dispatch on every accepted rotary press |
| `2` | Standard | Dispatch after 2 same-direction presses |
| `3` | Low | Dispatch after 3 same-direction presses |
| `4` | VeryLow | Dispatch after 4 same-direction presses |

Default:

- `2` (`Standard`)

## Diagnostics

New rotary diagnostics are recorded in the plugin input diagnostic stream:

- `RotaryAccumulated`
- `RotaryFiltered`
- `RotaryDispatched`
- `RotaryIgnoredRelease`

These are in addition to:

- `InputReceived`
- `InputMapped`
- `DispatchPrepared`
- `DispatchExecuted`

## Notes

- Firmware remains unchanged for this RC.
- The plugin handles filtering and dispatch cadence.
- This keeps the hardware baseline stable while allowing users to tune responsiveness.


