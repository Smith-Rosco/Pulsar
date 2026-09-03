# publish-local — Pulsar 发布扩展

pi coding-agent 扩展 + skill：自动化 Pulsar 的发布惯例。

**架构（薄命令 + skill）**：`/publish` 只负责把任务注入会话，AI 按
`.agents/skills/publish/SKILL.md` 的流程用 bash/PowerShell 工具逐步执行。
执行过程**全程在会话上下文中可见**，遇到问题 AI 可自行排障修复，关键步骤（版本、GitHub 发布、notes、CHANGELOG）由 AI 询问你确认。

位置：`.pi/extensions/publish-local/`（项目级，仓库内共享，`/reload` 后生效）

## 命令

| 命令 | 行为 |
|------|------|
| `/publish` | 本地发布（local-version 模式）：版本建议 → 构建 full+portable → 校验 → 打包 |
| `/publish gh` | release 模式：版本 + notes + CHANGELOG → commit/tag（notes 写入 tag message）→ push → CI 构建并创建 GitHub Release |
| `/publish gh-only` | 跳过构建/打包/commit/tag，用现有 zip 补发 GitHub |
| `/publish minor` | 按 bump 类型（patch/minor/major）推断版本 |
| `/publish 1.6.0` | 显式指定版本号 |

参数可组合，如 `/publish minor gh`。三种模式的详细边界见 SKILL.md §0。

## 典型使用

| 场景 | 命令 | 说明 |
|------|------|------|
| **本地发布** | `/publish` | AI 建议版本号（与你确认）→ 逐步骤执行 → 构建并打包 `Artifacts/Pulsar-v{ver}-{full,portable}.zip` |
| 指定版本本地发布 | `/publish 1.6.0` | 跳过版本建议，直接发布 1.6.0 |
| **正式发布到 GitHub** | `/publish gh` | release 模式：notes 写入 tag message → push → CI 创建 Release（本地不重复构建） |
| 指定版本 + GitHub | `/publish 1.6.0 gh` | 同上，锁定版本 |
| 只补发 GitHub（本地已发过） | `/publish gh-only` | 直接用 `Artifacts/Pulsar-v{ver}-*.zip` 补发 Release + push |
| 对 agent 口述 | 「发布 v1.6.0 到 GitHub」 | agent 调用 `publish_local` 工具（`github: true`）排队 `/publish 1.6.0 gh` |

**GitHub 发布前置条件**：`winget install GitHub.cli` + `gh auth login`。gh 不在 PATH 时 skill 会自动用 `C:\Program Files\GitHub CLI\gh.exe`。

## 为什么是 skill 而不是黑盒命令

早期版本把整个流程写在扩展 handler 里（TUI 对话框确认 + widget 进度），执行过程**不进入会话上下文**：AI 看不到任何步骤输出，出错只能弹错误通知，无法参与排障。改成 skill 后：

- AI 读 skill 获得完整流程 + 已知坑位（zip 的 PowerShell 坑、gh 的 `#` 路径坑、tag 推送顺序坑）
- 每步用脚本执行，输出全程可见，失败时 AI 读日志、查状态、修复后从失败阶段重试
- 关键决策（版本、GitHub 发布、notes 内容）由 AI 在对话中向你确认

## 开发

- `core.ts` 只保留 `/publish` 参数解析（`parseArgs`）；版本推断、notes 生成、zip 回退链、
  产物校验等纯逻辑均已迁移到 `.agents/skills/publish/scripts/` 的 PowerShell 脚本，
  由脚本内置断言保障正确性。冒烟测试：`node .pi/extensions/publish-local/smoke.ts`
- skill 在 `.agents/skills/publish/SKILL.md`，修改后无需 reload（下次任务自动加载）
- 修改扩展后 `/reload` 生效
- 注意：`core.ts` 仅使用可擦除 TS 语法（无 enum/namespace），以兼容 Node 原生 type stripping
