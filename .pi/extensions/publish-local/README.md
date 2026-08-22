# publish-local — Pulsar 发布扩展

pi coding-agent 扩展：自动化 Pulsar 的本地发布惯例（版本 bump → dotnet publish → 校验 → 打包 zip），可选发布到 GitHub Release。

位置：`.pi/extensions/publish-local/`（项目级，仓库内共享，`/reload` 后生效）

## 命令

| 命令 | 行为 |
|------|------|
| `/publish` | 仅本地：版本决策 → 构建 → 校验 → 打包 `Artifacts/Pulsar-v{ver}.zip` |
| `/publish gh` | 本地 + GitHub Release（需要 gh CLI） |
| `/publish gh-only` | 跳过构建，用现有 zip 直接发布到 GitHub（本地已发过、补发远端） |
| `/publish minor` | 按 bump 类型（patch/minor/major）推断版本 |
| `/publish 1.6.0` | 显式指定版本号 |

参数可组合，如 `/publish minor gh`。

## 工具（agent 可调用）

`publish_local` — 当你说「发布本地版本 / 打个包 / publish a release」时由 agent 调用。
工具只负责把 `/publish` 排队执行，**所有确认仍由你完成**（LLM 不替你点头）。

## 流程

1. **版本决策**：读取 csproj 当前版本，结合 `git log`（conventional commits）推断建议版本（feat→minor，fix→patch），可改可回车确认
2. **步骤确认**：更新版本号 / 执行构建 / 打包 zip 逐项确认；清空已有 publish 目录、覆盖已有 zip 时显式警告；**GitHub 发布默认不勾选**，仅显式要求时出现
3. **执行**：底部 widget 实时显示构建进度（约 2~5 分钟）
4. **校验**：产物必须包含 `Pulsar.exe` / `Pulsar.pdb` / `*_cor3.dll` / `Assets/`；zip 生成后再次校验条目（对应 `_cor3.dll` 缺失导致无法启动的历史教训）
5. **可选 commit + tag**：`chore: bump version to X.Y.Z` + annotated tag（notes 写入 tag message，CI 的 `--notes-from-tag` 可用）
6. **可选 GitHub Release**：notes 编辑器（自动生成初稿）→ `gh release create`（上传本地 zip）→ push tag + 分支
7. **摘要**：产物路径、大小、GitHub 链接

## 失败恢复语义

- csproj 版本号在 **commit 之前**任何一步失败（构建/校验/打包/中断）都会自动回滚
- commit / tag / GitHub 任一步失败不影响已完成的本地产物（可稍后 `/publish gh-only` 补发）
- 同版本重复发布：跳过 bump，提示目录/zip 覆盖，tag 已存在时自动跳过

## 前置条件

- 本地发布：无
- GitHub 发布：`winget install GitHub.cli` + `gh auth login`（未安装时扩展会提示并跳过该步骤）

## CI 协同

`.github/workflows/release.yml` 增加了「release 已存在则跳过」保护：
扩展先用本地验证过的产物创建 Release，之后 push tag 触发 CI 时 CI 检测到 release 已存在而自动跳过，不会二次创建或覆盖。

## 开发

- `core.ts` 无 pi 依赖，可用 Node 24+ 直接冒烟测试：
  `node .pi/extensions/publish-local/smoke.ts`
- 修改后 `/reload` 生效
- 注意：`core.ts` 仅使用可擦除 TS 语法（无 enum/namespace），以兼容 Node 原生 type stripping
