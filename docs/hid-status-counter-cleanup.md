# HID Status Counter Cleanup RC1

## Purpose

The HID runtime status counters were previously ambiguous. This note records the revised meanings so future captures can be interpreted consistently.

## Updated Counters

- `hidSendAttemptCount`
  - Number of times `send_hid_report()` was entered.
- `hidReadyTrueCount`
  - Number of times `tud_hid_ready()` returned true inside `send_hid_report()`.
- `hidReadyFalseCount`
  - Number of times `tud_hid_ready()` returned false inside `send_hid_report()`.
- `hidReportCallCount`
  - Number of times `tud_hid_report()` was actually called.
- `hidReportSuccessCount`
  - Number of successful `tud_hid_report()` calls.
- `hidReportFailCount`
  - Number of failed HID send attempts, including not-ready cases.
- `lastSendResult`
  - The most recent HID send outcome.

## Compatibility Field

- `sendCount`
  - Kept for compatibility in the short status line.
  - Treat it as a legacy alias for `hidSendAttemptCount`.

## Latest Hardware Observation

The latest CDC capture on `matrix-input-rc1` showed:

- `MATRIX_KEY` events were observed.
- `hidSendAttemptCount` increased from `0` to `18` during the key presses.
- `hidReadyTrueCount` increased in lockstep with the send attempts.
- `hidReadyFalseCount` remained `0` in the capture.
- `hidReportCallCount` increased with the send attempts.
- `lastSendResult=true` was observed during the capture.

## What To Watch In Captures

When reading CDC logs, focus on:

- whether `hidSendAttemptCount` increases when keys are pressed
- whether `hidReadyTrueCount` tracks actual ready windows
- whether `hidReportCallCount` stays in sync with send-ready windows
- whether `hidReportSuccessCount` remains at zero when the host is not receiving
