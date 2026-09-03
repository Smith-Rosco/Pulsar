# Pack-Zips.ps1 - 将发布产物打包为两个 ZIP 并校验（PK 魔数 + 列表）
# 用法: pwsh ./scripts/Pack-Zips.ps1 -Version 1.9.1 [-Build 2]
# -Build 与 Build-Publish.ps1 一致：使用 x.y.z.n 的产物目录与 zip 命名。
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [int]$Build = 0
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
. (Join-Path $PSScriptRoot 'Pulsar.Publish.Common.ps1')

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be major.minor.patch: $Version"
}
if ($Build -lt 0 -or $Build -gt 65535) {
    throw "Build must be in 0..65535: $Build"
}

$repo = Get-RepoRoot
$effective = Get-BuildVersion -Version $Version -Build $Build
$paths = Get-PublishPaths -Repo $repo -Version $effective

if (-not (Test-Path -LiteralPath $paths.FullDir -PathType Container)) {
    throw "Publish output not found: $($paths.FullDir). Run Build-Publish.ps1 first."
}

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
