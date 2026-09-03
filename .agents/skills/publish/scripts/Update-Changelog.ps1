# Update-Changelog.ps1 - 将 CHANGELOG.md 的 [Unreleased] 段固化为正式版本条目
# 用法: pwsh ./scripts/Update-Changelog.ps1 -Version 1.9.2
# 行为: `## [Unreleased]` 标题改写为 `## [X.Y.Z] - <今天日期>`，并在其上方插入
#       一个全新的空 [Unreleased] 段。条目内容不做改写——若段内仍是"暂无/TODO"
#       占位符，请先由 AI 根据提交记录补全后再运行本脚本。
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
$changelogPath = Join-Path $repo 'CHANGELOG.md'
if (-not (Test-Path -LiteralPath $changelogPath -PathType Leaf)) {
    throw "CHANGELOG.md not found: $changelogPath"
}

$lines = [System.IO.File]::ReadAllLines($changelogPath)
$unrelIdx = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^## \[Unreleased\]') { $unrelIdx = $i; break }
}
if ($unrelIdx -lt 0) { throw "No '## [Unreleased]' section found in CHANGELOG.md" }

# 已存在同版本条目则停止，不覆盖
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match ("^## \[$Version\]")) {
        throw "CHANGELOG.md already has a [$Version] entry; stop and review manually."
    }
}

# 守卫：Unreleased 段必须至少有一条真实条目。某分类下的"暂无/TODO"是合法的
# （表示该分类无变更），但全部条目都是占位符（或没有任何条目）说明内容未补全。
$sectionEnd = $lines.Count
for ($i = $unrelIdx + 1; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match '^## ') { $sectionEnd = $i; break }
}
$sectionText = ($lines[($unrelIdx + 1)..($sectionEnd - 1)] -join "`n")
$bullets = @($sectionText -split "`r?`n" | Where-Object { $_ -match '^\s*-\s+\S' })
$allPlaceholder = ($bullets.Count -eq 0) -or (@($bullets | Where-Object { $_ -notmatch '^\s*-\s*(暂无|TODO|TBD)' }).Count -eq 0)
if ($allPlaceholder) {
    throw "[Unreleased] section has no real entries (only placeholders or empty). Fill in user-perceivable changes from commits before running this script."
}

$date = (Get-Date).ToString('yyyy-MM-dd')
$newHeader = "## [$Version] - $date"
$emptyUnreleased = @(
    '## [Unreleased]',
    '',
    '### Added',
    '- 暂无',
    '',
    '### Changed',
    '- 暂无',
    '',
    '### Fixed',
    '- 暂无',
    ''
)

$updated = $lines[0..($unrelIdx - 1)] + $emptyUnreleased + $newHeader + $lines[($unrelIdx + 1)..($lines.Count - 1)]
Write-Utf8NoBom -Path $changelogPath -Content (($updated -join "`n") + "`n")
Write-Output "CHANGELOG.md updated: [$Version] - $date (content preserved from [Unreleased])"
Write-Output "Review the result and include CHANGELOG.md in the release commit."
