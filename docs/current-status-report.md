# YMMKeyboard Current Status Report

## 1. Project Status

| Project | Status | Notes |
|---|---|---|
| Project Audit | Completed | Repository-wide audit and documentation baseline established. |
| Diagnostics Foundation RC2 | Completed | Diagnostics tooling and verification flow are stable. |
| Input Diagnostics RC1 | Completed | `InputReceived`, `InputMapped`, `DispatchPrepared`, and `DispatchExecuted` are confirmed in live plugin input flow. |
| Input Simulation RC1 | Completed | Replay and simulation plumbing is in place. |
| Macro & Dispatch Diagnostics RC1 | Completed | Macro / dispatch visibility exists. |
| Unified Diagnostics Replay | Completed | Cross-diagnostic replay is working. |
| Hardware Validation RC2 | Completed | Formal identity is applied and verified on the device. |
| Rotary Host Receive Validation RC5 | Completed | `GP0/GP1` raw state, edge, and decode are in place; immediate `SW36` / `SW37` step emission and host HID receive are confirmed. |

## 2. Current Successes

- `dotnet build YMMKeyboardPlugin.slnx -c Release` succeeds.
- Firmware formal identity is active and stable.
- `DeviceInspector` sees `VID=0x2E8A` / `PID=0x4020`.
- `HidConsoleProbe` can receive host HID traffic.
- Formal payload `K_<row>_<col>:P/R` is observable on the host when the HID report length is fixed to 63 bytes.
- `InputReceived`, `InputMapped`, `DispatchPrepared`, and `DispatchExecuted` are now connected in the plugin path for HID events.
- The live `K_0_1 -> A` path has been verified as an actual YMM UI action.
- Rotary push-in is confirmed on `SW35`; rotary motion is now fully confirmed on `GP0/GP1` with raw-state, decode, and immediate HID host-receive diagnostics.

## 3. Current Blockers

- Some historical docs still reference earlier matrix probes; they should be treated as background unless updated to the latest formal-payload run.
- Rotary direction labels were clarified so that left / counter-clockwise is `SW36`, push-in is `SW35`, and right / clockwise is `SW37`.
- Rotary step emission is confirmed in immediate mode; `ROTARY_STEP` and host `SW36` / `SW37` reception both worked in RC5.

## 4. Cause Candidates

Current likely causes, in priority order:

1. Residual stale diagnostics from earlier probe phases.
2. Follow-up mapping / dispatch behavior if a different key path is used later.
3. YMM4 / plugin load timing only matters for older runs, not the latest confirmed one.

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
| Plugin InputReceived | `1` |
| Plugin InputMapped | `1` |
| Plugin DispatchPrepared | `1` |
| Plugin DispatchExecuted | `1` |
| Actual YMM UI Action | `Confirmed` |

## 6. Remaining Work by Area

### Firmware

- Keep the formal matrix payload path stable.
- Preserve the fixed 63-byte HID report length.
- Avoid reintroducing the earlier `reportLength=7` behavior.
- Rotary `GP0/GP1` electrical and host receive validation is now confirmed in RC5.

### Plugin

- Dispatch execution is now confirmed for the `K_0_1 -> A` path.
- The working baseline is frozen on the confirmed live UI action path.
- Any remaining mapping or dispatch details only need follow-up if a different key path is exercised.

### Diagnostics

- Record the latest live plugin run under `tmp/input-diagnostics/`.
- Preserve `InputReceived`, `InputMapped`, and `DispatchPrepared` as the confirmed baseline.

### YMM Integration

- Confirmed for the current formal payload path and live runtime session.

### Hardware Validation

- Use the latest known-good formal payload build as the baseline.

## 7. Risks

- HID report length regressions can silently break host visibility.
- Plugin startup timing can make a good device look broken if diagnostics start late.
- Old probe logs can be mistaken for current state if the run folder is not updated.

## 8. Next Actions

### P1

- Keep the confirmed live `InputReceived` run as the baseline.
- Save any future changed-path diagnostics under `tmp/input-diagnostics/` if new mappings are exercised.
- Validate the current multiple-key mapping set on the live device (`K_0_1`, `K_0_4`, `K_0_6`, `K_1_6`, `K_4_1`) against the existing `A/B/C/D/E` assignments.

### P2

- Compare any future alternate payload path against the confirmed formal path only if required.

### P3

- Refresh the summary docs after the live plugin verification.

## 9. Estimated Completion

| Area | Estimate |
|---|---:|
| Diagnostics | 97% |
| Firmware Identity | 100% |
| Hardware Validation | 95% |
| Input Validation | 97% |
| Macro Validation | 10% |
| YMM Integration | 75% |
| Overall | 85% |

## 10. Conclusion

The hardware side is in good shape: formal identity is applied, the host can receive the formal `K_<row>_<col>:P/R` payload, and the 63-byte report length is confirmed as the working transport shape. The plugin runtime now confirms `InputReceived`, `InputMapped`, `DispatchPrepared`, and `DispatchExecuted` for the live matrix path, and the actual YMM UI action is confirmed for the `K_0_1 -> A` mapping. Rotary push-in is confirmed, and rotary rotation is now confirmed end-to-end from `GP0/GP1` through host `HidConsoleProbe`.
