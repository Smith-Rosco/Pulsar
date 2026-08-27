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
| `release` | 更新版本，构建、打包、生成 release notes，并在用户确认后 commit/tag；推送和 GitHub Release 仍需明确授权 |

用户说“发布一个本地版本”时，默认使用 `local-version`。

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

```powershell
$repo = (git rev-parse --show-toplevel).Trim()
$csproj = Join-Path $repo 'Pulsar\Pulsar\Pulsar.csproj'
$publishDir = Join-Path $repo "Artifacts\publish\v$version"
$zipPath = Join-Path $repo "Artifacts\Pulsar-v$version.zip"
$projectArtifactsDir = Join-Path $repo 'Pulsar\Pulsar\Artifacts'

if (-not (Test-Path -LiteralPath (Split-Path -Parent $publishDir))) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $publishDir) -Force | Out-Null
}
```

`PublishDir` 必须是 `$publishDir\` 的绝对路径。不得依赖项目目录作为相对路径基准。

## 3. 构建

构建前清理目标版本目录和同名 ZIP。只清理本次版本的产物，不删除其他版本：

```powershell
if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

dotnet publish $csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishReadyToRun=true `
    "-p:PublishDir=$publishDir\"
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
```

不要在发布后通过猜测路径复制产物。如果 `$publishDir` 不包含预期文件，应先检查 MSBuild 输出和项目文件，而不是静默搬运错误目录。

## 4. 校验发布产物

发布目录必须直接包含 `Pulsar.exe`、`Pulsar.pdb`、至少一个 `*_cor3.dll` 和 `Assets\`：

```powershell
$requiredFiles = @('Pulsar.exe', 'Pulsar.pdb')
foreach ($name in $requiredFiles) {
    $path = Join-Path $publishDir $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing publish artifact: $path"
    }
}

$assetsDir = Join-Path $publishDir 'Assets'
if (-not (Test-Path -LiteralPath $assetsDir -PathType Container)) {
    throw "Missing publish artifact directory: $assetsDir"
}

$cor3 = @(Get-ChildItem -LiteralPath $publishDir -Filter '*_cor3.dll' -File)
if ($cor3.Count -eq 0) {
    throw "No *_cor3.dll found in $publishDir"
}

$assetCount = @(Get-ChildItem -LiteralPath $assetsDir -Recurse -File).Count
$exe = Get-Item -LiteralPath (Join-Path $publishDir 'Pulsar.exe')
Write-Output "Publish artifacts valid: exe=$($exe.Length) bytes, cor3=$($cor3.Count), assets=$assetCount"
```

如果产物意外出现在 `$projectArtifactsDir`，停止并修正 `PublishDir` 为绝对路径；不得把嵌套目录作为成功产物。

## 5. 打包 ZIP

优先使用 PowerShell 7。`Compress-Archive` 的源路径可以使用通配符，但不要把该通配符传给 `-LiteralPath`：

```powershell
pwsh -NoProfile -Command "Compress-Archive -Path '$publishDir\*' -DestinationPath '$zipPath' -CompressionLevel Optimal -Force"
if ($LASTEXITCODE -ne 0) { throw "pwsh Compress-Archive failed with exit code $LASTEXITCODE" }
```

如果 `pwsh` 不可用，再使用 PowerShell 5.1：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '$publishDir\*' -DestinationPath '$zipPath' -CompressionLevel Optimal -Force"
if ($LASTEXITCODE -ne 0) { throw "powershell Compress-Archive failed with exit code $LASTEXITCODE" }
```

最后才使用 Windows 自带 bsdtar，不要使用 PATH 中可能来自 Git 的 GNU tar：

```powershell
& 'C:\Windows\System32\tar.exe' -a -c -f $zipPath -C $publishDir Assets Pulsar.exe Pulsar.pdb *.dll
if ($LASTEXITCODE -ne 0) { throw "bsdtar failed with exit code $LASTEXITCODE" }
```

详见 `Docs/lessons/POWERSHELL_5_1_COMPRESS_ARCHIVE_BROKEN.md`。

如果必须复制目录，使用目录枚举，不要将 `*` 与 `-LiteralPath` 混用：

```powershell
Get-ChildItem -LiteralPath $sourceDir -Force |
    Copy-Item -Destination $targetDir -Recurse -Force
```

## 6. 校验 ZIP

```powershell
if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) {
    throw "ZIP was not created: $zipPath"
}

$bytes = [System.IO.File]::ReadAllBytes($zipPath)
if ($bytes.Length -lt 2 -or $bytes[0] -ne 0x50 -or $bytes[1] -ne 0x4B) {
    throw "ZIP magic is not PK"
}

Write-Output ('ZIP magic: {0:X2}{1:X2}' -f $bytes[0], $bytes[1])
& 'C:\Windows\System32\tar.exe' -tf $zipPath
if ($LASTEXITCODE -ne 0) { throw "ZIP listing failed with exit code $LASTEXITCODE" }
```

条目必须包含 `Pulsar.exe`、`Pulsar.pdb`、`Assets/` 和 `*_cor3.dll`。最终报告 ZIP 绝对路径和字节大小。

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

若没有版本号变化，跳过 commit；若 tag 已存在，停止并询问用户，不覆盖已有 tag。

## 9. GitHub Release

只有用户明确授权推送或发布到 GitHub 时执行。优先推送 annotated tag，让 CI 构建：

```powershell
gh run watch <run-id>
```

若需要上传本地产物，因仓库路径包含 `#`，先复制到不含 `#` 的临时目录。不要直接把仓库路径传给 `gh`：

```powershell
$uploadDir = Join-Path $env:TEMP 'pulsar-upload'
if (Test-Path -LiteralPath $uploadDir) { Remove-Item -LiteralPath $uploadDir -Recurse -Force }
New-Item -ItemType Directory -Path $uploadDir -Force | Out-Null
Copy-Item -LiteralPath $zipPath -Destination (Join-Path $uploadDir (Split-Path $zipPath -Leaf)) -Force
```

分步上传：

```powershell
```

详见 `Docs/lessons/GH_CLI_HASH_PATH_BUG.md`。

## 10. 排障

失败后先执行并检查完整输出：

```powershell
Get-ChildItem -LiteralPath (Join-Path $repo 'Artifacts') -Recurse
```

- `Pulsar.exe/PDB/Assets/cor3` 缺失：检查 `$publishDir` 是否为绝对路径，以及 `dotnet publish` 的最终输出路径。
- 产物落在 `Pulsar/Pulsar/Artifacts`：清理该版本嵌套目录，修正 `PublishDir` 后从构建阶段重试。
- `CommandNotFoundException` 或 `Compress-Archive` 失败：按 `pwsh` → `powershell -ExecutionPolicy Bypass` → System32 `tar.exe` 顺序降级。
- ZIP 不是 `PK`：删除 ZIP，从打包阶段重试；不要使用 PATH 中的 GNU tar 生成伪 ZIP。
- `GetFileAttributesEx` 路径被截断：将 ZIP/notes 复制到不含 `#` 的临时目录再调用 `gh`。
- release 已存在或 tag 已存在：停止并询问用户，不删除、不覆盖。

## 11. 完成报告

最终报告必须包含：

```text
Mode: local-artifact | local-version | release
Version: ...
Publish directory: <absolute path>
ZIP: <absolute path>
ZIP size: ... bytes
Pulsar.exe/PDB: present
cor3 DLL count: ...
Assets file count: ...
Commit: created | skipped
Tag: created | skipped
GitHub Release: created | skipped
Push: performed | skipped
```

若发生降级、跳过或路径纠正，必须在报告中明确说明。
