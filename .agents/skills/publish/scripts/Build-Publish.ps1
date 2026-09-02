# Build-Publish.ps1 - 构建 full + portable 两个发布产物并校验
# 用法: pwsh ./scripts/Build-Publish.ps1 -Version 1.9.0
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
$csproj = Get-CsprojPath $repo
$paths = Get-PublishPaths -Repo $repo -Version $Version

# 清理本次版本目录与同名 ZIP（不删其他版本）
if (Test-Path -LiteralPath $paths.PublishRoot) {
    Remove-Item -LiteralPath $paths.PublishRoot -Recurse -Force
}
foreach ($zip in @($paths.ZipFull, $paths.ZipPortable)) {
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
}
New-Item -ItemType Directory -Path $paths.FullDir -Force | Out-Null
New-Item -ItemType Directory -Path $paths.PortableDir -Force | Out-Null

# full：自包含单文件，含运行时
Write-Output "Publishing full -> $($paths.FullDir)"
dotnet publish $csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishReadyToRun=true `
    "-p:PublishDir=$($paths.FullDir)\"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (full) failed with exit code $LASTEXITCODE" }

# portable：framework-dependent 单文件，不含运行时
Write-Output "Publishing portable -> $($paths.PortableDir)"
dotnet publish $csproj `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    "-p:PublishDir=$($paths.PortableDir)\"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (portable) failed with exit code $LASTEXITCODE" }

Assert-Publish -Dir $paths.FullDir -RequireCor3 $true
Assert-Publish -Dir $paths.PortableDir -RequireCor3 $false
Write-Output "Build OK."
