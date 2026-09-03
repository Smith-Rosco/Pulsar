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

# 取 semver 最大的 v* tag（而非 creatordate 最新）：
# creatordate 排序在回溯补打历史 tag 时会取错基线。
$lastTag = $null
$tags = @(git tag --list 'v*' | ForEach-Object { $_.Trim() } |
    Where-Object { $_ -match '^v\d+\.\d+\.\d+$' } |
    Sort-Object { [version]$_.TrimStart('v') } -Descending)
if ($tags.Count -gt 0) { $lastTag = $tags[0] }

# 本地构建号建议（方案 B）：扫描 Artifacts\publish\v{version}.* 目录与同名 zip，
# 取已用最大第 4 位 + 1。csproj 不存构建号，仅用于 Build-Publish.ps1 -Build。
$publishRoot = Join-Path $repo 'Artifacts\publish'
$maxBuild = 0
if (Test-Path -LiteralPath $publishRoot -PathType Container) {
    foreach ($dir in @(Get-ChildItem -LiteralPath $publishRoot -Directory)) {
        if ($dir.Name -match ("^v" + [regex]::Escape($version) + "\.(\d+)$")) {
            $maxBuild = [Math]::Max($maxBuild, [int]$Matches[1])
        }
    }
}
$artifactsRoot = Join-Path $repo 'Artifacts'
if (Test-Path -LiteralPath $artifactsRoot -PathType Container) {
    foreach ($zip in @(Get-ChildItem -LiteralPath $artifactsRoot -File -Filter "Pulsar-$version.*-full.zip")) {
        if ($zip.Name -match ("^Pulsar-" + [regex]::Escape($version) + "\.(\d+)-full\.zip$")) {
            $maxBuild = [Math]::Max($maxBuild, [int]$Matches[1])
        }
    }
}
Write-Output "Next local build number: $($maxBuild + 1)"
if ([string]::IsNullOrWhiteSpace($lastTag)) {
    Write-Output "No tags yet. Recent commits (last 20):"
    git log --no-merges --pretty=format:%s -20
} else {
    Write-Output "Last tag: $lastTag"
    Write-Output "--- commits since $lastTag ---"
    git log --no-merges --pretty=format:%s "$lastTag..HEAD"
}
