---
name: publish
description: Pulsar 发布与打包流程。支持 local-artifact、local-version 和 release 模式；按模式执行版本决策、绝对路径构建、产物校验、ZIP 打包以及可选的 commit/tag/GitHub Release。用户要求发布、打包、publish 或运行 /publish 时使用。
---

# Pulsar 发布流程

所有命令从仓库根目录执行，但文件路径必须基于 `git rev-parse --show-toplevel` 生成绝对路径。每个阶段使用 bash 执行并展示结果；失败后先读取完整错误，再从失败阶段重试，不重复已成功的阶段。

## 0. 发布模式

先判断模式。若用户没有明确说明，询问并说明影响：

| 模式 | 行为 |
|---|---|
| `local-artifact` | 使用现有版本构建和打包；不修改版本号，不 commit，不 tag，不推送 |
| `local-version` | 使用确认后的版本号更新项目版本，构建和打包；不 commit，不 tag，不推送 |
| `release` | 更新版本、生成 release notes，用户确认后 commit/tag 并 push；构建、打包与 GitHub Release 由 CI 自动完成，本地**不重复**构建打包 |

用户说“发布一个本地版本”时，默认使用 `local-version`。

**release 模式自动化**：仓库已配置 CI（`.github/workflows/release.yml`）。push `v*` tag 时自动构建 full + portable、校验产物、压缩为 `Pulsar-$version-{full,portable}.zip` 并创建/填充 GitHub Release，release notes 取自 **tag message**（`--notes-from-tag`）。因此 release 模式的本地职责是：确认版本 → 生成并确认完整 notes → **把完整 notes 写入 tag message** → commit/tag → push → 等 CI 完成并验证 GitHub Release。不要重复本地构建打包（本地冒烟可选，见第 9 节）。

## 1. 版本决策

先取得仓库根目录和当前项目版本：

```powershell
$repo = (git rev-parse --show-toplevel).Trim()
$csproj = Join-Path $repo 'Pulsar\Pulsar\Pulsar.csproj'
$versionMatch = Select-String -Path $csproj -Pattern '<Version>([^<]+)</Version>'
if ($versionMatch.Count -ne 1) { throw "Expected exactly one <Version> in $csproj" }
$currentVersion = $versionMatch.Matches[0].Groups[1].Value
Write-Output "Repository: $repo"
Write-Output "Current version: $currentVersion"
```

- 用户给出显式版本 → 直接使用该版本。
- 用户未给版本且是 `local-artifact` → 使用当前版本。
- 用户未给版本且是 `local-version` 或 `release` → 查看最近 tag 和提交；包含 `feat` 时建议 minor，包含 `fix` 时建议 patch，否则建议保守 patch。
- 向用户展示建议版本和依据，得到确认后再修改版本。

最近 tag 和提交检查：

```powershell
$lastTag = (git tag --sort=-creatordate | Select-Object -First 1).Trim()
if ([string]::IsNullOrWhiteSpace($lastTag)) {
    git log --no-merges --pretty=format:%s -20
} else {
    git log --no-merges --pretty=format:%s "$lastTag..HEAD"
}
```

版本必须符合 `major.minor.patch`，并且不得低于当前版本，除非用户明确要求降级。

### 修改与校验版本

只修改项目文件中的 `<Version>`。修改后使用结构化正则再次校验：

```powershell
$versionMatch = Select-String -Path $csproj -Pattern '<Version>([^<]+)</Version>'
$actualVersion = $versionMatch.Matches[0].Groups[1].Value
if ($actualVersion -ne $version) {
    throw "Version mismatch: csproj=$actualVersion expected=$version"
}
```

## 2. 初始化绝对路径

每次发布都使用以下路径变量，不把相对路径传给 `PublishDir`：

每次发布构建两个版本：

| 版本 | 含义 | 命名 |
|---|---|---|
| `full` | 自包含单文件，含 .NET 运行时（cor3 DLL） | `Pulsar-$version-full.zip` |
| `portable` | framework-dependent 单文件，不含运行时（需本机装 .NET 8 Desktop Runtime） | `Pulsar-$version-portable.zip` |

```powershell
$repo = (git rev-parse --show-toplevel).Trim()
$csproj = Join-Path $repo 'Pulsar\Pulsar\Pulsar.csproj'
$publishRoot = Join-Path $repo "Artifacts\publish\v$version"
$fullDir = Join-Path $publishRoot 'full'
$portableDir = Join-Path $publishRoot 'portable'
$zipFull = Join-Path $repo "Artifacts\Pulsar-$version-full.zip"
$zipPortable = Join-Path $repo "Artifacts\Pulsar-$version-portable.zip"
$projectArtifactsDir = Join-Path $repo 'Pulsar\Pulsar\Artifacts'

if (-not (Test-Path -LiteralPath (Split-Path -Parent $publishRoot))) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $publishRoot) -Force | Out-Null
}
```

`PublishDir` 必须是 `$fullDir\` / `$portableDir\` 的绝对路径。不得依赖项目目录作为相对路径基准。

## 3. 构建

构建前清理目标版本目录和同名 ZIP。只清理本次版本的产物，不删除其他版本：

```powershell
if (Test-Path -LiteralPath $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}
foreach ($zip in @($zipFull, $zipPortable)) {
    if (Test-Path -LiteralPath $zip) {
        Remove-Item -LiteralPath $zip -Force
    }
}
New-Item -ItemType Directory -Path $fullDir -Force | Out-Null
New-Item -ItemType Directory -Path $portableDir -Force | Out-Null

# full：自包含单文件，含运行时
dotnet publish $csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishReadyToRun=true `
    "-p:PublishDir=$fullDir\"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (full) failed with exit code $LASTEXITCODE" }

# portable：framework-dependent 单文件，不含运行时
dotnet publish $csproj `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    "-p:PublishDir=$portableDir\"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (portable) failed with exit code $LASTEXITCODE" }
```

不要在发布后通过猜测路径复制产物。如果发布目录不包含预期文件，应先检查 MSBuild 输出和项目文件，而不是静默搬运错误目录。

## 4. 校验发布产物

`full` 目录必须直接包含 `Pulsar.exe`、`Pulsar.pdb`、至少一个 `*_cor3.dll` 和 `Assets\`；`portable` 目录必须直接包含 `Pulsar.exe`、`Pulsar.pdb` 和 `Assets\`，且**不得**包含 `*_cor3.dll`：

```powershell
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

Assert-Publish -Dir $fullDir -RequireCor3 $true
Assert-Publish -Dir $portableDir -RequireCor3 $false
```

portable 版构建后建议做冒烟测试（`Start-Process` 启动 6 秒不崩溃即通过），确认本机 .NET 8 Desktop Runtime 兼容。

如果产物意外出现在 `$projectArtifactsDir`，停止并修正 `PublishDir` 为绝对路径；不得把嵌套目录作为成功产物。

## 5. 打包 ZIP

优先使用 PowerShell 7。`Compress-Archive` 的源路径可以使用通配符，但不要把该通配符传给 `-LiteralPath`：

```powershell
foreach ($pair in @(
    @{ Dir = $fullDir; Zip = $zipFull },
    @{ Dir = $portableDir; Zip = $zipPortable }
)) {
    pwsh -NoProfile -Command "Compress-Archive -Path '$($pair.Dir)\*' -DestinationPath '$($pair.Zip)' -CompressionLevel Optimal -Force"
    if ($LASTEXITCODE -ne 0) { throw "pwsh Compress-Archive failed with exit code $LASTEXITCODE" }
}
```

如果 `pwsh` 不可用，再使用 PowerShell 5.1：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '$($pair.Dir)\*' -DestinationPath '$($pair.Zip)' -CompressionLevel Optimal -Force"
if ($LASTEXITCODE -ne 0) { throw "powershell Compress-Archive failed with exit code $LASTEXITCODE" }
```

最后才使用 Windows 自带 bsdtar，不要使用 PATH 中可能来自 Git 的 GNU tar：

```powershell
& 'C:\Windows\System32\tar.exe' -a -c -f $zipFull -C $fullDir Assets Pulsar.exe Pulsar.pdb *.dll
& 'C:\Windows\System32\tar.exe' -a -c -f $zipPortable -C $portableDir Assets Pulsar.exe Pulsar.pdb
if ($LASTEXITCODE -ne 0) { throw "bsdtar failed with exit code $LASTEXITCODE" }
```

详见 `Docs/lessons/POWERSHELL_5_1_COMPRESS_ARCHIVE_BROKEN.md`。

如果必须复制目录，使用目录枚举，不要将 `*` 与 `-LiteralPath` 混用：

```powershell
Get-ChildItem -LiteralPath $sourceDir -Force |
    Copy-Item -Destination $targetDir -Recurse -Force
```

## 6. 校验 ZIP

两个 ZIP 都须校验：

```powershell
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

Assert-Zip -ZipPath $zipFull
Assert-Zip -ZipPath $zipPortable
```

`full` 条目必须包含 `Pulsar.exe`、`Pulsar.pdb`、`Assets/` 和 `*_cor3.dll`；`portable` 条目必须包含 `Pulsar.exe`、`Pulsar.pdb`、`Assets/` 且无 `*_cor3.dll`。最终报告两个 ZIP 的绝对路径和字节大小。

## 7. Release notes

仅 `release` 模式需要 release notes。基于用户可感知的提交撰写简洁中文说明，固定使用以下章节，无内容章节省略：

```markdown
### 新功能
- ...

### 修复
- ...

### 性能优化
- ...

### 其他
- ...
```

展示 notes 并等待用户确认。未经确认不得用于 tag message 或 GitHub Release。

## 8. Commit 与 tag

`local-artifact` 和 `local-version` 模式跳过本节，并在最终报告中明确：`Commit: skipped`、`Tag: skipped`。

`release` 模式在用户确认 release notes 后执行。提交前检查 `git status` 和 `git diff`，只暂存版本号文件：

```powershell
if ($LASTEXITCODE -ne 0) { throw "version commit failed" }
git tag -a "v$version" -m "$notes"
if ($LASTEXITCODE -ne 0) { throw "tag creation failed" }
```

**tag message 必须是用户确认过的完整 release notes**，不要只写一行摘要——CI 用 `--notes-from-tag` 生成 GitHub Release body，tag message 就是发布页展示的 notes。若事后需修正 notes，用 `gh release edit "v$version" --notes-file <path>`（中文验证见第 9 节）。

若没有版本号变化，跳过 commit；若 tag 已存在，停止并询问用户，不覆盖已有 tag。

## 9. GitHub Release（release 模式）

**主路径（默认）：不手动上传，交给 CI。** 第 8 节 push tag 后，`release.yml` 自动触发（触发条件 `on.push.tags: v*`）。等待并验证：

```powershell
gh run list --workflow=release.yml --limit 1   # 取最新 run id
gh run watch <run-id> --exit-status            # 等待 CI 构建、校验、上传完成
```

CI 完成后验证 Release 已创建且资产齐全：

```powershell
gh release view "v$version" --json tagName,isDraft,assets
# 期望：isDraft=false；assets 含 Pulsar-$version-full.zip 与 Pulsar-$version-portable.zip
```

**中文 notes 验证**：PowerShell 控制台默认编码（GBK）会损坏 UTF-8 中文显示，切勿用终端输出判断乱码。把 release body 写入文件后直接用 Read 工具核对：

```powershell
gh release view "v$version" --json body | ConvertFrom-Json | ForEach-Object {
    [System.IO.File]::WriteAllText("$env:TEMP\pulsar-upload\body_check.md", $_.body, [System.Text.Encoding]::UTF8)
}
# 然后 Read $env:TEMP\pulsar-upload\body_check.md
```

若 body 非完整 notes（例如历史上 tag message 只写了一行），修正：

```powershell
gh release edit "v$version" --notes-file (Join-Path $env:TEMP 'pulsar-upload\notes.md')
```

**回退路径（仅当 CI 不可用或用户明确要求本地产物上传）**：因仓库路径包含 `#`，先把 ZIP/notes 复制到不含 `#` 的临时目录再调用 `gh`，详见 `Docs/lessons/GH_CLI_HASH_PATH_BUG.md`。此时 release 模式才需要执行第 3-6 节的本地构建与 ZIP 校验（本地冒烟测试 `Start-Process` 启动 6 秒不崩溃）。

## 10. 排障

失败后先执行并检查完整输出：

```powershell
Get-ChildItem -LiteralPath (Join-Path $repo 'Artifacts') -Recurse
```

- `Pulsar.exe/PDB/Assets/cor3` 缺失：检查 `$fullDir`/`$portableDir` 是否为绝对路径，以及 `dotnet publish` 的最终输出路径。
- 产物落在 `Pulsar/Pulsar/Artifacts`：清理该版本嵌套目录，修正 `PublishDir` 后从构建阶段重试。
- `CommandNotFoundException` 或 `Compress-Archive` 失败：按 `pwsh` → `powershell -ExecutionPolicy Bypass` → System32 `tar.exe` 顺序降级。
- ZIP 不是 `PK`：删除 ZIP，从打包阶段重试；不要使用 PATH 中的 GNU tar 生成伪 ZIP。
- `GetFileAttributesEx` 路径被截断：将 ZIP/notes 复制到不含 `#` 的临时目录再调用 `gh`。
- GitHub Release body 只有一行 / 非完整 notes：tag message 用了单行摘要，而 CI 用 `--notes-from-tag` 生成 body。修正：`gh release edit "v$version" --notes-file <完整notes文件>`（不重建 tag）。
- Release body 终端显示乱码：PowerShell 控制台 GBK 编码干扰，GitHub 端可能完好。用第 9 节方法写文件后用 Read 工具核对，不要凭终端输出判断。
- release 已存在或 tag 已存在：停止并询问用户，不删除、不覆盖。

## 11. 完成报告

最终报告必须包含（`release` 模式用 CI 产物，`local-*` 模式用本地产物，不适用的行填 N/A）：

```text
Mode: local-artifact | local-version | release
Version: ...
Local build: performed | skipped (release 默认 CI 构建，本地构建仅回退/冒烟)
Publish directories: <fullDir> | <portableDir>  (local) 或 N/A (release, CI)
ZIP (full): <绝对路径或 GitHub Release 资产链接>
ZIP (full) size: ... bytes (release 用 CI 资产大小)
ZIP (portable): <绝对路径或 GitHub Release 资产链接>
ZIP (portable) size: ... bytes
Pulsar.exe/PDB: present
cor3 DLL count (full): ...
Assets file count: ...
Release notes: written to tag message | edited via gh release edit
Commit: created | skipped
Tag: created | skipped
GitHub Release: created by CI | uploaded manually | skipped
Push: performed | skipped
```

若发生降级、跳过或路径纠正，必须在报告中明确说明。
