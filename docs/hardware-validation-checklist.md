# Hardware Validation Checklist

Use this checklist during the first real hardware validation run.

## Run Metadata

- Date:
- Operator:
- Host machine:
- OS version:
- Repository state:
- Tool commit or version:

## USB Enumeration

- [ ] VID observed
- [ ] PID observed
- [ ] ProductName observed
- [ ] Manufacturer observed
- [ ] Serial observed

Record:

- VID:
- PID:
- ProductName:
- Manufacturer:
- Serial:

## HID

- [ ] UsagePage observed
- [ ] Usage observed
- [ ] Report Length observed

Record:

- UsagePage:
- Usage:
- Report Length:

## Device Identity

- [ ] DeviceInspector recognizes the device
- [ ] Plugin recognizes the device
- [ ] DiagnosticsComparer matches both views

Record:

- DeviceInspector result:
- Plugin result:
- Comparer result:

## Input

- [ ] InputReceived observed
- [ ] InputMapped observed
- [ ] MacroResolved observed
- [ ] DispatchPrepared observed

Record:

- InputReceived:
- InputMapped:
- MacroResolved:
- DispatchPrepared:

## Run Outcome

- [ ] Pass
- [ ] Fail
- [ ] Blocked by environment

Notes:

- Any mismatch between expected and observed values
- Any missing logs or artifacts
- Any follow-up action required
