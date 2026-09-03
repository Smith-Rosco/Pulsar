# Watch-Release.ps1 - 等待 release.yml CI 完成，验证 Release 资产齐全，导出 body 供核对
# 用法: pwsh ./scripts/Watch-Release.ps1 -Version 1.9.0
param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
. (Join-Path $PSScriptRoot 'Pulsar.Publish.Common.ps1')

$uploadDir = Join-Path $env:TEMP 'pulsar-upload'
New-Item -ItemType Directory -Path $uploadDir -Force | Out-Null

$ghRepo = Get-GitHubRepo

# JSON 一律用 cmd 重定向落盘（PS 管道会经 GBK 重解码损坏 UTF-8 中文）
$runJsonFile = Join-Path $uploadDir "run_list_$Version.json"
cmd /c "gh run list --workflow=release.yml --limit 1 --json databaseId,headBranch,status > `"$runJsonFile`""
$runData = Get-Content -LiteralPath $runJsonFile -Raw | ConvertFrom-Json
if (-not $runData -or -not $runData[0].databaseId) {
    throw "No release.yml run found; did you push the v$Version tag?"
}
$runId = $runData[0].databaseId
Write-Output "Watching CI run $runId ..."
gh run watch $runId --exit-status
if ($LASTEXITCODE -ne 0) { throw "CI run $runId failed" }

$relJsonFile = Join-Path $uploadDir "release_$Version.json"
cmd /c "gh release view v$Version --json tagName,isDraft,assets > `"$relJsonFile`""
if (-not (Test-Path -LiteralPath $relJsonFile)) { throw "gh release view failed" }
Write-Output "--- Release JSON: $relJsonFile (Read to verify isDraft=false + assets) ---"

# 导出 body 到文件（cmd 直接重定向原始字节，避免 PS 管道经 GBK 重解码乱码），
# 供 Read 工具核对中文 notes。期望首字节 E6 96 B0（新）。
$bodyFile = Join-Path $uploadDir "body_check_$Version.md"
cmd /c "gh api repos/$ghRepo/releases/tags/v$Version --jq .body > `"$bodyFile`""
if (-not (Test-Path -LiteralPath $bodyFile)) { throw "Failed to export release body: $bodyFile" }
Write-Output "Release body exported: $bodyFile (Read this file to verify Chinese notes)"
