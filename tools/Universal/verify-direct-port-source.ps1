param(
    [string]$RootDir = (Resolve-Path (Join-Path $PSScriptRoot "..\.."))
)

$ErrorActionPreference = "Stop"
$manifestPath = Join-Path $RootDir "Data\seeds\direct-port\source-manifest.json"
$manifest = Get-Content -Raw $manifestPath | ConvertFrom-Json
$expected = @{}

foreach ($entry in $manifest.entries) {
    $relative = $entry.portPath.Replace("/", [IO.Path]::DirectorySeparatorChar)
    $path = Join-Path $RootDir $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing direct-port source: $($entry.portPath)"
    }

    $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
    if ($actualHash -ne $entry.portSha256) {
        throw "Direct-port source hash mismatch: $($entry.portPath)"
    }

    $expected[$entry.portPath] = $true
}

foreach ($asset in $manifest.runtimeAssets) {
    $relative = $asset.portPath.Replace("/", [IO.Path]::DirectorySeparatorChar)
    $path = Join-Path $RootDir $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing direct-port runtime/data asset: $($asset.portPath)"
    }

    $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
    if ($actualHash -ne $asset.portSha256) {
        throw "Direct-port runtime/data asset hash mismatch: $($asset.portPath)"
    }
}

$roots = @(
    "src\AetherXIV.Core.Common",
    "src\AetherXIV.Core.Lobby",
    "src\AetherXIV.Core.World",
    "src\AetherXIV.Core.Map"
)
$actual = @{}
foreach ($relativeRoot in $roots) {
    $sourceRoot = Join-Path $RootDir $relativeRoot
    Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter *.cs |
        Where-Object { $_.FullName -notmatch "[\\/](bin|obj)[\\/]" } |
        ForEach-Object {
            $relative = [IO.Path]::GetRelativePath($RootDir, $_.FullName).Replace("\", "/")
            $actual[$relative] = $true
        }

    Get-ChildItem -LiteralPath $sourceRoot -File -Filter *.csproj | ForEach-Object {
        if (Select-String -LiteralPath $_.FullName -SimpleMatch "Legacy " -Quiet) {
            throw "External reference source dependency: $($_.FullName)"
        }
    }
}

foreach ($path in $actual.Keys) {
    if (-not $expected.ContainsKey($path)) {
        throw "Unmanifested direct-port source: $path"
    }
}
foreach ($path in $expected.Keys) {
    if (-not $actual.ContainsKey($path)) {
        throw "Manifest-only direct-port source: $path"
    }
}

$exact = @($manifest.entries | Where-Object disposition -eq "exact-copy").Count
$adapted = @($manifest.entries).Count - $exact
if ($manifest.entries.Count -ne 418) {
    throw "Direct-port C# source count mismatch: $($manifest.entries.Count); expected 418"
}
if ($manifest.runtimeAssets.Count -ne 86) {
    throw "Direct-port runtime/data asset count mismatch: $($manifest.runtimeAssets.Count); expected 86"
}
Write-Host "Direct-port source verified: $($manifest.entries.Count) C# files ($exact exact, $adapted adapted), $($manifest.runtimeAssets.Count) exact runtime/data assets."
