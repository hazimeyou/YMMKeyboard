param(
    [string]$PicoSdkRoot = "tmp\\pico\\pico-sdk"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\\..")
$fwRoot = Join-Path $repoRoot "firmware\\src\\RP2040TinyUsb"
$sdkRootAbs = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $PicoSdkRoot))
$sdkParent = Split-Path $sdkRootAbs -Parent

Write-Host "[setup] repoRoot=$repoRoot"
Write-Host "[setup] firmwareRoot=$fwRoot"
Write-Host "[setup] picoSdk=$sdkRootAbs"

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "git not found. Install Git for Windows."
}

if (-not (Get-Command cmake -ErrorAction SilentlyContinue)) {
    throw "cmake not found. Install CMake."
}

$hasNinja = [bool](Get-Command ninja -ErrorAction SilentlyContinue)
if (-not $hasNinja) {
    Write-Warning "ninja not found. Fallback to Visual Studio generator."
}

$buildDir = Join-Path $fwRoot "build"
$cacheFile = Join-Path $buildDir "CMakeCache.txt"
if (Test-Path $cacheFile) {
    $cache = Get-Content $cacheFile -Raw
    if ((($hasNinja -and $cache -match "CMAKE_GENERATOR:INTERNAL=Visual Studio") -or ((-not $hasNinja) -and $cache -match "CMAKE_GENERATOR:INTERNAL=Ninja"))) {
        Write-Host "[setup] generator changed. cleaning build directory..."
        Remove-Item $buildDir -Recurse -Force
    }
}

if (-not (Test-Path $sdkRootAbs)) {
    New-Item -ItemType Directory -Path $sdkParent -Force | Out-Null
    Write-Host "[setup] cloning pico-sdk..."
    git clone --depth 1 https://github.com/raspberrypi/pico-sdk.git $sdkRootAbs
    Push-Location $sdkRootAbs
    try {
        git submodule update --init --recursive
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Host "[setup] pico-sdk already exists."
}

$env:PICO_SDK_PATH = $sdkRootAbs
Write-Host "[setup] PICO_SDK_PATH=$env:PICO_SDK_PATH"

Push-Location $fwRoot
try {
    if ($hasNinja) {
        cmake -S . -B build -G Ninja -DPICO_BOARD=waveshare_rp2040_zero -DCMAKE_BUILD_TYPE=Release
    }
    else {
        cmake -S . -B build -G "Visual Studio 17 2022" -A Win32 -DPICO_BOARD=waveshare_rp2040_zero
    }
}
finally {
    Pop-Location
}

Write-Host "[setup] done"
