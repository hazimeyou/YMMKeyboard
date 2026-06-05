# smoke-test-cleanup-rc1

## Metadata

- Date: 2026-06-06
- Branch: `codex/usb-hid-migration`
- Commit: `b7a869c` (`chore: finalize cleanup rc1`)
- Tags: `cleanup-rc1`, `working-baseline-rc1`

## Build Results

- `dotnet build YMMKeyboardPlugin.slnx`: success
- `dotnet build` warnings: 0
- `dotnet build` errors: 0
- `cmake --build firmware/src/RP2040TinyUsb/build`: success

## Device Visibility

- `COM12` is present on the host.
- `YMM Control HID` is present in HID enumeration.
- `DeviceInspector` serial probe saw `COM12` streaming current firmware diagnostics.

## Passive Serial Probe Observations

- `REV_SCAN_FRAME ROW=... COLS=1111111` observed repeatedly on `COM12`
- `MATRIX_SCAN cols=7 rows=6` observed on `COM12`
- `ROTARY_RAW a=1 b=1 ab=3` observed on `COM12`

## Smoke Test Status

- Matrix press/release: not manually exercised in this session
- Rotary CW / CCW: not manually exercised in this session
- HID receive confirmation: passive visibility confirmed via HID enumeration
- Plugin receive confirmation: not exercised with a new physical input event
- YMM actual UI action: not exercised in this session
- RotarySensitivity 1-4: not re-tuned in this session

## COM / CDC Fallback

- Legacy serial path remains visible on `COM12`
- Passive probe confirms the serial fallback is still alive
- No attempt was made to re-center COM/CDC as the primary path

## Known Issues / Limits

- No physical key press/release actuation was available in this session, so the full Matrix / Rotary / Plugin / YMM end-to-end smoke test remains incomplete.
- The verification here is limited to build success, device visibility, and passive serial output.
