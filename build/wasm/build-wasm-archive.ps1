[CmdletBinding()]
param(
    [string]$BuildDir,
    [string]$DotnetRoot,
    [string]$SdkVersion,
    [switch]$LegacyExceptions
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $BuildDir) { $BuildDir = Join-Path $repoRoot 'src/CalculatorDotNet/obj/native/wasm' }

if (-not (Test-Path (Join-Path $repoRoot 'external/calculator/src/CalcManager/UnitConverter.cpp'))) {
    throw "external/calculator is empty. Run: git submodule update --init"
}

if (-not $DotnetRoot) {
    $DotnetRoot = if ($env:DOTNET_ROOT) { $env:DOTNET_ROOT } else { Split-Path -Parent (Get-Command dotnet).Source }
}
$packs = Join-Path $DotnetRoot 'packs'
if (-not (Test-Path $packs)) { throw "No dotnet packs directory at '$packs'. Pass -DotnetRoot." }

if (-not $SdkVersion) { $SdkVersion = (& dotnet --version) }
$sdkMajor = [int](($SdkVersion -split '\.')[0])

function Get-VersionPrefix([string]$name) {
    if ($name -match '^(\d+(\.\d+)*)') { [version]$Matches[1] } else { [version]'0.0' }
}

function Get-BandDir([string]$packDir) {
    Get-ChildItem $packDir -Directory |
        Where-Object { (Get-VersionPrefix $_.Name).Major -eq $sdkMajor } |
        Sort-Object { Get-VersionPrefix $_.Name }, Name |
        Select-Object -Last 1
}

$sdkPackRe = '^Microsoft\.NET\.Runtime\.Emscripten\.(?<ver>[\d.]+)\.Sdk\.(?<rid>[\w-]+)$'
$sdkPack = Get-ChildItem $packs -Directory |
    ForEach-Object {
        if ($_.Name -match $sdkPackRe) {
            [pscustomobject]@{ Dir = $_.FullName; EmVersion = $Matches.ver; Rid = $Matches.rid }
        }
    } |
    Where-Object { Get-BandDir $_.Dir } |
    Sort-Object { [version]$_.EmVersion } |
    Select-Object -Last 1

if (-not $sdkPack) {
    throw "No Emscripten SDK pack for .NET $sdkMajor under '$packs'. Install the workload: dotnet workload install wasm-tools"
}

$emVersion = $sdkPack.EmVersion
$rid = $sdkPack.Rid

function Resolve-Pack([string]$kind, [switch]$Optional) {
    $dir = Join-Path $packs "Microsoft.NET.Runtime.Emscripten.$emVersion.$kind.$rid"
    $band = if (Test-Path $dir) { Get-BandDir $dir } else { $null }
    if (-not $band) {
        if ($Optional) { return $null }
        throw "Missing Emscripten $kind pack for .NET $sdkMajor at '$dir'."
    }
    $band.FullName
}

$sdkTools = Join-Path (Resolve-Pack 'Sdk') 'tools'
$nodeTools = Join-Path (Resolve-Pack 'Node') 'tools'
$cacheTools = Join-Path (Resolve-Pack 'Cache') 'tools'

$pythonPack = Resolve-Pack 'Python' -Optional
$pythonTools = if ($pythonPack) { Join-Path $pythonPack 'tools' } else { $null }

$isWin = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Windows)
$exe = if ($isWin) { '.exe' } else { '' }

$node = Get-ChildItem (Join-Path $nodeTools 'bin') -Filter "node$exe" -ErrorAction SilentlyContinue |
    Select-Object -First 1
if (-not $node) { throw "node not found under '$nodeTools'." }

$python = if ($pythonTools) {
    Get-ChildItem $pythonTools -Filter "python*$exe" -ErrorAction SilentlyContinue |
        Where-Object Name -match "^python3?$exe$" | Select-Object -First 1
} else {
    $cmd = Get-Command python3 -ErrorAction SilentlyContinue
    if (-not $cmd) { throw "No Emscripten Python pack for this platform and no python3 on PATH. Install python3." }
    $null  # system python3 is on PATH already; emcc finds it without EMSDK_PYTHON
}

$env:EM_CONFIG = Join-Path $sdkTools 'emscripten/.emscripten'
$env:DOTNET_EMSCRIPTEN_LLVM_ROOT = Join-Path $sdkTools 'bin'
$env:DOTNET_EMSCRIPTEN_BINARYEN_ROOT = $sdkTools
$env:DOTNET_EMSCRIPTEN_NODE_JS = $node.FullName
$env:EM_CACHE = Join-Path $cacheTools 'emscripten/cache'
if ($python) { $env:EMSDK_PYTHON = $python.FullName }

$sep = [System.IO.Path]::PathSeparator
$prepend = @(
    (Join-Path $sdkTools 'bin')
    (Join-Path $sdkTools 'emscripten')
    (Join-Path $nodeTools 'bin')
    $pythonTools
) | Where-Object { $_ }
$prepend = $prepend -join $sep
$env:PATH = $prepend + $sep + $env:PATH

$toolchain = (Join-Path $sdkTools 'emscripten/cmake/Modules/Platform/Emscripten.cmake') -replace '\\', '/'
if (-not (Test-Path $toolchain)) { throw "Emscripten CMake toolchain not found at '$toolchain'." }

Write-Host "Building CalcManagerShim for WebAssembly (Emscripten $emVersion, exceptions: $(if ($LegacyExceptions) { '-fexceptions' } else { '-fwasm-exceptions' }))"

function Find-Tool([string]$name) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    if (-not $isWin) { return $null }
    $roots = @(
        [Environment]::GetEnvironmentVariable('PATH', 'User')
        [Environment]::GetEnvironmentVariable('PATH', 'Machine')
    ) -join $sep
    foreach ($dir in ($roots -split $sep | Where-Object { $_ })) {
        $candidate = Join-Path $dir "$name$exe"
        if (Test-Path $candidate) { return $candidate }
    }
    return $null
}

$ninja = Find-Tool 'ninja'
$generator = if ($ninja) { 'Ninja' } else { 'Unix Makefiles' }

$cmakeArgs = @(
    '-S', (Join-Path $repoRoot 'src/native')
    '-B', $BuildDir
    '-G', $generator
    "-DCMAKE_TOOLCHAIN_FILE=$toolchain"
    '-DCMAKE_BUILD_TYPE=Release'
    '-DCALCSHIM_STATIC=ON'
    "-DCALCSHIM_WASM_LEGACY_EH=$(if ($LegacyExceptions) { 'ON' } else { 'OFF' })"
)

if ($ninja) {
    $cmakeArgs += "-DCMAKE_MAKE_PROGRAM=$($ninja -replace '\\', '/')"
} elseif (-not (Find-Tool 'make')) {
    throw "Neither ninja nor make was found. Install one (winget install Ninja-build.Ninja) so CMake has a build tool."
}

# A cache generated by a different generator makes configure fail outright rather than
# regenerate, which is easy to hit when ninja gets installed after a first attempt.
$cache = Join-Path $BuildDir 'CMakeCache.txt'
if (Test-Path $cache) {
    $cached = (Select-String -Path $cache -Pattern '^CMAKE_GENERATOR:INTERNAL=(.*)$' |
        Select-Object -First 1).Matches.Groups[1].Value
    if ($cached -and $cached -ne $generator) {
        Write-Host "Generator changed ($cached -> $generator); reconfiguring from scratch."
        Remove-Item -Recurse -Force $BuildDir
    }
}

& cmake @cmakeArgs
if ($LASTEXITCODE -ne 0) { throw "cmake configure failed." }

& cmake --build $BuildDir --parallel
if ($LASTEXITCODE -ne 0) { throw "cmake build failed." }

$archive = Join-Path $BuildDir 'CalcManagerShim.a'
if (-not (Test-Path $archive)) { throw "Expected archive not produced at '$archive'." }

$stamp = if ($LegacyExceptions) { 'legacy' } else { 'wasm-eh' }
Set-Content -Path (Join-Path $BuildDir 'exception-mode.txt') -Value $stamp -NoNewline

Write-Host "==> $archive"
