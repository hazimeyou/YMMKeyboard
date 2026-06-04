# Matrix Scan Direction Decision RC1

## Decision

Formal matrix scan direction is:

- `ROW=output`
- `COL=input pull-up`

## Why

- Forward validation showed `ROWS=000000` fixed across all driven columns.
- Reverse validation showed idle `COLS=1111111` and real `REV_COL_EDGE` / `REV_MATRIX_CANDIDATE` activity under key presses.
- That means the reverse-direction electrical model is the one that matches observable hardware behavior.

## Not Adopted

- `COL=output`
- `ROW=input pull-up`

This direction was used for the earlier forward probe, but it did not produce meaningful idle variation or edge detection in the current validation phase.

## Transition To Next Phase

The next firmware phase should treat the reverse-direction model as the canonical matrix input path:

- detect row-driven scan activity as `MATRIX_KEY`
- preserve the `K_r_c` report payload first
- keep key-label translation out of scope until the electrical mapping is fully stable

## Validation Evidence

- Forward probe: `COL=output / ROW=input pull-up` produced `ROWS=000000`
- Reverse probe: `ROW=output / COL=input pull-up` produced idle `COLS=1111111`
- Reverse probe also produced `REV_COL_EDGE` and `REV_MATRIX_CANDIDATE`

## Conclusion

Reverse-direction scanning is the correct electrical model to carry forward into `matrix-input-rc1`.

## Matrix Input RC1

For the next phase:

- treat reverse scan as the canonical input scan
- promote `MATRIX_KEY row=<r> col=<c> keyId=K_<r>_<c>` to the formal event
- send HID payloads as `K_<r>_<c>:P` and `K_<r>_<c>:R`
- postpone any key-label translation until the electrical mapping is stable
