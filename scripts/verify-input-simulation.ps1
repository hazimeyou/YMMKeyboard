param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path

$solution = Join-Path $repoRoot "YMMKeyboardPlugin.slnx"
$simulatorProject = Join-Path $repoRoot "tools\YMMKeyboard.InputSimulator\YMMKeyboard.InputSimulator.csproj"
$viewerProject = Join-Path $repoRoot "tools\YMMKeyboard.InputDiagnosticsViewer\YMMKeyboard.InputDiagnosticsViewer.csproj"
$scenarioDir = Join-Path $repoRoot "samples\input-scenarios"
$diagnosticsDir = Join-Path $repoRoot "tmp\input-diagnostics"
$simulationDir = Join-Path $repoRoot "tmp\input-simulator"

if (-not (Test-Path $scenarioDir)) {
    throw "Required scenario directory is missing: $scenarioDir"
}

New-Item -ItemType Directory -Force -Path $diagnosticsDir | Out-Null
New-Item -ItemType Directory -Force -Path $simulationDir | Out-Null

Write-Host "Building solution: $solution"
& dotnet build $solution -c $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Running InputSimulator batch: $scenarioDir"
$simulatorOutput = & dotnet run --no-build --project $simulatorProject -c $Configuration -- `
    --batch $scenarioDir `
    --output-dir $simulationDir `
    --format markdown 2>&1
$simulatorExit = $LASTEXITCODE
$simulatorText = ($simulatorOutput | Out-String)
$simulatorText | Write-Host
if ($simulatorExit -ne 0) {
    exit $simulatorExit
}

if ($simulatorText -notmatch 'issues=0') {
    throw "InputSimulator did not report issues=0."
}

$scenarioOutputs = @(
    "input-diagnostics-single-key.json",
    "input-diagnostics-modifier-key.json",
    "input-diagnostics-macro-trigger.json",
    "input-diagnostics-invalid-input.json"
)

foreach ($scenarioOutput in $scenarioOutputs) {
    $diagnosticsPath = Join-Path $diagnosticsDir $scenarioOutput
    if (-not (Test-Path $diagnosticsPath)) {
        throw "Expected diagnostics file was not written: $diagnosticsPath"
    }

    Write-Host "Running InputDiagnosticsViewer: $scenarioOutput"
    $viewerOutput = & dotnet run --no-build --project $viewerProject -c $Configuration -- `
        --input $diagnosticsPath `
        --format json `
        --output (Join-Path $simulationDir ($scenarioOutput + ".viewer.json")) 2>&1
    $viewerExit = $LASTEXITCODE
    $viewerText = ($viewerOutput | Out-String)
    $viewerText | Write-Host
    if ($viewerExit -ne 0) {
        exit $viewerExit
    }

    if ($viewerText -notmatch 'issues=0') {
        throw "InputDiagnosticsViewer did not report issues=0 for $scenarioOutput."
    }
}

foreach ($scenarioOutput in $scenarioOutputs) {
    $diagnosticsPath = Join-Path $diagnosticsDir $scenarioOutput
    Write-Host "Replaying diagnostics: $scenarioOutput"
    $replayOutput = & dotnet run --no-build --project $simulatorProject -c $Configuration -- `
        --replay $diagnosticsPath `
        --output-dir $simulationDir `
        --format markdown 2>&1
    $replayExit = $LASTEXITCODE
    $replayText = ($replayOutput | Out-String)
    $replayText | Write-Host
    if ($replayExit -ne 0) {
        exit $replayExit
    }

    if ($replayText -notmatch 'issues=0') {
        throw "InputSimulator replay did not report issues=0 for $scenarioOutput."
    }
}

Write-Host "Input simulation verification completed successfully."
