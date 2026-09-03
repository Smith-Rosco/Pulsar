# Pack-Zips.ps1 - 将发布产物打包为两个 ZIP 并校验（PK 魔数 + 列表）
# 用法: pwsh ./scripts/Pack-Zips.ps1 -Version 1.9.0
param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
. (Join-Path $PSScriptRoot 'Pulsar.Publish.Common.ps1')

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be major.minor.patch: $Version"
}

$repo = Get-RepoRoot
$paths = Get-PublishPaths -Repo $repo -Version $Version

$pairs = @(
    @{ Dir = $paths.FullDir; Zip = $paths.ZipFull },
    @{ Dir = $paths.PortableDir; Zip = $paths.ZipPortable }
)

foreach ($pair in $pairs) {
    Compress-ZipWithFallback -Dir $pair.Dir -ZipPath $pair.Zip
}

Assert-Zip -ZipPath $paths.ZipFull
Assert-Zip -ZipPath $paths.ZipPortable
Write-Output "Pack OK."
Write-Output "ZIP full:     $($paths.ZipFull)"
Write-Output "ZIP portable: $($paths.ZipPortable)"
