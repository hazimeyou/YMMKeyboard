# Firmware HID Report Send Debug RC1

## Summary

The latest correlation run shows that the forced HID test report is readable by the host.

YMM4 and the plugin are not part of this phase.

## Current Observation

- `2E8A:4020` is enumerable
- standalone probe reported `openSucceeded=True`
- `readLoopStarted=True`
- `readAttemptCount=61`
- `readSuccessCount=30`
- `readTimeoutCount=31`
- first received payload: `TEST_HID_0733`

## Runtime Marker Check

The latest CDC trace capture shows runtime activity including:

- `HID_STATUS`
- `HID_DIAG`
- `HB:`
- `SW_00`

The version-stamp capture already confirmed the current diagnostic firmware is `hid-send-debug-rc1`.

## Correlation Result

- firmware send path: working for the forced test report
- host receive path: working
- the remaining comparison is now the button-triggered `SW_00` path versus the forced `TEST_HID_*` path

## Targets

- `firmware/src/RP2040TinyUsb/src/main.c`
- `firmware/src/RP2040TinyUsb/src/usb_descriptors.c`

## Phase 1: Static Audit

Confirmed:

- report ID = `1`
- payload length = `63`
- on-wire report length = `64`
- endpoint size = `64`
- descriptor input payload length = `63`

## Phase 2: Forced HID Test

Confirmed in practice:

- the forced HID test report is readable by the host
- the first readable payload was `TEST_HID_0733`

## Phase 3: CDC Diagnostics

The runtime CDC output remains the source of truth for:

- `HID_STATUS`
- `HID_DIAG`
- `HB:`
- `SW_00`

## Next Step

Compare the forced test payloads with the button-triggered `SW_00` payloads and decide whether any remaining difference is in the input event path or not.

## Related Artifacts

- `docs/hid-send-path-investigation.md`
- `docs/hid-console-probe-result.md`
- `docs/firmware-version-stamp.md`
- `docs/hid-send-host-receive-correlation.md`