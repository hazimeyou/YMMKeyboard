# Diagnostics Comparison Report

## Summary
- inspectorSource: latest.json
- pluginSource: latest.json
- matchedHid: 3
- matchedCom: 2
- selectedCandidates: 1
- totalIssues: 0

## Issues
No issues.

## Matched HID
- HID 2E8A:4020 product=YMM RP2040 Control Keyboard maker=YMMKeyboard Project serial=YMMK-001 usage=FF00:0001 kind=formal
- HID 2E8A:101F product=YMM HID maker=YMMKeyboard serial=YMMK-LEGACY usage=FF00:0001 kind=temporary
- HID 046D:218E product=Logitech F310 Gamepad maker=Logitech serial=GAMEPAD-1 usage=0001:0005 kind=other

## Inspector Only HID
- none

## Plugin Only HID
- none

## Matched COM
- COM3
- COM7

## Inspector Only COM
- none

## Plugin Only COM
- none

## Matched Candidates
- HID key=HID:2E8A:4020:ymm rp2040 control keyboard:ymmkeyboard project:ymmk-001:FF00:0001 score=0 selected=False reasons=matched formal identity rejects=
- HID key=HID:2E8A:101F:ymm hid:ymmkeyboard:ymmk-legacy:FF00:0001 score=0 selected=False reasons=matched temporary identity rejects=

## Rejected Candidates
- HID vid=2E8A pid=101F product=YMM HID maker=YMMKeyboard serial=YMMK-LEGACY com= usage=FF00:0001 score=8164 selected=False reasons=connectionMode=HID|vid=2E8A|pid=101F|usagePage=FF00 usage=0001 rejects=mode=COM

## Selected Candidate
transport=HID vid=2E8A pid=4020 product=YMM RP2040 Control Keyboard maker=YMMKeyboard Project serial=YMMK-001 com= usage=FF00:0001 score=11164 selected=True reasons=connectionMode=HID|vid=2E8A|pid=4020|usagePage=FF00 usage=0001 rejects=

