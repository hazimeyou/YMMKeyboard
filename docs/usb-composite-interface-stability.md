# USB Composite Interface Stability

## Purpose

Confirm whether RP2040 normal boot exposes both CDC/COM and HID interfaces at the same time in a stable way.

## Tool

- `tools/YMMKeyboard.ComHidCorrelationProbe --snapshot-only`

## Latest Measurement

- Duration: `30s`
- Snapshot interval: `1s`
- `snapshotCount`: `17`
- `comPresentCount`: `17`
- `hidPresentCount`: `17`
- `bothPresentCount`: `17`
- `comMissingCount`: `0`
- `hidMissingCount`: `0`

## Observations

- COM ports were consistently present during every snapshot.
- HID `2E8A:4020` was present during every snapshot in this run.
- The simultaneous presence rate for COM and HID was `100%` for this measurement.
- The firmware version in the runtime capture was `lifecycle-diagnostics-rc1`.

## Interpretation

- The USB composite device is now observable as both CDC and HID at the same time in this measurement.
- The earlier HID-missing state was not permanent.
- The combined correlation probe should now have a stable window to capture both COM and HID together.

## Next Action

- Re-run `ComHidCorrelationProbe` for the button correlation step while both interfaces are visible.
- Compare `HID_DIAG sendResult=true` with the host HID `SW_*` reports.
