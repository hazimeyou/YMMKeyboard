param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path

$solution = Join-Path $repoRoot "YMMKeyboardPlugin.slnx"
$verifyScripts = @(
    "scripts\verify-diagnostics.ps1",
    "scripts\verify-input-diagnostics.ps1",
    "scripts\verify-input-simulation.ps1",
    "scripts\verify-macro-diagnostics.ps1",
    "scripts\verify-dispatch-diagnostics.ps1",
    "scripts\verify-diagnostics-replay.ps1"
)

Write-Host "Building solution: $solution"
& dotnet build $solution -c $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

foreach ($relativeScript in $verifyScripts) {
    $scriptPath = Join-Path $repoRoot $relativeScript
    if (-not (Test-Path $scriptPath)) {
        throw "Required verification script is missing: $scriptPath"
    }

    Write-Host "Running verification: $relativeScript"
    & $scriptPath -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

Write-Host "Hardware preparation verification completed successfully."
