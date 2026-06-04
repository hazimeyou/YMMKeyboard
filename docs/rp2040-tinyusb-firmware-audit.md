# RP2040 TinyUSB Firmware Full Audit

## Goal

Audit the RP2040TinyUsb firmware as a CDC/HID composite device and identify likely causes of unstable composite enumeration and missing HID visibility, before making larger code changes.

## Scope

- [main.c](/C:/Users/yu-za-hazimeyou/source/repos/YMMKeyboard/firmware/src/RP2040TinyUsb/src/main.c#L1)
- [usb_descriptors.c](/C:/Users/yu-za-hazimeyou/source/repos/YMMKeyboard/firmware/src/RP2040TinyUsb/src/usb_descriptors.c#L1)
- [usb_descriptors.h](/C:/Users/yu-za-hazimeyou/source/repos/YMMKeyboard/firmware/src/RP2040TinyUsb/src/usb_descriptors.h#L1)
- [tusb_config.h](/C:/Users/yu-za-hazimeyou/source/repos/YMMKeyboard/firmware/src/RP2040TinyUsb/src/tusb_config.h#L1)
- [CMakeLists.txt](/C:/Users/yu-za-hazimeyou/source/repos/YMMKeyboard/firmware/src/RP2040TinyUsb/CMakeLists.txt#L1)

## Executive Summary

The firmware is structurally close to a valid CDC + HID composite device:

- TinyUSB CDC and HID are both enabled.
- The descriptor defines 3 interfaces total.
- CDC and HID endpoints do not conflict.
- The HID report descriptor is present and matches the current forced-test send path.
- Forced `TEST_HID_*` reports are known to be readable on the host.
- CDC/COM is stable in repeated snapshots.

The strongest remaining issue is not a single obvious descriptor typo. The current behavior is more consistent with a runtime/Windows visibility problem around HID interface exposure, plus a very chatty firmware main loop that sends CDC output, forced HID test reports, and button reports continuously.

## Current Status Update

The latest lifecycle-diagnostics firmware run changed the observation picture:

- `FW_VERSION=lifecycle-diagnostics-rc1`
- `FW_INFO` is now observed in CDC
- `USB_EVENT mount` is observed in CDC
- `HID_STATUS` shows `mounted=true` in the new runtime trace
- the latest snapshot run shows `snapshotCount=17`, `comPresentCount=17`, `hidPresentCount=17`, `bothPresentCount=17`

So the HID-missing state is no longer the latest observed state; the composite device can be seen with both interfaces together.

## 1. Firmware Structure Review

### Main loop

`main()` does the following:

- `board_init()`
- `tusb_init()`
- `make_uid_hex()`
- then loops forever:
  - `tud_task()`
  - emit FW_INFO if CDC is connected
  - emit heartbeat every 3 seconds
  - emit button events every 5 seconds
  - emit HID status every 1 second
  - emit forced HID test every 1 second
  - `sleep_ms(1)`

### Observations

- `tud_task()` is called every loop, which is correct in principle.
- The loop is very busy on purpose: it produces CDC logs, HID diagnostics, and forced test traffic continuously.
- Button events are currently simulated by the `send_event('P', 0)` / `send_event('R', 0)` path because GPIO scan is still TODO.
- There is no queueing or throttling for HID traffic beyond `tud_hid_ready()`.

## 2. USB Lifecycle Audit

### Current state

There are no explicit callbacks in `main.c` for:

- `tud_mount_cb()`
- `tud_umount_cb()`
- `tud_suspend_cb()`
- `tud_resume_cb()`

### Implications

- The firmware does not explicitly reset runtime state on mount/unmount/reconnect.
- There is no lifecycle-based state machine to separate:
  - unmounted
  - mounted but not ready
  - ready
  - suspended
- The firmware depends mostly on live `tud_cdc_connected()` and `tud_hid_ready()` checks.

### Risk

- If Windows reconnects or rebinds interfaces, the firmware has no explicit lifecycle bookkeeping to help diagnose when the HID interface becomes available.
- Because `FW_INFO`, `HB:`, `HID_STATUS`, `HID_TEST`, and `HID_DIAG` are all emitted from the main loop, lifecycle transitions are inferred only indirectly.

## 3. HID Send Path Audit

### Send path

`send_hid_report()`:

- checks `tud_hid_ready()`
- copies payload into a fixed 63-byte local buffer
- calls `tud_hid_report(REPORT_ID_VENDOR, report, copy_len)`
- updates counters
- emits `HID_DIAG`

### Descriptor alignment

The current HID descriptor uses:

- Report ID: `1`
- Report size: `8`
- Report count: `63`
- payload length: `63`
- on-wire length: `64`

This matches the existing forced-test design.

### Runtime behavior

Known observations from separate host-side probes:

- forced `TEST_HID_*` reports are readable
- `tud_hid_report()` is not universally failing
- host receive is possible

### Risk points

- `send_hid_report()` only attempts to send when `tud_hid_ready()` is already true.
- When `hid_ready` is false, the code increments fail counters and does not queue a retry.
- Forced test traffic and button traffic share the same send path and the same one-second / five-second schedule.
- `send_event()` emits both CDC and HID output in the same tick, which can make timing analysis noisy.

### Specific concern

- `g_hid_ready_true_count` is incremented on successful send, not on every `tud_hid_ready()==true` observation. That is fine as a success counter, but it is not a pure readiness counter.

## 4. CDC Interaction Audit

### Current CDC usage

- `write_cdc_line()` checks `tud_cdc_connected()`
- writes with `tud_cdc_write_str()`
- flushes every line with `tud_cdc_write_flush()`

### Output pattern

CDC emits:

- `FW_INFO`
- `HB:`
- `HID_STATUS`
- `HID_TEST`
- `HID_DIAG`
- raw button lines like `uid:P:SW_00`

### Risk

- Frequent per-line flushing can increase traffic and make timing noisy.
- CDC output is synchronous and very chatty, which may obscure USB lifecycle timing when diagnosing HID visibility.
- There is no backoff or batching.

### Positive finding

- CDC is clearly alive and stable enough to carry diagnostics.
- In the latest snapshot measurements after lifecycle diagnostics were added, COM and HID were both visible together.

## 5. Composite Descriptor Audit

### Current configuration

From [tusb_config.h](/C:/Users/yu-za-hazimeyou/source/repos/YMMKeyboard/firmware/src/RP2040TinyUsb/src/tusb_config.h#L1):

- `CFG_TUD_CDC = 1`
- `CFG_TUD_HID = 1`
- `CFG_TUD_ENDPOINT0_SIZE = 64`
- `CFG_TUD_CDC_EP_BUFSIZE = 64`
- `CFG_TUD_HID_EP_BUFSIZE = 64`

From [usb_descriptors.h](/C:/Users/yu-za-hazimeyou/source/repos/YMMKeyboard/firmware/src/RP2040TinyUsb/src/usb_descriptors.h#L1):

- `ITF_NUM_CDC = 0`
- `ITF_NUM_CDC_DATA = 1`
- `ITF_NUM_VENDOR_HID = 2`
- `ITF_NUM_TOTAL = 3`

From [usb_descriptors.c](/C:/Users/yu-za-hazimeyou/source/repos/YMMKeyboard/firmware/src/RP2040TinyUsb/src/usb_descriptors.c#L1):

- `CONFIG_TOTAL_LEN = TUD_CONFIG_DESC_LEN + TUD_CDC_DESC_LEN + TUD_HID_DESC_LEN`
- `TUD_CONFIG_DESCRIPTOR(1, ITF_NUM_TOTAL, 0, CONFIG_TOTAL_LEN, 0x00, 100)`
- `TUD_CDC_DESCRIPTOR(ITF_NUM_CDC, 0x05, EPNUM_CDC_NOTIF, 8, EPNUM_CDC_OUT, EPNUM_CDC_IN, 64)`
- `TUD_HID_DESCRIPTOR(ITF_NUM_VENDOR_HID, 0x06, HID_ITF_PROTOCOL_NONE, hid_report_descriptor_len, EPNUM_HID_IN, 64, 1)`

### Endpoint map

- CDC notification IN: `0x81`
- CDC OUT: `0x02`
- CDC IN: `0x82`
- HID IN: `0x83`

### Result

- CDC and HID are both declared in the descriptor.
- Interface numbering is self-consistent.
- Endpoint addresses do not conflict.
- `CONFIG_TOTAL_LEN` is derived from the CDC + HID descriptor lengths.
- No obvious descriptor-length mismatch was found in the audit.

## 6. Windows Stability Comparison

### Observed stable behavior

- CDC/COM appears stable.
- Snapshot measurements repeatedly show COM ports present.
- `COM12` and `COM10` appear as `VID_2E8A&PID_4020` CDC interfaces.

### Observed unstable behavior

- HID `2E8A:4020` is not consistently visible.
- In a 30-second snapshot run, `hidPresentCount = 0` while `comPresentCount = 15`.
- In separate runs, HID `2E8A:4020` is visible and host-readable.

### Updated observation

- The lifecycle-diagnostics run now shows `hidPresentCount = 17` and `bothPresentCount = 17`.
- That indicates the HID-missing state was transient, not a permanent descriptor failure.

### Interpretation

This does not look like a simple “HID descriptor missing” problem.

The stronger explanation is one of these:

- HID visibility is timing-sensitive at boot or reconnect.
- Windows binding / enumeration state for the HID interface is inconsistent across runs.
- The combined probe is hitting a window where CDC is up but HID is not yet surfaced.

## 7. Problem Candidates

Ordered from most likely to least likely based on current evidence:

1. HID visibility timing / Windows binding instability
   - CDC remains stable while HID is absent in repeated snapshots.
   - Separate runs prove HID can exist, so this is likely a state/timing issue rather than a permanent descriptor absence.
   - Latest lifecycle snapshots show both interfaces can be present together, which suggests the state is recoverable.

2. Runtime send policy is too eager and too synchronous
   - The firmware sends forced tests, status, and button diagnostics continuously.
   - That makes it harder to isolate readiness transitions.

3. No explicit USB lifecycle state machine
   - Without mount/unmount handling, diagnosis is indirect.
   - The firmware does not track readiness transitions explicitly.

4. CDC chatter obscures HID timing
   - Frequent flushes and dense logging may make reproduction and capture more difficult.

5. Descriptor risk remains low but not zero
   - Although no obvious mismatch was found, a Windows composite binding edge case can still exist even when the raw descriptor looks correct.

## 8. Recommended Minimal Fix

The safest next step is not a descriptor redesign.

### Recommended Option: Add lifecycle diagnostics and reduce HID noise first

Minimal changes:

- add `tud_mount_cb()` / `tud_umount_cb()` diagnostics
- track mount/ready state explicitly
- keep forced test traffic, but reduce it or gate it when investigating button path
- keep CDC output, but make it easier to correlate with ready state

### Why this first

- It preserves the current working forced test path.
- It does not risk breaking the known-good `TEST_HID_*` host receive path.
- It gives direct evidence about when HID becomes ready or whether it never becomes ready.

### Alternative options, ranked

1. **Lifecycle diagnostics first**
   - Lowest risk
   - Best for diagnosis
   - Recommended

2. **Temporarily throttle forced `TEST_HID_*` traffic**
   - Useful if button `SW_00` needs isolation
   - Slightly higher risk because it changes the current known-good confirmation path

3. **HID-only firmware variant**
   - Best for isolating composite issues
   - Higher operational cost
   - Good fallback if Windows HID visibility remains unstable

4. **Descriptor simplification**
   - Highest risk because the current descriptor is already internally consistent
   - Not recommended as the first change

## 9. Next Validation Plan

### Step 1

Add lifecycle logs:

- mount
- unmount
- suspend
- resume
- ready state transitions

### Step 2

Add a simple runtime flag or counters for:

- `mounted`
- `hid_ready`
- `cdc_connected`

### Step 3

Retest with:

- `CdcTraceCapture`
- `HidConsoleProbe`
- `ComHidCorrelationProbe` snapshot mode

### Step 4

If HID remains absent in the combined snapshot, test a temporary HID-only build.

### Success conditions

- HID becomes visible in the same window as CDC
- `COM/HID` combined probe can select both sides in the same run
- `HID_DIAG sendResult=true` correlates with `SW_*` HID host reads

## 10. Conclusion

- The firmware is not obviously broken at the composite descriptor level.
- CDC and HID are both defined.
- Interface numbers and endpoint addresses are consistent.
- The most likely problem is HID visibility timing or Windows binding state, not a missing descriptor entry.
- The best next step is to add lifecycle diagnostics and, if needed, isolate HID behavior with a minimal HID-only variant before attempting any descriptor redesign.
