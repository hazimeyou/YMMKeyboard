# release-prep-rc1

## Scope

This note records the pre-release checks for `v0.1.0a` without changing runtime behavior.

## README Check

### Root README

- Present: overview, repository structure, build command
- Missing or not explicit enough: features, required environment, install steps, usage steps, known limitations, license section

### Component READMEs

- [ymm-plugin/README.md](../../ymm-plugin/README.md)
- [firmware/README.md](../../firmware/README.md)
- [tools/README.md](../../tools/README.md)
- [driver/README.md](../../driver/README.md)
- [hardware/README.md](../../hardware/README.md)

These are present, but they are still more internal/working-note oriented than release-oriented.

## License Check

- `LICENSE`: present, MIT License
- `NOTICE`: not present
- `third-party/`: not present

## Dependency Check

Likely external dependencies to call out in release notes or package notes:

- `System.IO.Ports`
- `HidSharp`
- `YukkuriMovieMaker` DLL references
- `YukkuriMovieMaker.Plugin`
- `YukkuriMovieMaker.Controls`

## Distribution Candidates

- `ymm-plugin/src/YMMKeyboardPlugin/bin/<configuration>/net10.0-windows/YMMKeyboardPlugin.dll`
- `firmware/src/RP2040TinyUsb/build/ymm_keyboard_fw.uf2`
- `README.md`
- `LICENSE`

## Release Draft

Candidate version: `v0.1.0a`

Draft headline:

- Initial public release
- RP2040 support
- USB HID support
- Matrix keyboard support
- Rotary encoder support
- YMM4 operation support
- Legacy COM diagnostic fallback support

## Pre-Release Checklist

- [x] Plugin build succeeds
- [x] Firmware build succeeds
- [x] working-baseline-rc1 is fixed
- [x] cleanup-rc1 is fixed
- [x] com-legacy-rc1 is fixed
- [ ] Root README release sections are expanded
- [ ] License/third-party notes are consolidated
- [ ] Distribution package contents are finalized
- [ ] GitHub Release draft is prepared

## Open Items

- Confirm final package naming for the `v0.1.0a` artifact set.
- Decide whether to add a small `NOTICE` file for bundled third-party attribution notes, if required.
- Expand the root README only after the release-prep scope is approved.
