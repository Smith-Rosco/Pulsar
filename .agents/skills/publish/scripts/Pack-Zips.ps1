# Pack-Zips.ps1 - 将发布产物打包为两个 ZIP 并校验（PK 魔数 + 列表）
# 用法: pwsh ./scripts/Pack-Zips.ps1 -Version 1.9.1 [-Build 2]
# -Build 与 Build-Publish.ps1 一致：使用 x.y.z.n 的产物目录与 zip 命名。
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [int]$Build = 0,
    # 与 Build-Publish.ps1 -WithInstaller 配对：额外产出 Standalone zip + SHA256SUMS.txt
    #（覆盖 Setup.exe，若存在）。默认不产，本地终态只有两个 ZIP。
    [switch]$WithInstaller,
    # 打包成功后默认删除 publish\v<ver>\ 产物目录（~96M，内容已进 ZIP）。
    # 冒烟测试/后续调试需要目录时加 -KeepPublishDirs。
    [switch]$KeepPublishDirs
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
$effective = Get-BuildVersion -Version $Version -Build $Build
$paths = Get-PublishPaths -Repo $repo -Version $effective

if (-not (Test-Path -LiteralPath $paths.FullDir -PathType Container)) {
    throw "Publish output not found: $($paths.FullDir). Run Build-Publish.ps1 first."
}

$pairs = @(
    @{ Dir = $paths.FullDir; Zip = $paths.ZipFull },
    @{ Dir = $paths.PortableDir; Zip = $paths.ZipPortable }
)

foreach ($pair in $pairs) {
    Compress-ZipWithFallback -Dir $pair.Dir -ZipPath $pair.Zip
}

Assert-Zip -ZipPath $paths.ZipFull
Assert-Zip -ZipPath $paths.ZipPortable
Write-Output "Pack OK."
Write-Output "ZIP full:     $($paths.ZipFull)"
Write-Output "ZIP portable: $($paths.ZipPortable)"

# installer (ADR-026)：Standalone zip + SHA256SUMS 仅在 -WithInstaller 时产出
#（GitHub Release 资产由 CI 构建，本地默认不重复）；stage 中转目录用后即删。
$standaloneStage = Join-Path $repo 'artifacts\publish\standalone-stage'
if ($WithInstaller) {
    if (Test-Path -LiteralPath $standaloneStage) { Remove-Item -LiteralPath $standaloneStage -Recurse -Force }
    New-Item -ItemType Directory -Path $standaloneStage -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $paths.FullDir 'Pulsar.exe') -Destination $standaloneStage -Force
    Compress-ZipWithFallback -Dir $standaloneStage -ZipPath $paths.ZipStandalone
    Assert-Zip -ZipPath $paths.ZipStandalone
    Remove-Item -LiteralPath $standaloneStage -Recurse -Force
    Write-Output "ZIP standalone: $($paths.ZipStandalone)"

    # SHA256SUMS：覆盖 Standalone zip + Setup.exe（若存在）
    $manifestFiles = @($paths.ZipStandalone)
    if (Test-Path -LiteralPath $paths.SetupExe) { $manifestFiles += $paths.SetupExe }
    Write-Sha256Manifest -OutputPath $paths.Sha256Sums -Files $manifestFiles
} elseif (Test-Path -LiteralPath $standaloneStage) {
    Remove-Item -LiteralPath $standaloneStage -Recurse -Force
}

# 本地终态收敛：默认删除 publish\v<ver>\ 产物目录（内容已进 ZIP 且已校验）。
if (-not $KeepPublishDirs) {
    if (Test-Path -LiteralPath $paths.PublishRoot) {
        Remove-Item -LiteralPath $paths.PublishRoot -Recurse -Force
        Write-Output "Cleaned publish dirs: $($paths.PublishRoot) (use -KeepPublishDirs to keep)"
    }
}
