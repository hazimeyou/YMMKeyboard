# Firmware HID Runtime Observation

## Scope

This note records the current runtime observation for the firmware HID send path and the newer lifecycle diagnostics.

## Current Evidence

The latest firmware now identifies itself as:

- `FW_ID=YMMKeyboard-RP2040-TinyUSB`
- `FW_VERSION=matrix-input-rc1`
- `FW_FEATURES=FW_INFO,HID_STATUS,MATRIX_KEY,MATRIX_HID,REVERSE_SCAN`

The latest CDC trace shows:

- `FW_INFO` observed
- `USB_EVENT mount` observed
- `HID_STATUS` observed
- `MATRIX_KEY` observed
- `MATRIX_HID` observed
- `HB:` observed
- `hidSendAttemptCount` increased during key presses
- `hidReadyTrueCount` tracked the send attempts
- `hidReadyFalseCount=0` in the latest capture
- `hidReportCallCount` increased during key presses
- `lastSendResult=true` observed

## What We Can Confirm Right Now

- The firmware runtime is alive.
- CDC output is active.
- USB lifecycle logging is active.
- The matrix input path is producing key events.
- The HID send path is being attempted during key presses.
- The new lifecycle fields are visible in the HID runtime diagnostics.

## Current Classification

The currently available evidence now fits:

- `matrix input path is active`
- `HID send attempts are counted correctly`
- `CDC diagnostics now reflect the actual send path`

## What Remains Open

- Whether the host-side HID probe should be rerun to confirm the corresponding payloads for this exact firmware image.
- Whether any remaining issue is in host-side isolation / classification rather than firmware send behavior.

## Next Required Observation

Compare:

- matrix `MATRIX_KEY` payloads
- HID runtime counters
- USB lifecycle events

That will let us see whether any remaining issue is in:

- button input generation
- debounce
- event formatting
- host-side isolation / classification
- lifecycle timing / readiness
- counter interpretation
