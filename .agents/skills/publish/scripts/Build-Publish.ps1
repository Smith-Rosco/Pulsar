# Build-Publish.ps1 - 构建 full + portable 两个发布产物并校验
# 用法: pwsh ./scripts/Build-Publish.ps1 -Version 1.9.1 [-Build 2]
# 构建号（-Build）：仅本地构建使用，第 4 位版本号 x.y.z.n。csproj 不被修改，
# 版本通过 -p:Version/FileVersion/AssemblyVersion 在 publish 时覆盖；
# 产物目录与 zip 命名使用 x.y.z.n。省略 -Build 时行为与 x.y.z 完全一致。
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [int]$Build = 0
)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
. (Join-Path $PSScriptRoot 'Pulsar.Publish.Common.ps1')

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be major.minor.patch: $Version"
}
if ($Build -lt 0 -or $Build -gt 65535) {
    throw "Build must be in 0..65535: $Build"
}

$repo = Get-RepoRoot
$csproj = Get-CsprojPath $repo
$effective = Get-BuildVersion -Version $Version -Build $Build
$paths = Get-PublishPaths -Repo $repo -Version $effective

# 清理本次版本目录与同名 ZIP（不删其他版本；带构建号时各构建互不覆盖）
if (Test-Path -LiteralPath $paths.PublishRoot) {
    Remove-Item -LiteralPath $paths.PublishRoot -Recurse -Force
}
foreach ($zip in @($paths.ZipFull, $paths.ZipPortable)) {
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
}
New-Item -ItemType Directory -Path $paths.FullDir -Force | Out-Null
New-Item -ItemType Directory -Path $paths.PortableDir -Force | Out-Null

# 构建号 > 0 时在 publish 阶段覆盖版本三元组（csproj 保持 x.y.z 不动）
$verArgs = @()
if ($Build -gt 0) {
    $verArgs = @("-p:Version=$effective", "-p:FileVersion=$effective", "-p:AssemblyVersion=$effective")
}

# full：自包含单文件，含运行时
Write-Output "Publishing full -> $($paths.FullDir)"
dotnet publish $csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishReadyToRun=true `
    "-p:PublishDir=$($paths.FullDir)\" `
    @verArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (full) failed with exit code $LASTEXITCODE" }

# portable：framework-dependent 单文件，不含运行时
Write-Output "Publishing portable -> $($paths.PortableDir)"
dotnet publish $csproj `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    "-p:PublishDir=$($paths.PortableDir)\" `
    @verArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (portable) failed with exit code $LASTEXITCODE" }

Write-BuildInfo -Dir $paths.FullDir -Version $Version -Build $Build -Channel 'full'
Write-BuildInfo -Dir $paths.PortableDir -Version $Version -Build $Build -Channel 'portable'

Assert-Publish -Dir $paths.FullDir -RequireCor3 $true
Assert-Publish -Dir $paths.PortableDir -RequireCor3 $false
Write-Output "Build OK. Effective version: $effective (csproj untouched: $Version)"
