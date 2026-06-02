# Diagnostics Cheat Sheet

This repo now treats diagnostics as reusable artifacts.

## Main artifacts

- `samples/device-inspector/latest.json`
- `samples/plugin-diagnostics/latest.json`
- `samples/comparer/report.md`

## Capture inspector JSON

```powershell
dotnet run --project tools/YMMKeyboard.DeviceInspector/YMMKeyboard.DeviceInspector.csproj -c Release -- --json --output samples/device-inspector/latest.json
```

## Capture plugin diagnostics JSON

Run YMM with diagnostics enabled, then copy the emitted JSON into:

```text
samples/plugin-diagnostics/latest.json
```

## Compare the two reports

```powershell
dotnet run --project tools/YMMKeyboard.DiagnosticsComparer/YMMKeyboard.DiagnosticsComparer.csproj -c Release -- --inspector samples/device-inspector/latest.json --plugin samples/plugin-diagnostics/latest.json --format markdown --output samples/comparer/report.md
```

## Replay without hardware

```powershell
dotnet run --project tools/YMMKeyboard.ProtocolSimulator/YMMKeyboard.ProtocolSimulator.csproj -c Release -- --inspector samples/device-inspector/latest.json --plugin samples/plugin-diagnostics/latest.json --format markdown --output tmp/protocol-simulator/report.md
```

## Expected sample layout

```text
samples/
  device-inspector/
    latest.json
  plugin-diagnostics/
    latest.json
  comparer/
    report.md
```

## Notes

- Keep the sample JSON stable so CI and docs can reuse it.
- The simulator is read-only and must not touch hardware.
- Hardware validation stays deferred until the later phase.
