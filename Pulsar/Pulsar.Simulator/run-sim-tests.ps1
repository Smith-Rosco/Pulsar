# 临时脚本：用 Simulator 预跑 Demo fixture 的三个插件动作（dry-run）
$ErrorActionPreference = 'Stop'
$sim = Join-Path $PSScriptRoot 'bin\Release\net8.0-windows\Pulsar.Simulator.exe'

function Run-Sim {
    param([string]$Title, [string]$Plugin, [string]$Action, [string]$Json)
    Write-Output "===== $Title ====="
    $out = & $sim @('-plugin', $Plugin, '-action', $Action, '-args', $Json) 2>&1 | Out-String
    Write-Output "[exit=$LASTEXITCODE len=$($out.Length)]"
    $out -split "`n" | ForEach-Object { $_.Substring(0, [Math]::Min(220, $_.Length)) } | Select-Object -First 8
}

$json1 = '{"code":"document.querySelector(''#login'').click()"}'
Run-Sim '1) bookmarklet 登录老旧系统' 'com.pulsar.bookmarklet' 'run' $json1

$json2 = '{"scriptPath":"%USERPROFILE%\\Documents\\Pulsar\\Scripts\\format-report.bas","macro":"FormatReport"}'
Run-Sim '2) vbarunner 一键跑宏' 'com.pulsar.vbarunner' 'run' $json2

$json3 = '{"field":"username","secret":"hr-portal"}'
Run-Sim '3) pki 自动填写登录' 'com.pulsar.pki' 'fill' $json3

$json4 = '{"scriptPath":"%USERPROFILE%\\Documents\\Pulsar\\Scripts\\report-pack.bas","macro":"PackReport"}'
Run-Sim '4) vbarunner 报表整理(子动作主宏)' 'com.pulsar.vbarunner' 'run' $json4
