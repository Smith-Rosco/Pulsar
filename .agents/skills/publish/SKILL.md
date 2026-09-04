---
name: publish
description: Pulsar 发布与打包流程。支持 local-artifact、local-version 和 release 模式；按模式执行版本决策、绝对路径构建、产物校验、ZIP 打包以及可选的 commit/tag/GitHub Release。用户要求发布、打包、publish 或运行 /publish 时使用。
---

# Pulsar 发布流程

本 skill 提供发布决策与编排逻辑，所有可执行操作固化为 `scripts/` 下的独立脚本。所有命令从仓库根目录执行；脚本内部路径基于 `git rev-parse --show-toplevel` 生成绝对路径，不依赖调用目录。

每个阶段调用对应脚本并展示结果；失败后先读取完整错误，再从失败阶段重试，不重复已成功的阶段。

## scripts/ 一览

| 脚本 | 作用 | 主要参数 |
|---|---|---|
| `Pulsar.Publish.Common.ps1` | 共享函数库（路径、版本读写、产物/ZIP 校验、无 BOM 写入）；被各脚本 dot-source，不直接运行 | - |
| `Get-ReleaseInfo.ps1` | 输出仓库根、当前版本、最近 tag、自 tag 以来的提交（版本决策辅助） | - |
| `Set-ProjectVersion.ps1` | 修改 `csproj` 的 `<Version>` 并同步 `<FileVersion>`/`<AssemblyVersion>`（`x.y.z.0`）；结构化校验；防降级与同版本重发（降级需 `-AllowDowngrade`） | `-Version 1.9.0` |
| `Build-Publish.ps1` | 构建 full + portable 两个发布产物并校验（含清理本次版本目录/ZIP）；`-Build n` 支持 x.y.z.n 本地构建号 | `-Version 1.9.0 [-Build 2]` |
| `Pack-Zips.ps1` | 将产物压缩为 `Pulsar-$version-{full,portable}.zip` 并校验（内置 pwsh → powershell → System32 tar 三级回退） | `-Version 1.9.0 [-Build 2]` |
| `Update-Changelog.ps1` | 将 `CHANGELOG.md` 的 `[Unreleased]` 段固化为 `## [X.Y.Z] - 日期` 并插入新的空 Unreleased 段；段内无真实条目时拒绝执行 | `-Version 1.9.0` |
| `New-ReleaseTag.ps1` | release 模式：commit 版本号 + 创建带完整 notes 的 tag + 校验 tag message | `-Version 1.9.0 -NotesFile <路径>` |
| `Watch-Release.ps1` | 等待 release.yml CI 完成（含 run 注册轮询 ~60s）、验证 Release 资产、导出 body 供核对 | `-Version 1.9.0` |
| `Edit-ReleaseNotes.ps1` | 修正 GitHub Release body 为完整 release notes | `-Version 1.9.0 -NotesFile <路径>` |

## 0. 发布模式

先判断模式。若用户没有明确说明，询问并说明影响：

| 模式 | 行为 |
|---|---|
| `local-artifact` | 使用现有版本构建和打包；不修改版本号，不 commit，不 tag，不推送 |
| `local-version` | 使用确认后的版本号更新项目版本，构建和打包；不 commit，不 tag，不推送 |
| `release` | 更新版本、生成 release notes，用户确认后 commit/tag 并 push；构建、打包与 GitHub Release 由 CI 自动完成，本地**不重复**构建打包 |

用户说"发布一个本地版本"时，默认使用 `local-version`。

### 本地构建号（x.y.z.n，方案 B）

`local-artifact` / `local-version` 模式可加第 4 位构建号区分同一版本的多次本地构建：

- **csproj 永远只存 `x.y.z`**。构建号不写入 csproj、不进入 CHANGELOG、不创建 `v1.9.1.2` 形式的 tag（`New-ReleaseTag.ps1` 的三位校验会拦截）。
- 通过 `Build-Publish.ps1 -Build 2` 在 publish 阶段以 `-p:Version/FileVersion/AssemblyVersion` 覆盖，exe 文件属性显示 `1.9.1.2`。
- 产物命名使用四位版本：`Artifacts\publish\v1.9.1.2\{full,portable}\` 与 `Artifacts\Pulsar-1.9.1.2-{full,portable}.zip`，多次构建并存不覆盖。
- 产物内附 `build-info.txt`（版本、构建号、channel、构建时间、commit hash）。
- 下一个构建号用 `Get-ReleaseInfo.ps1` 输出的 `Next local build number` 推断（取已用最大值 + 1）。
- `release` 模式**不使用**构建号：正式版本语义始终是三位。

**release 模式自动化**：仓库已配置 CI（`.github/workflows/release.yml`）。push `v*` tag 时自动构建 full + portable、校验产物、压缩为 `Pulsar-$version-{full,portable}.zip` 并创建/填充 GitHub Release，release notes 取自 **tag message**（workflow 用 GitHub API 显式读取 annotated tag message，**不使用** `--notes-from-tag`——部分 gh 版本上它会退化为 commit subject，见第 7 节排障）。因此 release 模式的本地职责是：确认版本 → 生成并确认完整 notes → **把完整 notes 写入 tag message** → commit/tag → push → 等 CI 完成并验证 GitHub Release。不要重复本地构建打包（本地冒烟可选，见回退路径）。

## 1. 版本决策

## 1. 版本决策

运行 `scripts/Get-ReleaseInfo.ps1` 取得仓库根、当前版本、最近 tag 与提交。

- 用户给出显式版本 → 直接使用该版本。
- 用户未给版本且是 `local-artifact` → 使用当前版本。
- 用户未给版本且是 `local-version` 或 `release` → 查看最近 tag 和提交；包含 `feat` 时建议 minor，包含 `fix` 时建议 patch，否则建议保守 patch。
- 向用户展示建议版本和依据，得到确认后再修改版本。

版本必须符合 `major.minor.patch`，且不得低于当前版本，除非用户明确要求降级（此时 `Set-ProjectVersion.ps1` 需加 `-AllowDowngrade`）。

### 修改与校验版本（local-version / release）

```powershell
pwsh .agents/skills/publish/scripts/Set-ProjectVersion.ps1 -Version 1.9.0
```

脚本同步更新 `<Version>`（x.y.z）与 `<FileVersion>`/`<AssemblyVersion>`（x.y.z.0），改后结构化正则校验，不匹配即抛错。版本相同或低于当前版本均会拒绝（同版本重发请用 `local-artifact` 模式）。

## 2. 构建与打包（local-artifact / local-version）

脚本内置：只清理本次版本的目录和同名 ZIP（不删其他版本）；`PublishDir` 为绝对路径。

```powershell
pwsh .agents/skills/publish/scripts/Build-Publish.ps1 -Version 1.9.0           # 常规：x.y.z
pwsh .agents/skills/publish/scripts/Build-Publish.ps1 -Version 1.9.1 -Build 2 # 本地构建号：x.y.z.2
pwsh .agents/skills/publish/scripts/Pack-Zips.ps1  -Version 1.9.0             # -Build 与上一步保持一致
```

产物规则（脚本内断言，失败抛错）：
- `full` 目录：`Pulsar.exe`、`Pulsar.pdb`、至少一个 `*_cor3.dll`、`Assets\`。
- `portable` 目录：`Pulsar.exe`、`Pulsar.pdb`、`Assets\`，且**不得**含 `*_cor3.dll`。
- 两个 ZIP 均以 `PK` 开头（`Compress-Archive` 魔数），并列出内容核对。
- 两个目录均含 `build-info.txt`（版本 / 构建号 / channel / 时间 / commit）。

portable 版构建后建议冒烟测试（`Start-Process` 启动 6 秒不崩溃即通过），确认本机 .NET 8 Desktop Runtime 兼容。

## 3. Release notes（release 模式）

基于用户可感知的提交撰写简洁中文说明，固定使用以下章节，无内容章节省略：

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

展示 notes 并等待用户确认。未经确认不得用于 tag message 或 GitHub Release。将确认后的 notes 保存为临时 `.md` 文件供后续脚本使用。

### 更新 CHANGELOG（release 模式）

发布前必须把 `CHANGELOG.md` 的 `[Unreleased]` 段固化为正式条目：

1. 检查 `[Unreleased]` 段内容：若仍是"暂无/TODO"占位符，先根据本次版本的提交记录
   （用户可感知的变更）补全；某分类确实无变更时保留"暂无"即可。
2. 运行脚本固化：

```powershell
pwsh .agents/skills/publish/scripts/Update-Changelog.ps1 -Version 1.9.0
```

脚本把 `## [Unreleased]` 改写为 `## [X.Y.Z] - <今天日期>` 并插入新的空 Unreleased 段；
段内没有任何真实条目时会拒绝执行。完成后展示结果并纳入版本 commit（见第 4 节）。

`local-artifact` / `local-version` 模式不改 CHANGELOG。

## 4. Commit 与 tag（release 模式）

`local-artifact` 和 `local-version` 模式跳过本节，并在最终报告中明确：`Commit: skipped`、`Tag: skipped`。

```powershell
pwsh .agents/skills/publish/scripts/New-ReleaseTag.ps1 -Version 1.9.0 -NotesFile <确认后的notes路径>
```

`New-ReleaseTag.ps1` 内部处理（勿再手写）：
- 版本相关文件（`Pulsar/Pulsar/Pulsar.csproj` 与 `CHANGELOG.md`）若有改动则一并 commit（`chore(release): bump version to X`），无改动跳过。
- 将 notes 重写为**无 BOM 的 UTF-8**（PS7 的 `[Text.Encoding]::UTF8` 带 BOM，会污染 tag message 首行）。
- 用 `git -c core.commentChar=§ tag -a` 创建 annotated tag —— **必须覆盖 commentChar**：git 默认 `#` 会把 `### 新功能` 等章节标题当注释剥离，导致 Release body 只剩正文 bullet。
- tag 已存在则停止抛错，不覆盖已有 tag。
- 校验 tag message：cmd 直接重定向原始字节到文件（PS 管道会经 GBK 重解码乱码），核对首三字节为 `23 23 23`（`###`）而非 `EF BB BF`（BOM）。

**tag message 必须是用户确认过的完整 release notes**，不要只写一行摘要——CI 用 GitHub API 读取 tag message 生成 GitHub Release body（见第 5 节），tag message 就是发布页展示的 notes。

### push

```powershell
git push origin main
git push origin v1.9.0
```

## 5. GitHub Release（release 模式）

**主路径（默认）：不手动上传，交给 CI。** push tag 后，`release.yml` 自动触发（触发条件 `on.push.tags: v*`）。

```powershell
pwsh .agents/skills/publish/scripts/Watch-Release.ps1 -Version 1.9.0
```

脚本会：取最新 `release.yml` run → `gh run watch --exit-status` 等待完成 → 导出 Release JSON 与 body 到 `$env:TEMP\pulsar-upload\` → 期望 `isDraft=false` 且 assets 含 `Pulsar-$version-full.zip` 与 `Pulsar-$version-portable.zip`。

**中文 notes 验证**：PowerShell 控制台默认编码（GBK）会把 `gh` 的 UTF-8 输出重解码成乱码，**切勿用终端输出或 PS 管道文件判断**。Watch-Release 已用 `cmd` 直接重定向导出 `body_check_$version.md`，用 Read 工具核对：应显示完整中文 notes，首字节对应 UTF-8 `E6 96 B0`（新），而非 mojibake。

若 body 非完整 notes（例如历史 tag message 只写了一行，或 `###` 标题被 git 剥离），修正：

```powershell
pwsh .agents/skills/publish/scripts/Edit-ReleaseNotes.ps1 -Version 1.9.0 -NotesFile <完整notes路径>
```

edit 后再次用 Watch-Release 导出的 `body_check_$version.md` 核对（不重建 tag）。

## 6. 回退路径（仅当 CI 不可用或用户明确要求本地产物上传）

因仓库路径包含 `#`，先把 ZIP/notes 复制到不含 `#` 的临时目录再调用 `gh`，详见 `Docs/lessons/GH_CLI_HASH_PATH_BUG.md`。此时 release 模式才需要执行第 2 节的本地构建与 ZIP 校验（本地冒烟测试 `Start-Process` 启动 6 秒不崩溃）。

## 7. 排障

失败后先执行并检查完整输出：

```powershell
Get-ChildItem -LiteralPath (Join-Path (git rev-parse --show-toplevel) 'Artifacts') -Recurse
```

- `Pulsar.exe/PDB/Assets/cor3` 缺失：检查 `$fullDir`/`$portableDir` 是否为绝对路径（脚本已基于 repo 根生成），以及 `dotnet publish` 的最终输出路径。
- 产物落在 `Pulsar/Pulsar/Artifacts`：清理该版本嵌套目录，修正 `PublishDir` 后从构建阶段重试。
- `CommandNotFoundException` 或 `Compress-Archive` 失败：`Pack-Zips.ps1` 已内置
  `pwsh` → `powershell -ExecutionPolicy Bypass` → System32 `tar.exe` 三级回退（每级用 PK 魔数校验）；
  三级全部失败才需人工介入。
- ZIP 不是 `PK`：删除 ZIP，从打包阶段重试；不要使用 PATH 中的 GNU tar 生成伪 ZIP。
- `GetFileAttributesEx` 路径被截断：将 ZIP/notes 复制到不含 `#` 的临时目录再调用 `gh`。
- GitHub Release body 只有一行 / 非完整 notes：tag message 用了单行摘要，或 `###` 标题被 git 注释剥离。修正：`Edit-ReleaseNotes.ps1`（不重建 tag）。
- Release body 终端显示乱码：PowerShell 控制台 GBK 编码干扰，GitHub 端可能完好。用 Read 工具核对 `body_check_$version.md`，不要凭终端输出判断，更不要用 `ConvertFrom-Json | WriteAllText` 管道（会二次编码误报损坏）。
- tag message 中 `###` 章节标题丢失（`git tag -n1` 首行是 bullet 而非 `###`）：git 默认 `commentChar=#` 把 `#` 开头行当注释剥离。修正：删旧 tag，用 `New-ReleaseTag.ps1`（内置 `-c core.commentChar=§`）重建（未 push 时）。
- tag message 首行带 BOM（Format-Hex 首三字节 `EF BB BF`）：notes 文件用了会带 BOM 的编码。修正：用 Write 工具或 `New-Object System.Text.UTF8Encoding($false)` 重写文件后重建 tag。
- `git cat-file tag` / `gh` 输出经 PS 管道后中文乱码：cmd 直接重定向（`cmd /c "... > file"`）的字节才是真实数据；PS 管道经 `[Console]::OutputEncoding`(gb2312) 重解码会损坏中文。
- `New-ReleaseTag.ps1` 误报 "Tag message does not start with '###'"：**`cmd /c` 命令行上下文不会像批处理文件那样把 `%%` 折叠成 `%`**——传给 git for-each-ref 的格式串必须写单个 `%(contents)`；若误写 `%%(contents)`，git 会原样输出字面量 `%(contents)`，导致校验误报（tag 本身仍正确，需用 `git cat-file tag` 核实）。此坑已修复；改动该行时不要重新引入双 `%`。
- 推送 tag 后 `Watch-Release.ps1` 报 "No release.yml run found"：GitHub Actions 对刚 push 的 tag 注册工作流有延迟，脚本已内置最多 ~60s（每 5s 一次，共 12 次）的 run 注册轮询；若仍失败再检查 tag 是否真的推送成功。
- Release body 是 commit subject（如 `chore(release): bump version to 1.10.0`）而非完整 tag notes，且 tag message 已核对正确：**`gh release create --notes-from-tag` 在部分 gh 版本上会退化为读取 commit subject**（2026-09 实测：v1.9.1 正常、v1.10.0 退化，同一 workflow 不同 runner gh 版本行为不同）。release.yml 已改为用 GitHub API 显式读取 annotated tag message（`git/ref/tags/<tag>` → `git/tags/<sha>` → `--jq .message`）经 cmd 重定向写入 notes 文件再 `--notes-file`。若历史 release 已受影响，用 `Edit-ReleaseNotes.ps1` 修正 body；修改 workflow 时不要退回 `--notes-from-tag`。
- release 已存在或 tag 已存在：停止并询问用户，不删除、不覆盖。

## 8. 完成报告

最终报告必须包含（`release` 模式用 CI 产物，`local-*` 模式用本地产物，不适用的行填 N/A）：

```text
Mode: local-artifact | local-version | release
Version: ...
Build number: n | none (release 模式固定 none)
Effective version: x.y.z(.n)  (产物目录/zip/exe 文件属性所用版本)
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
Changelog: updated | skipped (local-* 模式)
Commit: created | skipped
Tag: created | skipped
GitHub Release: created by CI | uploaded manually | skipped
Push: performed | skipped
```

若发生降级、跳过或路径纠正，必须在报告中明确说明。
