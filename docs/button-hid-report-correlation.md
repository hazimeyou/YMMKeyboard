# Button HID Report Correlation RC1

## Objective

Confirm whether the real button input `Key1` reaches the host as an HID `SW_00` report.

## Current Evidence

- CDC trace shows `SW_00` observations.
- CDC trace shows `HID_DIAG:button=SW_00 ... sendResult=true`.
- The same run's `HidConsoleProbe` received HID traffic successfully.
- The same run's `HidConsoleProbe` classified only `TEST_HID_*` traffic in the latest shared run.
- The button-only capture path is now split out into `tmp/button-hid-trace/` so `SW_*` can be isolated cleanly.

## CDC Summary

- `SW_00` observed: yes
- `HID_DIAG sent=true`: yes
- `HID_DIAG sent=false`: no clear button-path failure was observed in the latest run

## HID Probe Summary

- `readSuccessCount`: `31`
- `readTimeoutCount`: `30`
- `TEST_HID_* count`: `31`
- `SW_* count`: `0`
- `OTHER count`: `0`

## Interpretation

The forced HID test path is confirmed end to end.

For the button path, the old standalone `GPIO29` probe is no longer the right model. The real layout is matrix-based, so the next capture must classify `K_*` matrix payloads rather than `SW_*` standalone payloads.

That leaves two practical possibilities for the matrix probe:

1. the matrix payload is still mixed in a way the current probe classification does not isolate
2. the matrix path is being sent but is not being observed in the same way as the forced test path

## Conclusion

- Forced `TEST_HID_*` path: confirmed
- Legacy `SW_00` path on CDC: observed in earlier periodic diagnostics, but that was not a real button probe
- Matrix `K_*` path on CDC/HID: not yet confirmed
- The current firmware has now been switched to a matrix scan probe based on the `code.py` layout
- Next capture should classify `K_*` payloads rather than `SW_*`

## Next Step

Run the matrix scan probe aligned to the `code.py` row/column layout so we can decide whether the missing payload is a wiring issue, matrix scan issue, debounce issue, or send-path issue.

### Latest Matrix Attempt

- `MATRIX_SCAN cols=7 rows=6`: observed
- `MATRIX_KEY`: not observed
- `MATRIX_HID`: not observed
- `HidConsoleProbe` `ReadSuccessCount`: `0`

The current evidence says the transport layer is ready, but the physical key press did not yet produce a detectable matrix transition in this capture window.
