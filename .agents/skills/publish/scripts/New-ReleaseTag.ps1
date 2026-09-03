# New-ReleaseTag.ps1 - release 模式：commit 版本号 + 创建带完整 notes 的 tag + 校验 tag message
# 用法: pwsh ./scripts/New-ReleaseTag.ps1 -Version 1.9.0 -NotesFile <完整release notes路径>
# 注意：NotesFile 必须包含用户确认过的完整 release notes（可含 ### 章节标题）。
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

$repo = Get-RepoRoot

# 1) 版本相关文件（csproj + CHANGELOG.md）若有改动则 commit；无改动跳过
$csproj = Get-CsprojPath $repo
$releaseFiles = @("Pulsar/Pulsar/Pulsar.csproj", "CHANGELOG.md")
$changed = @($releaseFiles | Where-Object { git diff --quiet -- $_; $LASTEXITCODE -ne 0 })
if ($changed.Count -gt 0) {
    git add -- @releaseFiles
    if ($LASTEXITCODE -ne 0) { throw "git add failed" }
    git commit -m "chore(release): bump version to $Version"
    if ($LASTEXITCODE -ne 0) { throw "version commit failed" }
    Write-Output "Committed version bump ($($changed -join ', '))."
} else {
    Write-Output "No version/CHANGELOG change; commit skipped."
}

# 2) 重写 notes 为无 BOM 的 UTF-8（PS7 [Text.Encoding]::UTF8 带 BOM，会污染 tag message 首行）
$notesContent = [System.IO.File]::ReadAllText($NotesFile)
$cleanNotes = Join-Path $env:TEMP "pulsar-notes-$Version.md"
Write-Utf8NoBom -Path $cleanNotes -Content $notesContent

# 3) tag 已存在则停止（不覆盖）
if (git tag --list "v$Version") {
    throw "Tag v$Version already exists; stop and ask user, do not overwrite."
}

# 4) 创建 annotated tag。必须覆盖 commentChar：git 默认 '#' 会把 `### 新功能`
#    等章节标题当注释剥离，导致 Release body 只剩正文 bullet。
git -c core.commentChar=§ tag -a "v$Version" -F $cleanNotes
if ($LASTEXITCODE -ne 0) { throw "tag creation failed" }

# 5) 校验 tag message：cmd 直接重定向原始字节（PS 管道会经 GBK 重解码乱码），
#    并核对首三字节为 23 23 23（###）而非 EF BB BF（BOM）。
$msgFile = Join-Path $env:TEMP "pulsar-tagmsg-$Version.txt"
cmd /c "git for-each-ref refs/tags/v$Version --format=%%(contents) > `"$msgFile`""
$bytes = [System.IO.File]::ReadAllBytes($msgFile)
if ($bytes.Length -lt 3) { throw "Tag message is empty: $msgFile" }
if ($bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
    throw "Tag message starts with BOM; notes encoding issue. Inspect: $msgFile"
}
if (-not ($bytes[0] -eq 0x23 -and $bytes[1] -eq 0x23 -and $bytes[2] -eq 0x23)) {
    Write-Warning "Tag message does not start with '###' - verify notes headers were preserved: $msgFile"
}
Write-Output "Tag v$Version created. Raw message: $msgFile"
