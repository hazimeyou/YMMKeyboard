# Firmware HID Runtime Observation

## Scope

This note records the current runtime observation for the firmware HID send path and the newer lifecycle diagnostics.

## Current Evidence

The latest firmware now identifies itself as:

- `FW_ID=YMMKeyboard-RP2040-TinyUSB`
- `FW_VERSION=lifecycle-diagnostics-rc1`
- `FW_FEATURES=FW_INFO,HID_STATUS,HID_TEST,HID_DIAG,USB_EVENT,LIFECYCLE_DIAG`

The latest CDC trace shows:

- `FW_INFO` observed
- `USB_EVENT mount` observed
- `HID_STATUS` observed
- `HID_TEST` observed
- `HID_DIAG` observed
- `HB:` observed
- `SW_00` observed

## What We Can Confirm Right Now

- The firmware runtime is alive.
- CDC output is active.
- USB lifecycle logging is active.
- The forced HID test path is readable by the host.
- `SW_00` is visible in CDC and reports `sendResult=true`.
- The new lifecycle fields are visible in the HID runtime diagnostics.

## Current Classification

The currently available evidence now fits:

- `firmware forced test HID sending works`
- `host receives forced test HID reports`
- `button path is observed in CDC`
- `lifecycle state can be observed in CDC`

## What Remains Open

- Whether the button-triggered `SW_00` payload can be isolated independently from forced `TEST_HID_*` traffic in the host probe.
- Whether HID visibility timing on Windows is affected by lifecycle transitions even when CDC remains stable.

## Next Required Observation

Compare:

- forced `TEST_HID_*` payloads
- button-triggered `SW_00` payloads
- USB lifecycle events

That will let us see whether any remaining issue is in:

- button input generation
- debounce
- event formatting
- host-side isolation / classification
- lifecycle timing / readiness
