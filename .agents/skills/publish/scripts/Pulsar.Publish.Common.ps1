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
    # 仓库惯例（与 Properties/发布产物一致）：Version = x.y.z，FileVersion / AssemblyVersion = x.y.z.0。
    # 三者必须同步更新，否则 exe 文件属性版本会停留在旧值。
    param(
        [string]$CsprojPath,
        [string]$Version
    )
    $content = [System.IO.File]::ReadAllText($CsprojPath)
    $content = $content -replace '<Version>([^<]+)</Version>', "<Version>$Version</Version>"
    $content = $content -replace '<FileVersion>([^<]+)</FileVersion>', "<FileVersion>$Version.0</FileVersion>"
    $content = $content -replace '<AssemblyVersion>([^<]+)</AssemblyVersion>', "<AssemblyVersion>$Version.0</AssemblyVersion>"
    Write-Utf8NoBom -Path $CsprojPath -Content $content
    $actual = Get-ProjectVersion $CsprojPath
    if ($actual -ne $Version) {
        throw "Version mismatch: csproj=$actual expected=$Version"
    }
}

function Get-BuildVersion {
    # 方案 B：构建号不入 csproj。csproj 永远存 x.y.z；本地构建的第 4 位只在
    # publish 时以 -p:Version/FileVersion/AssemblyVersion 覆盖，并用于产物命名。
    param(
        [Parameter(Mandatory = $true)][string]$Version,
        [int]$Build = 0
    )
    if ($Build -gt 0) { return "$Version.$Build" }
    return $Version
}

function Write-BuildInfo {
    # 本地构建的 zip 内附 build-info.txt，排障时无需看 exe 文件属性。
    param(
        [Parameter(Mandatory = $true)][string]$Dir,
        [Parameter(Mandatory = $true)][string]$Version,
        [int]$Build = 0,
        [Parameter(Mandatory = $true)][ValidateSet('full', 'portable')][string]$Channel
    )
    $commit = (git rev-parse --short HEAD).Trim()
    $lines = @(
        "Version: $Version",
        "Build: $Build",
        "Channel: $Channel",
        "Built: $((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))",
        "Commit: $commit"
    )
    Write-Utf8NoBom -Path (Join-Path $Dir 'build-info.txt') -Content (($lines -join "`r`n") + "`r`n")
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

function Get-GitHubRepo {
    # 从 origin remote 解析 owner/repo（支持 https 与 ssh URL），避免脚本硬编码仓库。
    $url = (git remote get-url origin).Trim()
    if ($url -match 'github\.com[/:]([^/]+)/([^/]+?)(?:\.git)?$') {
        return "$($Matches[1])/$($Matches[2])"
    }
    throw "Cannot parse owner/repo from origin remote: $url"
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

function Test-ZipMagic {
    param([string]$ZipPath)
    if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) { return $false }
    $bytes = [System.IO.File]::ReadAllBytes($ZipPath)
    return ($bytes.Length -ge 2 -and $bytes[0] -eq 0x50 -and $bytes[1] -eq 0x4B)
}

function Compress-ZipWithFallback {
    # 三级回退（与 SKILL.md 第 7 节排障顺序一致）：
    #   1. pwsh（模块在自己目录，默认 RemoteSigned，最可靠）
    #   2. powershell -ExecutionPolicy Bypass（5.1 绕过 PS7 模块副本的执行策略拦截）
    #   3. System32 bsdtar（Win10 1803+，无需 PowerShell 模块）
    # 注意不能用 PATH 中的 tar（Git for Windows 的 GNU tar 只会生成伪 .zip）。
    # 每级成功后用 PK 魔数校验，失败删除残留后降级。
    param(
        [Parameter(Mandatory = $true)][string]$Dir,
        [Parameter(Mandatory = $true)][string]$ZipPath
    )
    if (-not (Test-Path -LiteralPath $Dir -PathType Container)) {
        throw "Source directory not found: $Dir"
    }

    $archiveCmd = "Compress-Archive -Path '$Dir\*' -DestinationPath '$ZipPath' -CompressionLevel Optimal -Force"
    $attempts = @(
        @{ Cmd = 'pwsh';        Args = @('-NoProfile', '-Command', $archiveCmd) },
        @{ Cmd = 'powershell';  Args = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', $archiveCmd) }
    )
    foreach ($attempt in $attempts) {
        if (Test-ZipMagic $ZipPath) { Remove-Item -LiteralPath $ZipPath -Force }
        & $attempt.Cmd @($attempt.Args)
        if ($LASTEXITCODE -eq 0 -and (Test-ZipMagic $ZipPath)) {
            Write-Output "Compressed via $($attempt.Cmd): $ZipPath"
            return
        }
        Write-Warning "Compression via $($attempt.Cmd) failed (exit=$LASTEXITCODE or bad magic); falling back."
    }

    # 兜底：System32 bsdtar 直接打 zip（条目相对 -C Dir，无 ./ 前缀）
    if (Test-ZipMagic $ZipPath) { Remove-Item -LiteralPath $ZipPath -Force }
    $bsdtar = Join-Path $env:SystemRoot 'System32\tar.exe'
    if (-not (Test-Path -LiteralPath $bsdtar -PathType Leaf)) {
        throw "All zip methods failed (pwsh / powershell / bsdtar not found at $bsdtar)"
    }
    $entries = @(Get-ChildItem -LiteralPath $Dir | ForEach-Object { $_.Name })
    if ($entries.Count -eq 0) { throw "Source directory is empty: $Dir" }
    & $bsdtar -a -c -f $ZipPath -C $Dir @entries
    if ($LASTEXITCODE -ne 0) { throw "bsdtar zip failed with exit code $LASTEXITCODE" }
    if (-not (Test-ZipMagic $ZipPath)) {
        Remove-Item -LiteralPath $ZipPath -Force
        throw "bsdtar output is not a valid zip (unexpected; PATH tar is likely GNU tar)"
    }
    Write-Output "Compressed via System32 tar.exe (fallback): $ZipPath"
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
