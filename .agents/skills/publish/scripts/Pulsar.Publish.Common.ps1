# Pulsar.Publish.Common.ps1 - 共享函数库（被各阶段脚本 dot-source）
# 所有路径基于 git rev-parse --show-toplevel，不依赖调用目录。

function Get-RepoRoot {
    return (git rev-parse --show-toplevel).Trim()
}

function Get-CsprojPath {
    param([string]$Repo)
    return Join-Path $Repo 'Pulsar\Pulsar\Pulsar.csproj'
}

function Get-ProjectVersion {
    param([string]$CsprojPath)
    $m = Select-String -Path $CsprojPath -Pattern '<Version>([^<]+)</Version>'
    if ($m.Count -ne 1) { throw "Expected exactly one <Version> in $CsprojPath" }
    return $m.Matches[0].Groups[1].Value
}

function Set-ProjectVersion {
    param(
        [string]$CsprojPath,
        [string]$Version
    )
    $content = [System.IO.File]::ReadAllText($CsprojPath)
    $content = $content -replace '<Version>([^<]+)</Version>', "<Version>$Version</Version>"
    Write-Utf8NoBom -Path $CsprojPath -Content $content
    $actual = Get-ProjectVersion $CsprojPath
    if ($actual -ne $Version) {
        throw "Version mismatch: csproj=$actual expected=$Version"
    }
}

function Get-PublishPaths {
    param(
        [string]$Repo,
        [string]$Version
    )
    $publishRoot = Join-Path $Repo "Artifacts\publish\v$Version"
    return @{
        Repo          = $Repo
        PublishRoot   = $publishRoot
        FullDir       = Join-Path $publishRoot 'full'
        PortableDir   = Join-Path $publishRoot 'portable'
        ZipFull       = Join-Path $Repo "Artifacts\Pulsar-$Version-full.zip"
        ZipPortable   = Join-Path $Repo "Artifacts\Pulsar-$Version-portable.zip"
    }
}

function Write-Utf8NoBom {
    param(
        [string]$Path,
        [string]$Content
    )
    $enc = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $enc)
}

function Assert-Publish {
    param(
        [string]$Dir,
        [bool]$RequireCor3
    )
    foreach ($name in @('Pulsar.exe', 'Pulsar.pdb')) {
        if (-not (Test-Path -LiteralPath (Join-Path $Dir $name) -PathType Leaf)) {
            throw "Missing publish artifact: $(Join-Path $Dir $name)"
        }
    }
    $assetsDir = Join-Path $Dir 'Assets'
    if (-not (Test-Path -LiteralPath $assetsDir -PathType Container)) {
        throw "Missing publish artifact directory: $assetsDir"
    }
    $cor3 = @(Get-ChildItem -LiteralPath $Dir -Filter '*_cor3.dll' -File)
    if ($RequireCor3 -and $cor3.Count -eq 0) {
        throw "No *_cor3.dll found in $Dir (full)"
    }
    if (-not $RequireCor3 -and $cor3.Count -gt 0) {
        throw "portable must not contain *_cor3.dll: $Dir"
    }
    $assetCount = @(Get-ChildItem -LiteralPath $assetsDir -Recurse -File).Count
    $exe = Get-Item -LiteralPath (Join-Path $Dir 'Pulsar.exe')
    Write-Output "Valid: $Dir exe=$($exe.Length) bytes, cor3=$($cor3.Count), assets=$assetCount"
}

function Assert-Zip {
    param([string]$ZipPath)
    if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
        throw "ZIP was not created: $ZipPath"
    }
    $bytes = [System.IO.File]::ReadAllBytes($ZipPath)
    if ($bytes.Length -lt 2 -or $bytes[0] -ne 0x50 -or $bytes[1] -ne 0x4B) {
        throw "ZIP magic is not PK: $ZipPath"
    }
    Write-Output ('ZIP magic: {0:X2}{1:X2} size={2} path={3}' -f $bytes[0], $bytes[1], $bytes.Length, $ZipPath)
    & 'C:\Windows\System32\tar.exe' -tf $ZipPath
    if ($LASTEXITCODE -ne 0) { throw "ZIP listing failed with exit code $LASTEXITCODE" }
}
