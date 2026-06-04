# USB Composite Descriptor Audit

## Purpose

Audit the RP2040 TinyUSB composite descriptor to determine whether CDC and HID are defined together correctly and whether the current Windows HID missing behavior is likely caused by descriptor wiring or by runtime/OS state.

## Scope

- `firmware/src/RP2040TinyUsb/src/usb_descriptors.c`
- `firmware/src/RP2040TinyUsb/src/usb_descriptors.h`
- `firmware/src/RP2040TinyUsb/src/tusb_config.h`
- `firmware/src/RP2040TinyUsb/src/main.c`
- `firmware/src/RP2040TinyUsb/CMakeLists.txt`

## TinyUSB Config Audit

### Current configuration

- `CFG_TUD_CDC = 1`
- `CFG_TUD_HID = 1`
- `CFG_TUD_ENDPOINT0_SIZE = 64`
- `CFG_TUD_CDC_EP_BUFSIZE = 64`
- `CFG_TUD_HID_EP_BUFSIZE = 64`

### Result

- CDC is enabled.
- HID is enabled.
- Endpoint 0 size is 64 bytes.
- HID endpoint buffer size is 64 bytes.
- The firmware is configured for a CDC + HID composite device.

## Interface Numbering Audit

### Defined interface numbers

- `ITF_NUM_CDC = 0`
- `ITF_NUM_CDC_DATA = 1`
- `ITF_NUM_VENDOR_HID = 2`
- `ITF_NUM_TOTAL = 3`

### Result

- Total interface count is 3.
- The descriptor and interface enumeration are internally consistent.
- CDC occupies interfaces 0 and 1.
- HID occupies interface 2.

## Descriptor Length Audit

### Configuration descriptor

- `CONFIG_TOTAL_LEN = TUD_CONFIG_DESC_LEN + TUD_CDC_DESC_LEN + TUD_HID_DESC_LEN`
- `TUD_CONFIG_DESCRIPTOR(..., ITF_NUM_TOTAL, ..., CONFIG_TOTAL_LEN, ..., 100)`
- `TUD_CDC_DESCRIPTOR(ITF_NUM_CDC, ..., EPNUM_CDC_NOTIF, 8, EPNUM_CDC_OUT, EPNUM_CDC_IN, 64)`
- `TUD_HID_DESCRIPTOR(ITF_NUM_VENDOR_HID, ..., hid_report_descriptor_len, EPNUM_HID_IN, 64, 1)`

### Result

- The configuration descriptor length is built from the exact CDC and HID descriptor lengths.
- No obvious length mismatch was found in the descriptor construction.
- The descriptor includes both CDC and HID entries in the same configuration.

## Endpoint Map

### CDC endpoints

- Notification IN: `0x81`
- Data OUT: `0x02`
- Data IN: `0x82`

### HID endpoints

- HID IN: `0x83`
- HID OUT constant exists in headers as `0x03`, but the current HID descriptor macro uses the IN endpoint only.

### Result

- There is no endpoint address conflict between CDC and HID.
- CDC and HID use separate IN endpoints.
- The HID path is configured as an IN-capable interface for host-bound reports.

## HID Report Descriptor Audit

### Current descriptor

- Usage Page: vendor-defined `0xFF00`
- Usage: `0x01`
- Report ID: `1`
- Report Size: `8`
- Report Count: `63`
- Input payload length: `63` bytes
- Output payload length: `63` bytes

### Result

- The HID report descriptor is present and matches the runtime send path design.
- Report ID and payload sizing are aligned with the current forced test reports.

## Windows Observation Comparison

### Observed stable state

- COM/CDC is stable.
- `COM12` is consistently present.
- `COM10` is also present in the same device family.

### Observed missing state

- HID `2E8A:4020` is not present in the 30-second snapshot window taken during the stability investigation.
- In separate runs, HID `2E8A:4020` can be observed.

### Interpretation

- The descriptor is not obviously missing HID.
- The problem is more consistent with interface visibility / Windows binding / timing state than with a missing HID descriptor entry.

## Current Risk

- Windows may be exposing CDC and HID with different stability characteristics.
- HID may be present only after a boot/replug timing window, even when CDC is visible continuously.
- Windows may cache or transiently suppress the HID interface state after earlier boot states.
- If HID visibility is intermittent, a combined COM+HID correlation probe will fail even though both interfaces exist in the firmware descriptor.

## Recommended Minimal Fix

1. Keep the descriptor layout as-is for now.
2. Continue investigating the HID visibility state with boot timing / replug timing.
3. If HID remains absent in normal boot after repeated snapshots, inspect Windows binding and interface enumeration rather than changing the composite layout immediately.
4. If a firmware-side change is needed later, prefer a minimal diagnostic-only change first, not a descriptor redesign.

## Conclusion

- CDC and HID are both defined in the firmware descriptor.
- Interface numbering is consistent: `ITF_NUM_TOTAL = 3`.
- Endpoint assignment does not show a conflict.
- The current missing HID behavior in Windows is not explained by an obvious descriptor gap.
- The stronger candidate causes are timing, binding, or OS observation state rather than the composite descriptor structure itself.
