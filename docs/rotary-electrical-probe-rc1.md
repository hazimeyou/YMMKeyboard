# Rotary Electrical Probe RC1

## Purpose

Verify that the `GP0 / GP1` rotary encoder changes raw electrical state in firmware and that the quadrature decode sees the transitions.

## Current Assumptions

- `GP0` and `GP1` are rotary encoder A/B.
- Both pins use input pull-up.
- The encoder is independent from the matrix scan.

## Diagnostics

The firmware emits:

- `ROTARY_RAW a=<0/1> b=<0/1> ab=<0..3>`
- `ROTARY_EDGE old=<0..3> new=<0..3> oldA=<0/1> oldB=<0/1> newA=<0/1> newB=<0/1>`
- `ROTARY_DECODE old=<n> new=<n> delta=<+1/-1/0> accum=<n> threshold=<n>`
- `ROTARY_STEP direction=CW mapped=SW36 accumBefore=<n> threshold=<n> pressSent=<true/false> releaseSent=<true/false> sent=<true/false>`
- `ROTARY_STEP direction=CCW mapped=SW37 accumBefore=<n> threshold=<n> pressSent=<true/false> releaseSent=<true/false> sent=<true/false>`

## Expected Outcomes

- If `ROTARY_RAW` never changes, wiring or pull-up is wrong.
- If `ROTARY_EDGE` appears but `delta` stays `0`, the decode table or A/B order likely needs correction.
- If `delta` becomes `+1` or `-1`, the electrical path is working and we can tune detent aggregation next.
- If `ROTARY_STEP` appears, the detent threshold is low enough for the current EC11 behavior.

## Next Step

Capture a 60-second CDC trace while rotating slowly in both directions, then a few quick clicks in each direction.
