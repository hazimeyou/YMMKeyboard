# Rotary Switch Direction

## Canonical Mapping

For the current YMMKeyboard baseline, the rotary control should be interpreted as:

- `SW35` = rotary push
- `SW36` = counter-clockwise / left rotation
- `SW37` = clockwise / right rotation

## Current State

This matches the live UI labels in the plugin:

- Left rotation -> `SW36`
- Push -> `SW35`
- Right rotation -> `SW37`

## Raw Switch ID Mapping

The KMK / firmware side still emits raw switch IDs in encoder order:

- raw `36` -> `SW37`
- raw `37` -> `SW36`

So the plugin-level switch names are intentionally swapped relative to the raw encoder direction, and that is expected.

## What Was Incorrect

The confusing part was the inline comment in `Keymacro.cs`, which described the two rotary directions backwards.

## Status

The mapping is now documented as:

- left / counter-clockwise -> `SW36`
- push -> `SW35`
- right / clockwise -> `SW37`

