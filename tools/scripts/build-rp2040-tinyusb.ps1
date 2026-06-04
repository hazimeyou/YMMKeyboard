param(
    [switch]$Reconfigure
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\\..")
$fwRoot = Join-Path $repoRoot "firmware\\src\\RP2040TinyUsb"
$defaultSdk = Join-Path $repoRoot "tmp\\pico\\pico-sdk"

$cmakeCmd = Get-Command cmake -ErrorAction SilentlyContinue
if ($cmakeCmd) {
    $cmakeExe = $cmakeCmd.Source
}
else {
    $cmakeFallback = "C:\\Program Files\\CMake\\bin\\cmake.exe"
    if (Test-Path $cmakeFallback) {
        $cmakeExe = $cmakeFallback
    }
    else {
        throw "cmake is not available. Please install CMake or add it to PATH."
    }
}

if (-not $env:PICO_SDK_PATH) {
    if (Test-Path $defaultSdk) {
        $env:PICO_SDK_PATH = [System.IO.Path]::GetFullPath($defaultSdk)
    }
    else {
        throw "PICO_SDK_PATH is not set. Run setup-rp2040-build-env.ps1 first."
    }
}

$gccCmd = Get-Command arm-none-eabi-gcc -ErrorAction SilentlyContinue
if (-not $gccCmd) {
    $toolchainFallback = "C:\\Program Files (x86)\\Arm GNU Toolchain arm-none-eabi\\14.2 rel1\\bin"
    if (Test-Path (Join-Path $toolchainFallback "arm-none-eabi-gcc.exe")) {
        $env:PICO_TOOLCHAIN_PATH = $toolchainFallback
    }
}

Write-Host "[build] firmwareRoot=$fwRoot"
Write-Host "[build] PICO_SDK_PATH=$env:PICO_SDK_PATH"
if ($env:PICO_TOOLCHAIN_PATH) {
    Write-Host "[build] PICO_TOOLCHAIN_PATH=$env:PICO_TOOLCHAIN_PATH"
}

Push-Location $fwRoot
try {
    $ninjaCmd = Get-Command ninja -ErrorAction SilentlyContinue
    $ninjaExe = $null
    if ($ninjaCmd) {
        $ninjaExe = $ninjaCmd.Source
    }
    else {
        $ninjaFallback = "C:\\Users\\yu-za-hazimeyou\\AppData\\Local\\Microsoft\\WinGet\\Packages\\Ninja-build.Ninja_Microsoft.Winget.Source_8wekyb3d8bbwe\\ninja.exe"
        if (Test-Path $ninjaFallback) {
            $ninjaExe = $ninjaFallback
        }
    }
    $hasNinja = -not [string]::IsNullOrWhiteSpace($ninjaExe)
    $cacheFile = Join-Path $fwRoot "build\\CMakeCache.txt"
    if (Test-Path $cacheFile) {
        $cache = Get-Content $cacheFile -Raw
        if ((($hasNinja -and $cache -match "CMAKE_GENERATOR:INTERNAL=Visual Studio") -or ((-not $hasNinja) -and $cache -match "CMAKE_GENERATOR:INTERNAL=Ninja"))) {
            Write-Host "[build] generator changed. cleaning build directory..."
            Remove-Item (Join-Path $fwRoot "build") -Recurse -Force
        }
    }

    $ninjaFile = Join-Path $fwRoot "build\\build.ninja"
    $slnFile = Get-ChildItem (Join-Path $fwRoot "build") -Filter "*.sln" -ErrorAction SilentlyContinue | Select-Object -First 1

    if ($Reconfigure -or (-not (Test-Path $ninjaFile) -and -not $slnFile)) {
        if ($hasNinja) {
            & $cmakeExe -S . -B build -G Ninja -DCMAKE_MAKE_PROGRAM="$ninjaExe" -DPICO_BOARD=waveshare_rp2040_zero -DCMAKE_BUILD_TYPE=Release
        }
        else {
            & $cmakeExe -S . -B build -G "Visual Studio 17 2022" -A Win32 -DPICO_BOARD=waveshare_rp2040_zero
        }
        $slnFile = Get-ChildItem (Join-Path $fwRoot "build") -Filter "*.sln" -ErrorAction SilentlyContinue | Select-Object -First 1
    }

    if (Test-Path $ninjaFile) {
        & $cmakeExe --build build
    }
    elseif ($slnFile) {
        & $cmakeExe --build build --config Release
    }
    else {
        throw "No build system found in build directory."
    }
}
finally {
    Pop-Location
}

$uf2 = Join-Path $fwRoot "build\\ymm_keyboard_fw.uf2"
$bin = Join-Path $fwRoot "build\\ymm_keyboard_fw.bin"
$uf2conv = Join-Path $repoRoot "tools\\scripts\\rp2040_tools\\uf2conv.py"
if (Test-Path $uf2) {
    Remove-Item $uf2 -Force
}
if ((Test-Path $bin) -and (Test-Path $uf2conv)) {
    $pythonExe = $null
    $pyCmd = Get-Command py -ErrorAction SilentlyContinue
    if ($pyCmd) {
        $pythonExe = $pyCmd.Source
    }
    else {
        $pythonCmd = Get-Command python -ErrorAction SilentlyContinue
        if ($pythonCmd -and $pythonCmd.CommandType -ne "Application") {
            $pythonCmd = $null
        }
        if ($pythonCmd) {
            $pythonExe = $pythonCmd.Source
        }
        elseif (Test-Path "C:\Users\yu-za-hazimeyou\AppData\Local\Python\pythoncore-3.14-64\python.exe") {
            $pythonExe = "C:\Users\yu-za-hazimeyou\AppData\Local\Python\pythoncore-3.14-64\python.exe"
        }
        elseif (Test-Path "C:\Windows\py.exe") {
            $pythonExe = "C:\Windows\py.exe"
        }
    }
    if ($pythonExe) {
        & $pythonExe $uf2conv --base 0x10000000 --family RP2040 --convert $bin --output $uf2
    }
}

if (Test-Path $uf2) {
    Write-Host "[build] UF2 generated: $uf2"
}
else {
    throw "UF2 was not generated."
}
