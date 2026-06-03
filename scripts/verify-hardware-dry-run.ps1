param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path

$requiredDocs = @(
    "docs\hardware-validation-plan.md",
    "docs\hardware-inventory.md",
    "docs\hardware-validation-checklist.md",
    "docs\hardware-validation-report-template.md",
    "docs\hardware-validation-dry-run.md"
)

$sampleReport = Join-Path $repoRoot "samples\hardware-validation\sample-report.md"
$preparationScript = Join-Path $repoRoot "scripts\verify-hardware-preparation.ps1"

foreach ($relativePath in $requiredDocs) {
    $fullPath = Join-Path $repoRoot $relativePath
    if (-not (Test-Path $fullPath)) {
        throw "Required document is missing: $fullPath"
    }
}

if (-not (Test-Path $sampleReport)) {
    throw "Required sample report is missing: $sampleReport"
}

if (-not (Test-Path $preparationScript)) {
    throw "Required preparation script is missing: $preparationScript"
}

Write-Host "Running hardware preparation verification"
& $preparationScript -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Hardware dry run verification completed successfully."
