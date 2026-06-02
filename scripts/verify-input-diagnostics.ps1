param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path

$solution = Join-Path $repoRoot "YMMKeyboardPlugin.slnx"
$viewerProject = Join-Path $repoRoot "tools\YMMKeyboard.InputDiagnosticsViewer\YMMKeyboard.InputDiagnosticsViewer.csproj"
$sampleDir = Join-Path $repoRoot "samples\input-diagnostics"
$outputDir = Join-Path $repoRoot "tmp\input-diagnostics"

if (-not (Test-Path $sampleDir)) {
    throw "Required sample directory is missing: $sampleDir"
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

Write-Host "Building solution: $solution"
& dotnet build $solution -c $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$samples = @(
    "single-input.json",
    "mapped-input.json",
    "macro-input.json",
    "rejected-input.json"
)

foreach ($sample in $samples) {
    $inputPath = Join-Path $sampleDir $sample
    if (-not (Test-Path $inputPath)) {
        throw "Required sample file is missing: $inputPath"
    }

    $outputName = "input-diagnostics-" + [System.IO.Path]::GetFileNameWithoutExtension($sample) + ".json"
    $outputPath = Join-Path $outputDir $outputName

    Write-Host "Running InputDiagnosticsViewer: $sample"
    $viewerOutput = & dotnet run --no-build --project $viewerProject -c $Configuration -- `
        --input $inputPath `
        --format json `
        --output $outputPath 2>&1
    $viewerExit = $LASTEXITCODE
    $viewerText = ($viewerOutput | Out-String)
    $viewerText | Write-Host
    if ($viewerExit -ne 0) {
        exit $viewerExit
    }

    if ($viewerText -notmatch 'issues=0') {
        throw "InputDiagnosticsViewer did not report issues=0 for $sample."
    }

    if (-not (Test-Path $outputPath)) {
        throw "Viewer report was not written: $outputPath"
    }
}

Write-Host "Input diagnostics verification completed successfully."
