# Edit-ReleaseNotes.ps1 - 修正 GitHub Release body 为完整 release notes
# 用法: pwsh ./scripts/Edit-ReleaseNotes.ps1 -Version 1.9.0 -NotesFile <完整release notes路径>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string]$NotesFile
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
. (Join-Path $PSScriptRoot 'Pulsar.Publish.Common.ps1')

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be major.minor.patch: $Version"
}
if (-not (Test-Path -LiteralPath $NotesFile -PathType Leaf)) {
    throw "Notes file not found: $NotesFile"
}

# 重写 notes 为无 BOM 的 UTF-8，避免 gh 读取异常
$notesContent = [System.IO.File]::ReadAllText($NotesFile)
$cleanNotes = Join-Path $env:TEMP "pulsar-notes-$Version-clean.md"
Write-Utf8NoBom -Path $cleanNotes -Content $notesContent

gh release edit "v$Version" --notes-file $cleanNotes
if ($LASTEXITCODE -ne 0) { throw "gh release edit failed" }

# 导出核对（cmd 直接重定向，避免 PS 管道 GBK 重解码）
$uploadDir = Join-Path $env:TEMP 'pulsar-upload'
New-Item -ItemType Directory -Path $uploadDir -Force | Out-Null
$ghRepo = Get-GitHubRepo
$bodyFile = Join-Path $uploadDir "body_check_$Version.md"
cmd /c "gh api repos/$ghRepo/releases/tags/v$Version --jq .body > `"$bodyFile`""
Write-Output "Release body updated. Verify: $bodyFile"
