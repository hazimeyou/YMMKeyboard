param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path

$solution = Join-Path $repoRoot "YMMKeyboardPlugin.slnx"
$viewerProject = Join-Path $repoRoot "tools\YMMKeyboard.MacroDiagnosticsViewer\YMMKeyboard.MacroDiagnosticsViewer.csproj"
$sampleDir = Join-Path $repoRoot "samples\macro-scenarios"
$outputDir = Join-Path $repoRoot "tmp\macro-diagnostics"

if (-not (Test-Path $sampleDir)) {
    throw "Required sample directory is missing: $sampleDir"
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

Write-Host "Building solution: $solution"
& dotnet build $solution -c $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$samples = @(
    "single-macro.json",
    "nested-macro.json",
    "invalid-macro.json",
    "large-macro.json",
    "missing-macro.json"
)

foreach ($sample in $samples) {
    $inputPath = Join-Path $sampleDir $sample
    if (-not (Test-Path $inputPath)) {
        throw "Required sample file is missing: $inputPath"
    }

    Write-Host "Running MacroDiagnosticsViewer: $sample"
    $scenarioOutput = & dotnet run --no-build --project $viewerProject -c $Configuration -- `
        --scenario $inputPath `
        --format markdown `
        --output-dir $outputDir 2>&1
    $scenarioExit = $LASTEXITCODE
    $scenarioText = ($scenarioOutput | Out-String)
    $scenarioText | Write-Host
    if ($scenarioExit -ne 0) {
        exit $scenarioExit
    }

    if ($scenarioText -notmatch 'issues=0') {
        throw "MacroDiagnosticsViewer did not report issues=0 for $sample."
    }

    $reportName = "macro-diagnostics-" + [System.IO.Path]::GetFileNameWithoutExtension($sample) + ".json"
    $reportPath = Join-Path $outputDir $reportName
    if (-not (Test-Path $reportPath)) {
        throw "Macro diagnostics report was not written: $reportPath"
    }

    Write-Host "Replaying MacroDiagnosticsViewer: $reportName"
    $replayOutput = & dotnet run --no-build --project $viewerProject -c $Configuration -- `
        --replay $reportPath `
        --format markdown `
        --output-dir $outputDir 2>&1
    $replayExit = $LASTEXITCODE
    $replayText = ($replayOutput | Out-String)
    $replayText | Write-Host
    if ($replayExit -ne 0) {
        exit $replayExit
    }

    if ($replayText -notmatch 'issues=0') {
        throw "MacroDiagnosticsViewer replay did not report issues=0 for $sample."
    }
}

Write-Host "Macro diagnostics verification completed successfully."
