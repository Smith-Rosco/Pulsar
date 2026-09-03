# Set-ProjectVersion.ps1 - 修改 csproj 的 <Version> 并结构化校验（防降级）
# 用法: pwsh ./scripts/Set-ProjectVersion.ps1 -Version 1.9.0
# 降级需显式加 -AllowDowngrade
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [switch]$AllowDowngrade
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
. (Join-Path $PSScriptRoot 'Pulsar.Publish.Common.ps1')

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be major.minor.patch: $Version"
}

$repo = Get-RepoRoot
$csproj = Get-CsprojPath $repo
$current = Get-ProjectVersion $csproj
Write-Output "Current version: $current"

if (-not $AllowDowngrade) {
    $cur = [version]$current
    $new = [version]$Version
    if ($new -lt $cur) {
        throw "Version $Version is lower than current $current; downgrade requires -AllowDowngrade"
    }
    if ($new -eq $cur) {
        throw "Version $Version equals current version; same-version republish is not allowed. Use local-artifact mode (no version change) or pick a higher version."
    }
}

Set-ProjectVersion -CsprojPath $csproj -Version $Version
Write-Output "Updated to: $(Get-ProjectVersion $csproj)"
