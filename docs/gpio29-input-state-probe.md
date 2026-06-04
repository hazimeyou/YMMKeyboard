# GPIO29 Input State Probe RC1

## Goal

Determine whether the rotary switch button wired to `GPIO29` is actually reaching firmware.

This phase checks only GPIO input state. HID transport is not the focus.

## Current Firmware Stamp

- `FW_VERSION=gpio29-state-probe-rc1`
- `FW_FEATURES=FW_INFO,HID_STATUS,GPIO_DIAG,GPIO_EDGE,GPIO_STABLE,BUTTON_GPIO29`

## Expected Logs

Periodic:

```text
GPIO_DIAG pin=29 raw=<0/1> activeLow=true pressed=<true/false> stable=<true/false>
```

On raw change:

```text
GPIO_EDGE pin=29 old=<0/1> new=<0/1> pressed=<true/false>
```

On debounce completion:

```text
GPIO_STABLE pin=29 pressed=<true/false>
```

## Capture Plan

1. Build and flash one board.
2. Start `CdcTraceCapture` against `COM12` for 30 seconds.
3. Press the `GPIO29` button two or three times during the window.
4. Inspect `tmp/gpio29-input-state-probe/cdc-trace.json`.

## What We Need to Learn

- Does `GPIO29` raw value change?
- Does raw change appear before debounce?
- Does debounce produce a stable pressed/released transition?

## Decision Rules

- If raw never changes, the issue is wiring / pull-up / wrong pin.
- If raw changes but stable never does, the debounce window is too strict or the press is too brief.
- If stable pressed appears but HID still does not move, the next step is the send path.

