#requires -Version 5.1
<#
.SYNOPSIS
  Pulsar dev helper: build / test / commit, with automatic repair of Windows
  environment variables that sandboxed shells (e.g. WorkBuddy bash) strip away.

.DESCRIPTION
  Some hosts spawn shells without standard Windows env vars (APPDATA,
  LOCALAPPDATA, ProgramData, ProgramFiles, ProgramFiles(x86),
  CommonProgramFiles, CommonProgramFiles(x86)). .NET 8 NuGet reads them via
  Environment.GetFolderPath and crashes with
  "Value cannot be null. (Parameter 'path1')" when they are missing.
  This script patches ONLY the missing vars, process-scoped, never
  overwriting existing values, then runs the requested command.

  Commands:
    build   dotnet build Pulsar\Pulsar.sln        (extra args passed through)
    test    dotnet test Pulsar.Tests.csproj       (extra args passed through)
    commit  git add (-u by default, -A with -All) + git commit -Message
    all     build, then full test

.EXAMPLE
  .\scripts\dev.ps1 build
  .\scripts\dev.ps1 build --no-incremental
  .\scripts\dev.ps1 test
  .\scripts\dev.ps1 test --filter "FullyQualifiedName~HotkeyService"
  .\scripts\dev.ps1 commit -Message "fix: some bug"
  .\scripts\dev.ps1 commit -Message "feat: thing" -All
  .\scripts\dev.ps1 all

.NOTES
  - PowerShell 5.1 compatible (no '&&', no ternary, no PS7-only syntax).
  - ASCII-only on purpose: PS 5.1 reads BOM-less UTF-8 .ps1 files as ANSI.
  - Exit code mirrors the first failing dotnet/git step; 0 on success.
  - Passthrough caveat: short flags that are a prefix of a declared parameter
    (e.g. -t, -r) must use their long form. Common short flags like
    -c / -o / -v are safe.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('build', 'test', 'commit', 'all')]
    [string]$Task,

    # commit only: commit message (required for 'commit')
    [string]$Message,

    # commit only: stage everything including untracked (git add -A)
    [switch]$All,

    # extra args passed through to dotnet for build/test
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Rest
)

# NOTE: $ErrorActionPreference intentionally left at default. With 'Stop',
# PowerShell 5.1 turns redirected native stderr lines into terminating errors.

if ([string]::IsNullOrEmpty($PSScriptRoot)) {
    throw "[dev] must run as a script file (powershell -File scripts\dev.ps1 ...)"
}
if ($null -eq $Rest) { $Rest = @() }

$repoRoot    = Split-Path -Parent $PSScriptRoot
$sln         = Join-Path $repoRoot 'Pulsar\Pulsar.sln'
$testProject = Join-Path $repoRoot 'Pulsar\Pulsar.Tests\Pulsar.Tests.csproj'
if (-not (Test-Path -LiteralPath $sln))         { throw "[dev] solution not found: $sln" }
if (-not (Test-Path -LiteralPath $testProject)) { throw "[dev] test project not found: $testProject" }

# ---------------------------------------------------------------------------
# Env repair: patch ONLY missing vars, process scope, never overwrite.
# Variable list from the dotnet-nuget-env-fix investigation (authoritative).
# ---------------------------------------------------------------------------
function Repair-EnvVar {
    param([string]$Name, [string]$Fallback)
    $current = [Environment]::GetEnvironmentVariable($Name)
    if (-not [string]::IsNullOrEmpty($current)) { return }
    [Environment]::SetEnvironmentVariable($Name, $Fallback, 'Process')
    Write-Host ("[dev] patched missing env var {0} -> {1}" -f $Name, $Fallback)
}

function Get-UserProfileSafe {
    if (-not [string]::IsNullOrEmpty($env:USERPROFILE)) { return $env:USERPROFILE }
    if (-not [string]::IsNullOrEmpty($env:HOMEDRIVE) -and -not [string]::IsNullOrEmpty($env:HOMEPATH)) {
        $homePath = $env:HOMEPATH
        if (-not $homePath.StartsWith('\')) { $homePath = '\' + $homePath }
        return ($env:HOMEDRIVE.TrimEnd('\') + $homePath)
    }
    if (-not [string]::IsNullOrEmpty($env:USERNAME)) { return (Join-Path 'C:\Users' $env:USERNAME) }
    throw "[dev] cannot determine user profile dir (USERPROFILE/HOMEDRIVE/HOMEPATH/USERNAME all missing)"
}

function Repair-DotNetEnv {
    $systemDrive = 'C:'
    if (-not [string]::IsNullOrEmpty($env:SystemDrive)) { $systemDrive = $env:SystemDrive.TrimEnd('\') }

    $userProfile = Get-UserProfileSafe
    Repair-EnvVar -Name 'USERPROFILE'  -Fallback $userProfile
    Repair-EnvVar -Name 'APPDATA'      -Fallback (Join-Path $userProfile 'AppData\Roaming')
    Repair-EnvVar -Name 'LOCALAPPDATA' -Fallback (Join-Path $userProfile 'AppData\Local')

    Repair-EnvVar -Name 'ProgramData' -Fallback ($systemDrive + '\ProgramData')
    Repair-EnvVar -Name 'SystemRoot'  -Fallback ($systemDrive + '\Windows')
    Repair-EnvVar -Name 'windir'      -Fallback ($systemDrive + '\Windows')

    $pf = [Environment]::GetEnvironmentVariable('ProgramFiles')
    if ([string]::IsNullOrEmpty($pf)) {
        $pf = [Environment]::GetEnvironmentVariable('ProgramW6432')
        if ([string]::IsNullOrEmpty($pf)) { $pf = $systemDrive + '\Program Files' }
        Repair-EnvVar -Name 'ProgramFiles' -Fallback $pf
    }

    # ProgramFiles(x86): derive from the (possibly just patched) ProgramFiles.
    $pf86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
    if ([string]::IsNullOrEmpty($pf86)) {
        $base = [Environment]::GetEnvironmentVariable('ProgramFiles')
        if ($base.EndsWith(' (x86)')) { $base = $base.Substring(0, $base.Length - 6) }
        Repair-EnvVar -Name 'ProgramFiles(x86)' -Fallback ($base + ' (x86)')
    }

    $cpf = [Environment]::GetEnvironmentVariable('CommonProgramFiles')
    if ([string]::IsNullOrEmpty($cpf)) {
        Repair-EnvVar -Name 'CommonProgramFiles' -Fallback (Join-Path ([Environment]::GetEnvironmentVariable('ProgramFiles')) 'Common Files')
    }
    $cpf86 = [Environment]::GetEnvironmentVariable('CommonProgramFiles(x86)')
    if ([string]::IsNullOrEmpty($cpf86)) {
        Repair-EnvVar -Name 'CommonProgramFiles(x86)' -Fallback (Join-Path ([Environment]::GetEnvironmentVariable('ProgramFiles(x86)')) 'Common Files')
    }
}

function Resolve-DotNet {
    $cmd = Get-Command 'dotnet' -ErrorAction SilentlyContinue
    if ($null -ne $cmd) { return $cmd.Source }
    $fallback = Join-Path ([Environment]::GetEnvironmentVariable('ProgramFiles')) 'dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $fallback) {
        Write-Host "[dev] dotnet not on PATH; using fallback: $fallback"
        return $fallback
    }
    throw "[dev] dotnet not found on PATH and no fallback at: $fallback"
}

function Get-StepExitCode {
    # 127 = native command never ran (spawn blocked/failed): $LASTEXITCODE is null/empty.
    if ($null -eq $LASTEXITCODE -or [string]::IsNullOrEmpty([string]$LASTEXITCODE)) { return 127 }
    return [int]$LASTEXITCODE
}

function Invoke-Step {
    param([string]$Title, [scriptblock]$Action)
    Write-Host ''
    Write-Host ("[dev] >> {0}" -f $Title)
    & $Action
    $code = Get-StepExitCode
    if ($code -ne 0) {
        Write-Host ("[dev] FAILED (exit code {0}): {1}" -f $code, $Title) -ForegroundColor Red
        exit $code
    }
}

function Invoke-Commit {
    if ([string]::IsNullOrWhiteSpace($Message)) {
        throw "[dev] 'commit' requires -Message. Example: .\scripts\dev.ps1 commit -Message 'fix: something'"
    }
    if ($All) {
        Write-Host '[dev] staging ALL changes (git add -A, includes untracked)'
        git -C $repoRoot add -A
    }
    else {
        Write-Host '[dev] staging tracked changes only (git add -u); pass -All to include untracked files'
        git -C $repoRoot add -u
    }
    $code = Get-StepExitCode
    if ($code -ne 0) {
        Write-Host '[dev] git add FAILED' -ForegroundColor Red
        exit $code
    }
    git -C $repoRoot commit -m $Message
    $code = Get-StepExitCode
    if ($code -ne 0) {
        Write-Host '[dev] git commit FAILED' -ForegroundColor Red
        exit $code
    }
    Write-Host '[dev] commit created.'
}

Write-Host ("[dev] Pulsar dev helper | task: {0} | repo: {1}" -f $Task, $repoRoot)
Repair-DotNetEnv
$dotnet = Resolve-DotNet
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

switch ($Task) {
    'build' {
        Invoke-Step -Title ('dotnet build ' + $sln) -Action { & $dotnet build $sln @Rest }
    }
    'test' {
        Invoke-Step -Title ('dotnet test ' + $testProject) -Action { & $dotnet test $testProject @Rest }
    }
    'commit' {
        Invoke-Commit
    }
    'all' {
        Invoke-Step -Title ('dotnet build ' + $sln) -Action { & $dotnet build $sln @Rest }
        Invoke-Step -Title ('dotnet test ' + $testProject) -Action { & $dotnet test $testProject }
    }
}

$stopwatch.Stop()
Write-Host ''
Write-Host ('[dev] done in {0:n1}s' -f $stopwatch.Elapsed.TotalSeconds)
exit 0
