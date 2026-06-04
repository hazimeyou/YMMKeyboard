# RP2040 TinyUSB Lifecycle Diagnostics

## Purpose

Record the minimal USB lifecycle diagnostics added to the RP2040 TinyUSB firmware so that CDC/HID composite state transitions can be observed without larger firmware changes.

## Firmware Version

- `FW_VERSION=lifecycle-diagnostics-rc1`
- `FW_FEATURES=FW_INFO,HID_STATUS,HID_TEST,HID_DIAG,USB_EVENT,LIFECYCLE_DIAG`

## Added Diagnostics

### USB lifecycle callbacks

- `tud_mount_cb()`
- `tud_umount_cb()`
- `tud_suspend_cb(bool remote_wakeup_en)`
- `tud_resume_cb()`

### Tracked state

- `usbMounted`
- `usbSuspended`
- `mountCount`
- `unmountCount`
- `suspendCount`
- `resumeCount`

### CDC output

- `USB_EVENT mount`
- `USB_EVENT unmount`
- `USB_EVENT suspend remoteWakeup=true|false`
- `USB_EVENT resume`

### HID diagnostics

- `HID_STATUS` now includes:
  - `mounted`
  - `suspended`
  - `hidReady`
  - `mountCount`
  - `unmountCount`
  - `suspendCount`
  - `resumeCount`
  - `sendCount`
  - `sendFailCount`

- `HID_TEST` now includes:
  - `mounted`
  - `suspended`
  - `ready`

- `HID_DIAG` now includes:
  - `mounted`
  - `suspended`
  - `hidReady`

## Why This Helps

- It shows whether the device has mounted before HID sending starts.
- It distinguishes a simple `hidReady=false` condition from a real lifecycle transition.
- It makes HID visibility timing on Windows easier to correlate with host observations.

## Latest Runtime Observation

- `USB_EVENT mount` was observed in CDC.
- `HID_STATUS` showed `mounted=true`.
- The latest snapshot run showed both CDC and HID present together:
  - `snapshotCount=17`
  - `comPresentCount=17`
  - `hidPresentCount=17`
  - `bothPresentCount=17`

## Next Use

Use this firmware image together with:

- [CDC Trace Capture](/C:/Users/yu-za-hazimeyou/source/repos/YMMKeyboard/docs/cdc-trace-capture.md#L1)
- [USB Composite Interface Stability](/C:/Users/yu-za-hazimeyou/source/repos/YMMKeyboard/docs/usb-composite-interface-stability.md#L1)
- [RP2040 TinyUSB Firmware Full Audit](/C:/Users/yu-za-hazimeyou/source/repos/YMMKeyboard/docs/rp2040-tinyusb-firmware-audit.md#L1)

to correlate lifecycle state with HID visibility and button report delivery.
