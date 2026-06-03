# Hardware Validation Dry Run

This document defines the dry run for hardware validation preparation.

## Purpose

The dry run confirms that:

- the procedure is defined
- the report format is usable
- the output locations are consistent
- the preparation verification script succeeds

The dry run does not connect to hardware.

## Out of Scope

- DeviceInspector execution
- HID enumeration
- COM enumeration
- firmware changes
- plugin changes
- HID transmit changes
- COM transmit changes
- YMM operation changes

## Required Artifacts

- `docs/hardware-validation-plan.md`
- `docs/hardware-inventory.md`
- `docs/hardware-validation-checklist.md`
- `docs/hardware-validation-report-template.md`
- `samples/hardware-validation/sample-report.md`
- `scripts/verify-hardware-preparation.ps1`
- `scripts/verify-hardware-dry-run.ps1`

## Dry Run Steps

1. Confirm the required documents exist.
2. Confirm the sample report exists.
3. Confirm the report template exists.
4. Run the hardware preparation verification script.
5. Verify the dry run completes without hardware access.

## Success Criteria

The dry run is successful when:

- all required documents are present
- the sample report is present
- `scripts/verify-hardware-preparation.ps1` exits successfully
- no real hardware access is attempted
- the repository remains in a hardware validation state of `not started`

## Expected Output Placement

- preparation verification reports remain in `tmp/`
- the sample report remains under `samples/hardware-validation/`
- no device-specific capture artifacts are created during the dry run

## Command

```powershell
./scripts/verify-hardware-dry-run.ps1
```
