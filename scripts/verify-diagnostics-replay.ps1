param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path

$solution = Join-Path $repoRoot "YMMKeyboardPlugin.slnx"
$replayProject = Join-Path $repoRoot "tools\YMMKeyboard.DiagnosticsReplay\YMMKeyboard.DiagnosticsReplay.csproj"
$macroProject = Join-Path $repoRoot "tools\YMMKeyboard.MacroDiagnosticsViewer\YMMKeyboard.MacroDiagnosticsViewer.csproj"
$dispatchProject = Join-Path $repoRoot "tools\YMMKeyboard.DispatchDiagnosticsViewer\YMMKeyboard.DispatchDiagnosticsViewer.csproj"

$devicePath = Join-Path $repoRoot "samples\device-inspector\latest.json"
$pluginPath = Join-Path $repoRoot "samples\plugin-diagnostics\latest.json"
$inputPath = Join-Path $repoRoot "samples\input-diagnostics\single-input.json"
$macroScenario = Join-Path $repoRoot "samples\macro-scenarios\single-macro.json"
$dispatchScenario = Join-Path $repoRoot "samples\dispatch-scenarios\single-action.json"

$macroOutputDir = Join-Path $repoRoot "tmp\macro-diagnostics"
$dispatchOutputDir = Join-Path $repoRoot "tmp\dispatch-diagnostics"
$replayOutputDir = Join-Path $repoRoot "tmp\diagnostics-replay"

foreach ($dir in @($macroOutputDir, $dispatchOutputDir, $replayOutputDir)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

Write-Host "Building solution: $solution"
& dotnet build $solution -c $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Generating macro diagnostics"
$macroOutput = & dotnet run --no-build --project $macroProject -c $Configuration -- `
    --scenario $macroScenario `
    --format markdown `
    --output-dir $macroOutputDir 2>&1
$macroExit = $LASTEXITCODE
$macroText = ($macroOutput | Out-String)
$macroText | Write-Host
if ($macroExit -ne 0) { exit $macroExit }
if ($macroText -notmatch 'issues=0') {
    throw "MacroDiagnosticsViewer did not report issues=0."
}

Write-Host "Generating dispatch diagnostics"
$dispatchOutput = & dotnet run --no-build --project $dispatchProject -c $Configuration -- `
    --scenario $dispatchScenario `
    --format markdown `
    --output-dir $dispatchOutputDir 2>&1
$dispatchExit = $LASTEXITCODE
$dispatchText = ($dispatchOutput | Out-String)
$dispatchText | Write-Host
if ($dispatchExit -ne 0) { exit $dispatchExit }
if ($dispatchText -notmatch 'issues=0') {
    throw "DispatchDiagnosticsViewer did not report issues=0."
}

$macroReport = Join-Path $macroOutputDir "macro-diagnostics-single-macro.json"
$dispatchReport = Join-Path $dispatchOutputDir "dispatch-diagnostics-single-action.json"

if (-not (Test-Path $macroReport)) {
    throw "Macro diagnostics report was not written: $macroReport"
}
if (-not (Test-Path $dispatchReport)) {
    throw "Dispatch diagnostics report was not written: $dispatchReport"
}

Write-Host "Running unified diagnostics replay"
$replayOutput = & dotnet run --no-build --project $replayProject -c $Configuration -- `
    --device $devicePath `
    --plugin $pluginPath `
    --input $inputPath `
    --macro $macroReport `
    --dispatch $dispatchReport `
    --format markdown `
    --output-dir $replayOutputDir 2>&1
$replayExit = $LASTEXITCODE
$replayText = ($replayOutput | Out-String)
$replayText | Write-Host
if ($replayExit -ne 0) { exit $replayExit }
if ($replayText -notmatch 'issues=0') {
    throw "DiagnosticsReplay did not report issues=0."
}

Write-Host "Diagnostics replay verification completed successfully."
