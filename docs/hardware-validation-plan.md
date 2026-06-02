# Hardware Validation Plan

This document defines the hardware validation preparation phase only.

## Scope

The purpose of hardware validation is to confirm, on real hardware, what is observed, what is recorded, and what counts as success.

In scope:

- USB enumeration
- HID descriptor inspection
- device identity matching
- input event flow observation
- validation reporting

Out of scope:

- firmware changes
- HID transmit changes
- COM transmit changes
- YMM operation changes
- real hardware connection during preparation

## Validation Targets

- `RP2040 Zero`
- `RP2040 TinyUSB firmware`
- `YMMKeyboardPlugin`
- `DeviceInspector`
- `DiagnosticsComparer`
- `ProtocolSimulator`

## Required Equipment

- RP2040 Zero device
- USB cable
- host machine with the repository checked out
- build environment for `dotnet`
- access to the existing diagnostics tools and sample outputs

## Success Criteria

Hardware validation is successful when the following are all true:

- USB enumeration matches the expected VID, PID, ProductName, Manufacturer, and Serial values
- HID inspection matches the expected UsagePage, Usage, and report length values
- DeviceInspector recognizes the device
- YMMKeyboardPlugin recognizes the device
- DiagnosticsComparer reports an exact match
- input events are observed through the expected pipeline
- the validation record is complete enough to repeat or review the run later

## Failure Logging

When validation fails, record:

- validation date and time
- hardware identifier
- host machine name
- operating system version
- tool version or commit reference
- observed VID, PID, ProductName, Manufacturer, Serial
- observed HID UsagePage, Usage, and report length
- where the flow stopped
- exact error text
- relevant diagnostic artifacts

## Diagnostic Flow

1. Confirm the pre-hardware preparation checks are green.
2. Start DeviceInspector and capture enumeration details.
3. Confirm plugin-side identity matching.
4. Run DiagnosticsComparer for identity comparison.
5. Observe input flow events.
6. Record the result in the validation report template.
7. Classify the run as pass, fail, or blocked by environment.

## Operating Rule

Do not begin real hardware validation until the preparation checklist and pre-hardware CI both pass.
