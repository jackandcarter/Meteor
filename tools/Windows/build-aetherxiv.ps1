param(
    [string]$Configuration = $(if ($env:AETHERXIV_BUILD_CONFIGURATION) { $env:AETHERXIV_BUILD_CONFIGURATION } else { "Release" }),
    [string]$ServerRid = $(if ($env:AETHERXIV_SERVER_RID) { $env:AETHERXIV_SERVER_RID } else { "win-x64" }),
    [string]$LauncherRid = $(if ($env:AETHERXIV_LAUNCHER_RID) { $env:AETHERXIV_LAUNCHER_RID } else { "win-x64" })
)

$ErrorActionPreference = "Stop"

$rootDir = Resolve-Path (Join-Path $PSScriptRoot "..\..")
if ($Configuration -notin @("Debug", "Release")) { throw "Configuration must be Debug or Release." }
$OutputRoot = Join-Path $rootDir "bin\build\$Configuration\Windows"

$dotnet = if ($env:DOTNET_BIN) { $env:DOTNET_BIN } else { "dotnet" }
$python = $null
$pythonPrefixArgs = @()
if ($env:PYTHON_BIN) {
    $python = $env:PYTHON_BIN
}
else {
    foreach ($candidate in @("python3", "python", "py")) {
        $command = Get-Command $candidate -ErrorAction SilentlyContinue
        if ($command) {
            $python = $command.Source
            if ($candidate -eq "py") { $pythonPrefixArgs = @("-3") }
            break
        }
    }
}
$launcherRoot = Join-Path $rootDir "AetherXIV Launcher"
$umbraVersion = if ($env:AETHERXIV_UMBRA_VERSION) { $env:AETHERXIV_UMBRA_VERSION } else { "2.0.0" }
$releaseWorkRoot = Join-Path $rootDir "bin\build\.work\$Configuration\Windows"
$env:AetherXivWorkRoot = $releaseWorkRoot

function Publish-Project {
    param(
        [string]$ProjectPath,
        [string]$OutputPath,
        [string[]]$ExtraArgs = @(),
        [switch]$SelfContained
    )

    $selfContainedValue = if ($SelfContained) { "true" } else { "false" }
    New-Item -ItemType Directory -Force -Path $OutputPath | Out-Null
    & $dotnet publish $ProjectPath `
        --configuration $Configuration `
        --self-contained $selfContainedValue `
        --output $OutputPath `
        -m:1 `
        /nodeReuse:false `
        /p:NuGetAudit=false `
        /p:PublishSingleFile=false `
        /p:UseAppHost=true `
        @ExtraArgs
}

function Reset-OutputRoot {
    $binRoot = Join-Path $rootDir "bin"
    $buildRoot = Join-Path $binRoot "build"
    New-Item -ItemType Directory -Force -Path $buildRoot | Out-Null
    Get-ChildItem -Force $binRoot | Where-Object { $_.Name -ne "build" } | Remove-Item -Recurse -Force
    $workRoot = Join-Path $buildRoot ".work"
    if (Test-Path $workRoot) { Remove-Item -Recurse -Force $workRoot }
    Get-ChildItem -Force $buildRoot | Where-Object { $_.Name -notin @("Debug", "Release") } | Remove-Item -Recurse -Force
    # A platform package is a complete release image. Recreate it so stale
    # diagnostics or superseded payloads cannot survive a later build.
    if (Test-Path $OutputRoot) { Remove-Item -Recurse -Force $OutputRoot }
    New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
}

function Resolve-MSBuild {
    if ($env:MSBUILD_EXE -and (Test-Path $env:MSBUILD_EXE)) {
        return $env:MSBUILD_EXE
    }

    $programFilesX86 = [Environment]::GetEnvironmentVariable("ProgramFiles(x86)")
    if (-not [string]::IsNullOrWhiteSpace($programFilesX86)) {
        $vswhere = Join-Path $programFilesX86 "Microsoft Visual Studio\Installer\vswhere.exe"
        if (Test-Path $vswhere) {
            $candidate = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
            if ($candidate) {
                return $candidate
            }
        }
    }

    return $null
}

function Assert-BuildPrerequisites {
    $missing = @()
    $dotnetCommand = Get-Command $dotnet -ErrorAction SilentlyContinue
    $hasPinnedSdk = $false
    if ($dotnetCommand) {
        $hasPinnedSdk = @(& $dotnet --list-sdks 2>$null) -match '^10\.0\.203\s'
    }
    if (-not $hasPinnedSdk) {
        $missing += ".NET SDK 10.0.203 (dotnet)"
    }
    if (-not $python) {
        $missing += "Python 3"
    }
    if (-not (Resolve-MSBuild) -and -not (Get-Command "i686-w64-mingw32-g++" -ErrorAction SilentlyContinue)) {
        $missing += "Visual Studio C++ Build Tools or MinGW-w64"
    }

    if ($missing.Count -gt 0) {
        throw "AetherXIV Windows build prerequisites are missing:`n  - $($missing -join "`n  - ")`nSee docs/build/WINDOWS.md before running this build again."
    }
}

function Build-NativeUmbraWithMinGw {
    $mingw = Get-Command "i686-w64-mingw32-g++" -ErrorAction SilentlyContinue
    if (-not $mingw) {
        throw "A full Windows release requires Visual Studio C++ MSBuild or i686-w64-mingw32-g++."
    }

    $nativeRoot = Join-Path $OutputRoot "native"
    $injectorPath = Join-Path $nativeRoot "Umbra.NativeInjector.x86.exe"
    $bootstrapPath = Join-Path $nativeRoot "Aether.Umbra.Bootstrap.x86.dll"
    New-Item -ItemType Directory -Force -Path $nativeRoot | Out-Null

    & $mingw.Source -std=c++20 -O2 -municode -static `
        (Join-Path $launcherRoot "AetherXIV.Launcher.NativeInjector\umbra_native_injector.cpp") `
        -o $injectorPath
    if ($LASTEXITCODE -ne 0) { throw "MinGW native injector build failed." }

    $imgui = Join-Path $launcherRoot "Umbra\vendor\imgui"
    $bootstrap = Join-Path $launcherRoot "Umbra\Aether.Umbra.Bootstrap"
    & $mingw.Source -std=c++20 -O2 -fno-builtin -fno-tree-loop-distribute-patterns `
        -fno-exceptions -fno-rtti -DIMGUI_IMPL_WIN32_DISABLE_GAMEPAD `
        "-I$imgui" "-I$(Join-Path $imgui 'backends')" -shared -static -static-libgcc -static-libstdc++ `
        '-Wl,--kill-at' -o $bootstrapPath `
        (Join-Path $bootstrap "dllmain.cpp") `
        (Join-Path $imgui "imgui.cpp") `
        (Join-Path $imgui "imgui_draw.cpp") `
        (Join-Path $imgui "imgui_tables.cpp") `
        (Join-Path $imgui "imgui_widgets.cpp") `
        (Join-Path $imgui "backends\imgui_impl_dx9.cpp") `
        (Join-Path $imgui "backends\imgui_impl_win32.cpp") `
        -lgdi32 -ldwmapi
    if ($LASTEXITCODE -ne 0) { throw "MinGW Umbra bootstrap build failed." }

    Copy-NativeUmbraPayloads $injectorPath $bootstrapPath
    Remove-Item -Recurse -Force $nativeRoot
}

function Copy-NativeUmbraPayloads {
    param(
        [string]$NativeInjectorPath,
        [string]$BootstrapPath
    )

    foreach ($helperRid in @("win-x64", "win-x86")) {
        $helperRoot = Join-Path $OutputRoot "launcher\app\Helpers\$helperRid"
        if (Test-Path $helperRoot) {
            Copy-Item -Force $NativeInjectorPath (Join-Path $helperRoot "Umbra.NativeInjector.x86.exe")
        }
    }

    $frameworkRoot = Join-Path $OutputRoot "launcher\app\Umbra\Framework"
    New-Item -ItemType Directory -Force -Path $frameworkRoot | Out-Null
    Copy-Item -Force $BootstrapPath (Join-Path $frameworkRoot "Aether.Umbra.Bootstrap.x86.dll")
    $assetsRoot = Join-Path $frameworkRoot "Assets"
    if (Test-Path $assetsRoot) {
        Remove-Item -Recurse -Force $assetsRoot
    }
    Copy-Item -Recurse -Force (Join-Path $launcherRoot "Umbra\assets") $assetsRoot
    Set-Content -Path (Join-Path $frameworkRoot "version.txt") -Value $umbraVersion
}

Assert-BuildPrerequisites
Reset-OutputRoot

Write-Host "Publishing server hosts..."
Publish-Project (Join-Path $rootDir "src\AetherXIV.Core.Map\AetherXIV.Core.Map.csproj") (Join-Path $OutputRoot "servers\map") @("--runtime", $ServerRid)
Publish-Project (Join-Path $rootDir "src\AetherXIV.Core.World\AetherXIV.Core.World.csproj") (Join-Path $OutputRoot "servers\world") @("--runtime", $ServerRid)
Publish-Project (Join-Path $rootDir "src\AetherXIV.Core.Lobby\AetherXIV.Core.Lobby.csproj") (Join-Path $OutputRoot "servers\lobby") @("--runtime", $ServerRid)
Publish-Project (Join-Path $rootDir "src\AetherXIV.Launcher.Host\AetherXIV.Launcher.Host.csproj") (Join-Path $OutputRoot "servers\launcher-services") @("--runtime", $ServerRid)
& $python @pythonPrefixArgs (Join-Path $rootDir "tools\Universal\lua-tree-manifest.py") `
    --scripts-root (Join-Path $OutputRoot "servers\map\scripts") `
    --manifest (Join-Path $OutputRoot "servers\map\scripts.manifest.json") `
    --write
if ($LASTEXITCODE -ne 0) { throw "Lua inventory generation failed." }

Write-Host "Publishing launcher app and helpers..."
Publish-Project (Join-Path $launcherRoot "AetherXIV.Launcher.App\AetherXIV.Launcher.App.csproj") (Join-Path $OutputRoot "launcher\app") @("--runtime", $LauncherRid) -SelfContained
Publish-Project (Join-Path $launcherRoot "AetherXIV.Launcher.ClientLauncher\AetherXIV.Launcher.ClientLauncher.csproj") (Join-Path $OutputRoot "launcher\app\Helpers\win-x64") @("--runtime", "win-x64") -SelfContained
Publish-Project (Join-Path $launcherRoot "AetherXIV.Launcher.ClientLauncher\AetherXIV.Launcher.ClientLauncher.csproj") (Join-Path $OutputRoot "launcher\app\Helpers\win-x86") @("--runtime", "win-x86") -SelfContained

Write-Host "Publishing AetherXIV Core app..."
Publish-Project (Join-Path $rootDir "src\AetherXIV.UI.App\AetherXIV.UI.App.csproj") (Join-Path $OutputRoot "core\app") @("--runtime", $LauncherRid) -SelfContained

Write-Host "Publishing managed Umbra payload..."
Publish-Project (Join-Path $launcherRoot "Umbra\Aether.Umbra.Framework\Aether.Umbra.Framework.csproj") (Join-Path $OutputRoot "launcher\app\Umbra\Framework\Managed") @("--runtime", "win-x86") -SelfContained

$msbuild = Resolve-MSBuild
if ($msbuild) {
    Write-Host "Building native injector and Umbra bootstrap with MSBuild..."
    $nativeRoot = Join-Path $OutputRoot "native"
    $nativeInjectorOutput = Join-Path $nativeRoot "injector"
    $bootstrapOutput = Join-Path $nativeRoot "umbra-bootstrap"
    New-Item -ItemType Directory -Force -Path $nativeInjectorOutput, $bootstrapOutput | Out-Null

    & $msbuild (Join-Path $launcherRoot "AetherXIV.Launcher.NativeInjector\AetherXIV.Launcher.NativeInjector.vcxproj") /p:Configuration=$Configuration /p:Platform=Win32 /m:1 "/p:OutDir=$nativeInjectorOutput\" /p:TargetName=Umbra.NativeInjector.x86
    & $msbuild (Join-Path $launcherRoot "Umbra\Aether.Umbra.Bootstrap\Aether.Umbra.Bootstrap.vcxproj") /p:Configuration=$Configuration /p:Platform=Win32 /m:1 "/p:OutDir=$bootstrapOutput\" /p:TargetName=Aether.Umbra.Bootstrap.x86

    Copy-NativeUmbraPayloads `
        (Join-Path $nativeInjectorOutput "Umbra.NativeInjector.x86.exe") `
        (Join-Path $bootstrapOutput "Aether.Umbra.Bootstrap.x86.dll")
    Remove-Item -Recurse -Force $nativeRoot
}
else {
    Write-Host "Visual Studio C++ MSBuild not found; building native Windows payloads with MinGW..."
    Build-NativeUmbraWithMinGw
}

& $python @pythonPrefixArgs (Join-Path $rootDir "tools\Universal\create-direct-core-database-package.py") `
    --repo-root $rootDir `
    --output-dir (Join-Path $OutputRoot "Database")
if ($LASTEXITCODE -ne 0) { throw "Database package creation failed." }

if ($Configuration -eq "Release") {
    Get-ChildItem -Path $OutputRoot -Recurse -File -Filter *.pdb | Remove-Item -Force
}
Get-ChildItem -Path $OutputRoot -Recurse -File -Filter .DS_Store | Remove-Item -Force
if (Test-Path $releaseWorkRoot) {
    Remove-Item -Recurse -Force $releaseWorkRoot
}
$releaseWorkParent = Split-Path $releaseWorkRoot -Parent
if ((Test-Path $releaseWorkParent) -and -not (Get-ChildItem -Force $releaseWorkParent)) { Remove-Item -Force $releaseWorkParent }
$workRoot = Split-Path $releaseWorkParent -Parent
if ((Test-Path $workRoot) -and -not (Get-ChildItem -Force $workRoot)) { Remove-Item -Force $workRoot }
$forbiddenReleaseFiles = @(Get-ChildItem -Path $OutputRoot -Recurse -File | Where-Object {
    $_.Name -match '(?i)(\.Tests?\.|AetherXIV\.(Map|World|Lobby)\.Host)'
})
if ($forbiddenReleaseFiles.Count -gt 0) {
    throw "Release contains test or superseded server files: $($forbiddenReleaseFiles.FullName -join ', ')"
}

$requiredReleaseFiles = @(
    "servers\map\AetherXIV.Core.Map.exe",
    "servers\world\AetherXIV.Core.World.exe",
    "servers\lobby\AetherXIV.Core.Lobby.exe",
    "servers\launcher-services\AetherXIV.Launcher.Host.exe",
    "core\app\AetherXIV.Core.App.exe",
    "launcher\app\AetherXIV.Launcher.App.exe",
    "servers\map\scripts\player.lua",
    "servers\map\staticactors.bin",
    "servers\map\scripts.manifest.json",
    "servers\map\navmesh\wil0Field01.snb",
    "servers\map\navmesh\SHARPNAV_LICENSE",
    "Database\ffxiv_server.sql",
    "Database\setup.ps1",
    "launcher\app\Helpers\win-x64\Umbra.NativeInjector.x86.exe",
    "launcher\app\Helpers\win-x86\Umbra.NativeInjector.x86.exe",
    "launcher\app\Umbra\Framework\Aether.Umbra.Bootstrap.x86.dll"
)
foreach ($relativePath in $requiredReleaseFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $OutputRoot $relativePath) -PathType Leaf)) {
        throw "Windows release is missing required file: $relativePath"
    }
}

Write-Host "AetherXIV Windows build complete."
Write-Host "Output: $OutputRoot"
