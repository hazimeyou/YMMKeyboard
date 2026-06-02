param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path

$solution = Join-Path $repoRoot "YMMKeyboardPlugin.slnx"
$inspectorSample = Join-Path $repoRoot "samples\device-inspector\latest.json"
$pluginSample = Join-Path $repoRoot "samples\plugin-diagnostics\latest.json"
$comparerProject = Join-Path $repoRoot "tools\YMMKeyboard.DiagnosticsComparer\YMMKeyboard.DiagnosticsComparer.csproj"
$simulatorProject = Join-Path $repoRoot "tools\YMMKeyboard.ProtocolSimulator\YMMKeyboard.ProtocolSimulator.csproj"
$diagnosticsTmp = Join-Path $repoRoot "tmp\diagnostics-ci"
$comparerReport = Join-Path $diagnosticsTmp "report.md"
$simulatorReport = Join-Path $diagnosticsTmp "protocol-simulator-report.md"

foreach ($path in @($inspectorSample, $pluginSample)) {
    if (-not (Test-Path $path)) {
        throw "Required sample file is missing: $path"
    }
}

New-Item -ItemType Directory -Force -Path $diagnosticsTmp | Out-Null

Write-Host "Building solution: $solution"
& dotnet build $solution -c $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Running DiagnosticsComparer against samples"
$comparerOutput = & dotnet run --no-build --project $comparerProject -c $Configuration -- `
    --inspector $inspectorSample `
    --plugin $pluginSample `
    --format markdown `
    --output $comparerReport 2>&1
$comparerExit = $LASTEXITCODE
$comparerText = ($comparerOutput | Out-String)
$comparerText | Write-Host
if ($comparerExit -ne 0) {
    exit $comparerExit
}
if ($comparerText -notmatch 'issues=0') {
    throw "DiagnosticsComparer did not report issues=0."
}
if (-not (Test-Path $comparerReport)) {
    throw "Comparer report was not written: $comparerReport"
}

Write-Host "Running ProtocolSimulator against samples"
$simulatorOutput = & dotnet run --no-build --project $simulatorProject -c $Configuration -- `
    --inspector $inspectorSample `
    --plugin $pluginSample `
    --format markdown `
    --output $simulatorReport 2>&1
$simulatorExit = $LASTEXITCODE
$simulatorText = ($simulatorOutput | Out-String)
$simulatorText | Write-Host
if ($simulatorExit -ne 0) {
    exit $simulatorExit
}
if ($simulatorText -notmatch 'issues=0') {
    throw "ProtocolSimulator did not report issues=0."
}
if (-not (Test-Path $simulatorReport)) {
    throw "Simulator report was not written: $simulatorReport"
}

Write-Host "Diagnostics verification completed successfully."
