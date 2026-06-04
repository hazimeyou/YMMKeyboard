# COM/HID Correlation Probe Result

## Current State

- latest retry completed
- `COM12` was selected successfully
- `2E8A:4020` HID was not available in the same run
- conclusion: `no_matching_hid_device`

## Latest Output

- `tmp/com-hid-correlation/correlation.json`
- `tmp/com-hid-correlation/correlation.md`
- `tmp/com-hid-correlation/correlation.log`

## Latest Observation

- `COM: HID_DIAG button=SW_00 sendResult=true` was already confirmed in separate CDC traces
- `HID: TEST_HID_* observed` was already confirmed in separate HID probes
- in this combined retry, the HID side did not match `2E8A:4020`, so `SW_*` correlation could not be completed

## Decision

- button HID path is still not fully correlated in one combined run
- next retry needs the RP2040 to be in a stable normal-boot state where both `COM12` and `2E8A:4020 HID` are visible at the same time
