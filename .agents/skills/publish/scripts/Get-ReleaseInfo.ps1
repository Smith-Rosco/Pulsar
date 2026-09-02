# Get-ReleaseInfo.ps1 - 输出仓库根、当前版本、最近 tag、自 tag 以来的提交（供版本决策）
# 用法: pwsh ./scripts/Get-ReleaseInfo.ps1
param()
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
. (Join-Path $PSScriptRoot 'Pulsar.Publish.Common.ps1')

$repo = Get-RepoRoot
$csproj = Get-CsprojPath $repo
$version = Get-ProjectVersion $csproj
Write-Output "Repository: $repo"
Write-Output "Current version: $version"

$lastTag = (git tag --sort=-creatordate | Select-Object -First 1).Trim()
if ([string]::IsNullOrWhiteSpace($lastTag)) {
    Write-Output "No tags yet. Recent commits (last 20):"
    git log --no-merges --pretty=format:%s -20
} else {
    Write-Output "Last tag: $lastTag"
    Write-Output "--- commits since $lastTag ---"
    git log --no-merges --pretty=format:%s "$lastTag..HEAD"
}
