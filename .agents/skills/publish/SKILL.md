---
name: publish
description: Pulsar 发布与打包流程。支持 local-artifact、local-version 和 release 模式；按模式执行版本决策、绝对路径构建、产物校验、ZIP 打包以及可选的 commit/tag/GitHub Release。用户要求发布、打包、publish 或运行 /publish 时使用。
---

# Pulsar 发布流程（Python 单入口）

所有可执行操作统一由 `scripts/publish.py` 完成（PowerShell 旧脚本已于 2026-09-09 移除）。单入口优势：内置 env 自愈（winreg 恢复沙箱剥落的 `ProgramFiles`/`APPDATA` 等系统变量）、结构化 fail-fast 断言、三级报告（L1 结果 / L2 详情 / L3 JSON）。

```bash
python scripts/publish.py <子命令> [参数]
```

从仓库根目录执行；脚本内部基于 `git rev-parse --show-toplevel` 生成绝对路径。

## 子命令一览

| 子命令 | 作用 | 主要参数 |
|---|---|---|
| `info` | 版本决策辅助：仓库根、当前版本、最近 tag、自 tag 以来的提交、下一构建号 | - |
| `set-version` | 写入 csproj `<Version>` 并同步 `<FileVersion>`/`<AssemblyVersion>`（x.y.z.0）；防降级与同版本重发 | `--version 1.9.0 [--allow-downgrade]` |
| `build` | 构建 full + portable 两个发布产物并校验；本地默认不编译 Setup.exe | `--version 1.9.0 [--build 2] [--installer]` |
| `pack` | 压缩 ZIP、校验（PK 魔数 + 体积阈值 + build-info）、终态收敛（成功后删除 `publish\v<ver>\` 产物目录） | `--version 1.9.0 [--build 2] [--installer] [--keep-publish-dirs]` |
| `all` | `build` + `pack` 一口气完成 | 同 build/pack |
| `changelog` | 把 `CHANGELOG.md` 的 `[Unreleased]` 段固化为 `## [X.Y.Z] - 日期` 并插入新空段；无真实条目时拒绝 | `--version 1.9.0` |
| `tag` | commit 版本相关文件 + 创建 annotated tag（notes 写入 tag message，覆盖 `core.commentChar=§`） | `--version 1.9.0 --notes-file <路径>` |
| `watch` | 等待 release.yml CI 完成（含 run 注册轮询 ~60s）、验证 Release 资产、导出 body 供核对 | `--version 1.9.0` |
| `edit-notes` | 修正 GitHub Release body 为完整 notes | `--version 1.9.0 --notes-file <路径>` |
| `iscc-probe` | 探测 ISCC.exe 可用性（installer 回退路径诊断） | - |

全局选项：`--dry-run`（打印计划不执行）、`--json`（stdout 直出 L3 JSON）。

## 0. 发布模式

先判断模式。若用户没有明确说明，询问并说明影响：

| 模式 | 行为 |
|---|---|
| `local-artifact` | 使用现有版本构建和打包；不修改版本号，不 commit，不 tag，不推送 |
| `local-version` | 使用确认后的版本号更新项目版本，构建和打包；不 commit，不 tag，不推送 |
| `release` | 更新版本、生成 release notes，用户确认后 commit/tag 并 push；构建、打包与 GitHub Release 由 CI 自动完成，本地**不重复**构建打包 |

用户说"发布一个本地版本"时，默认使用 `local-version`。

### 本地构建号（x.y.z.n，方案 B）

- **csproj 永远只存 `x.y.z`**。构建号经 `build --build 2` 在 publish 阶段以 MSBuild 属性覆盖，产物命名使用四位版本（`Pulsar-1.9.1.2-full.zip`），多次构建并存不覆盖。
- 产物内附 `build-info.txt`（版本、构建号、channel、构建时间、commit hash）。
- 下一构建号取 `info` 输出的 `Next local build number`（已用最大值 + 1）。
- `release` 模式**不使用**构建号：正式版本语义始终是三位。

## 1. 版本决策

运行 `info`：

```bash
python scripts/publish.py info
```

- 用户给出显式版本 → 直接使用该版本。
- 用户未给版本且是 `local-artifact` → 使用当前版本。
- 用户未给版本且是 `local-version` 或 `release` → 依据提交记录建议：含 `feat` 建议 minor，含 `fix` 建议 patch，否则保守 patch。
- 向用户展示建议版本和依据，得到确认后再 `set-version`。

## 2. 构建与打包（local-artifact / local-version）

```bash
python scripts/publish.py all --version 1.9.0            # build + pack 常规
python scripts/publish.py all --version 1.9.1 --build 2  # 本地构建号
```

脚本内置断言（任一失败即 fail-fast，输出结构化 `{name, passed, detail}`）：
- `full`：`Pulsar.exe` **≥ 50 MB**（.NET 8 单文件自包含嵌入运行时，勿再校验 cor3）、`Pulsar.pdb`、`Assets\`。
- `portable`：`Pulsar.exe` **< 20 MB**（framework-dependent，误含运行时会超阈值）。
- 两个 ZIP 以 `PK` 开头；两个目录含 `build-info.txt`。
- **本地终态（2026-09-07 收敛）**：发布完成后 `artifacts\` 根只有 `Pulsar-$version-{full,portable}.zip` 两个文件；`publish\v<ver>\` 自动删除（`--keep-publish-dirs` 保留供冒烟/调试）。

**installer 回退路径（ADR-026）**：Setup.exe / Standalone zip / SHA256SUMS 是 CI 职责，本地默认不产出。CI 不可用需手动上传时，build 与 pack **都加** `--installer`（ISCC 不可用时非致命跳过并警告）。

portable 版构建后建议冒烟测试（启动 6 秒不崩溃即通过）。

## 3. Release notes（release 模式）

基于用户可感知的提交撰写简洁中文说明，固定章节（无内容章节省略）：`### 新功能` / `### 修复` / `### 性能优化` / `### 其他`。展示并等待用户确认后保存为 `.md` 文件。

**tag message 必须是用户确认过的完整 release notes**，不要只写一行摘要——CI 用 GitHub API 读取 annotated tag message 生成 GitHub Release body，tag message 就是发布页展示的 notes。

### 更新 CHANGELOG（release 模式）

`local-artifact` / `local-version` 模式不改 CHANGELOG。release 模式发布前：

1. 检查 `[Unreleased]` 段：若是"暂无/TODO"占位符，先根据本次提交补全。
2. `python scripts/publish.py changelog --version 1.9.0` 固化，结果纳入版本 commit。

## 4. Commit 与 tag（release 模式）

`local-*` 模式跳过本节，并在最终报告中明确 `Commit: skipped`、`Tag: skipped`。

```bash
python scripts/publish.py tag --version 1.9.0 --notes-file notes.md
```

脚本内部处理（勿手写重复）：commit 版本文件（`chore(release): bump version to X`）→ notes 重写为无 BOM UTF-8 → `git -c core.commentChar=§ tag -a`（防止 `###` 章节标题被 git 注释剥离）→ tag 已存在则停止不覆盖 → 校验 tag message 首三字节为 `###` 而非 BOM。

### push

按仓库约定由用户自己执行：

```bash
git push origin main && git push origin v1.9.0
```

## 5. GitHub Release（release 模式）

**主路径（默认）：不手动上传，交给 CI。** push tag 后 `release.yml` 自动触发：

```bash
python scripts/publish.py watch --version 1.9.0
```

脚本等待 CI 完成、验证 `isDraft=false` 且 assets 齐全、导出 Release body 供核对（直接字节落盘，绕过终端 GBK 重解码）。用 Read 工具核对导出的 body：应显示完整中文 notes。

若 body 非完整 notes，修正后重跑：`python scripts/publish.py edit-notes --version 1.9.0 --notes-file notes.md`（不重建 tag）。

## 6. 回退路径（仅当 CI 不可用或用户明确要求本地上传）

本地构建打包（第 2 节，installer 需 `--installer`）后手动 `gh` 上传。因仓库路径包含 `#`，先把 ZIP/notes 复制到不含 `#` 的临时目录再调用 `gh`，详见 `Docs/lessons/GH_CLI_HASH_PATH_BUG.md`。

## 7. 排障

- **优先重跑对应子命令**：publish.py 断言失败会输出具体 `{name, detail}`，先读完整输出再定位。
- **沙箱/新终端下 dotnet 报 NU1301 或 ConfigurationDefaults 异常**：publish.py 已内置 winreg env 自愈（含从 `SystemDrive` 推导 `ProgramFiles`）；若在 publish.py 之外手动跑 dotnet，用 `scripts/dotnet-env.sh` 修复环境。
- 历史编码/编码坑（BOM、commentChar、`--notes-from-tag` 退化）已固化进 `tag`/`watch` 子命令，不再需要人工干预；修改脚本时不要回退这些防护。
- **`changelog` 子命令曾把 HTML 注释模板误当真实段**（2026-09-10 v1.14.0 暴露）：`CHANGELOG.md` 顶部的模板注释里有一个缩进 2 空格的示例 `## [Unreleased]`，旧实现的 `re.search` / `str.replace` 都命中它 → 注释模板被改写成版本段，真实 `[Unreleased]` 段纹丝不动，且脚本仍报 PASS。已修：定位前按行结构屏蔽 HTML 注释 + 行首锚定 `^`；替换时只改标题行本体、body 原样保留（`re.M` 下 lookahead 会把行尾 `\n` 划进 body，重建串会吃掉它 → 段标题粘连、内容丢失）。**改动 `cmd_changelog` 时不要回退这两条防护。**
- Release 已存在或 tag 已存在：停止并询问用户，不删除、不覆盖。

## 8. 完成报告（L1：只报结果）

最终报告**只展示结果，不展示技术细节**。默认终端输出即 L1 报告，直接转述即可：

- ✅ 成功横幅（模式 + 版本）
- 交付物清单（ZIP / Setup.exe / Standalone / SHA256SUMS：路径 + 大小）
- 产物位置（本地目录或 GitHub Release 链接）
- 跳过项（如 `Commit: skipped`、`Tag: skipped`、`Changelog: skipped`）
- 指向 `report.json`（需要细节时再查 L2/L3）

若发生降级、跳过或路径纠正，必须在报告中明确说明。
