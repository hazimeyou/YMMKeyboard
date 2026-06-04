# YMMKeyboard Current Status Report

## 1. Project Status

| Project | Status | Notes |
|---|---|---|
| Project Audit | Completed | Repository-wide audit and documentation baseline established. |
| Diagnostics Foundation RC2 | Completed | Diagnostics tooling and verification flow are stable. |
| Input Diagnostics RC1 | Completed | `InputReceived` is now reachable from plugin-side input flow. |
| Input Simulation RC1 | Completed | Replay and simulation plumbing is in place. |
| Macro & Dispatch Diagnostics RC1 | Completed | Macro / dispatch visibility exists. |
| Unified Diagnostics Replay | Completed | Cross-diagnostic replay is working. |
| Hardware Validation RC2 | Completed | Formal identity is applied and verified on the device. |

## 2. Current Successes

- `dotnet build YMMKeyboardPlugin.slnx -c Release` succeeds.
- Firmware formal identity is active and stable.
- `DeviceInspector` sees `VID=0x2E8A` / `PID=0x4020`.
- `HidConsoleProbe` can receive host HID traffic.
- Formal payload `K_<row>_<col>:P/R` is observable on the host when the HID report length is fixed to 63 bytes.
- `InputReceived` is now connected in the plugin path for HID events.

## 3. Current Blockers

- `InputReceived` verification still needs a live plugin run with the current matrix formal payload path.
- We still need to confirm the exact plugin-side behavior for the matrix payload in a running YMM4 session.
- Some historical docs still reference earlier matrix probes; they should be treated as background unless updated to the latest formal-payload run.

## 4. Cause Candidates

Current likely causes, in priority order:

1. Plugin runtime context not started with the latest build.
2. Plugin HID parsing path not matching the current formal payload shape.
3. YMM4 / plugin load timing affecting when `InputReceived` starts being recorded.
4. Residual stale diagnostics from earlier probe phases.

## 5. Latest Observed State

| Item | Value |
|---|---|
| VID | `0x2E8A` |
| PID | `0x4020` |
| ProductName | `YMM Control HID` |
| Manufacturer | `YMMKeyboard` |
| Serial | `504434042060791C` |
| Selected Candidate | `HID:2E8A:4020` |
| Host HID Result | `K_0_1:P` and `K_0_1:R` received |
| Host Classification | `K_COLON` |
| HID Report Length | `63` bytes payload, `64` bytes on wire with report ID |

## 6. Remaining Work by Area

### Firmware

- Keep the formal matrix payload path stable.
- Preserve the fixed 63-byte HID report length.
- Avoid reintroducing the earlier `reportLength=7` behavior.

### Plugin

- Confirm the live `InputReceived` path with the current firmware.
- Verify that the current HID parser accepts the formal matrix payload path in a running YMM4 session.

### Diagnostics

- Record the next live plugin run under `tmp/input-diagnostics/`.
- Verify `InputReceived` appears before mapping and dispatch steps.

### YMM Integration

- Confirm that YMM4 loads the latest plugin build and records the current HID traffic.

### Hardware Validation

- Use the latest known-good formal payload build as the baseline for remaining plugin verification.

## 7. Risks

- HID report length regressions can silently break host visibility.
- Plugin startup timing can make a good device look broken if diagnostics start late.
- Old probe logs can be mistaken for current state if the run folder is not updated.

## 8. Next Actions

### P1

- Run YMM4 with the latest plugin build and confirm `InputReceived` on a live matrix press.
- Save the resulting diagnostics under `tmp/input-diagnostics/`.

### P2

- Confirm whether `InputMapped` appears after `InputReceived`.
- If needed, compare the plugin-side raw HID parsing against the formal payload shape.

### P3

- Refresh the summary docs after the live plugin verification.

## 9. Estimated Completion

| Area | Estimate |
|---|---:|
| Diagnostics | 95% |
| Firmware Identity | 100% |
| Hardware Validation | 95% |
| Input Validation | 55% |
| Macro Validation | 10% |
| YMM Integration | 50% |
| Overall | 75% |

## 10. Conclusion

The hardware side is in good shape: formal identity is applied, the host can receive the formal `K_<row>_<col>:P/R` payload, and the 63-byte report length is confirmed as the working transport shape. The remaining gap is plugin-side live validation: we now need a YMM4 session with the latest build to confirm `InputReceived` in the real runtime path.
