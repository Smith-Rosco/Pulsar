# gitignore `[Dd]ebug/` 静默排除源码目录——Core/Debug 丢失事件

**日期：** 2026-09-05  
**严重程度：** 高（仓库不可编译，跨会话丢失源码）  
**影响范围：** 任何名为 `Debug/`/`debug/` 的源码目录；Pulsar `Pulsar/Pulsar/Core/Debug/`  
**状态：** 已解决

---

## 问题描述

`Pulsar/Pulsar/Core/Debug/DebugModeOptions.cs` 与 `DebugPkiRedaction.cs`
（`--ui-debug` 模式与 PKI 脱敏的核心类型）在开发会话中正常创建、正常编译，
但**从未进入任何 git 提交**：全仓库历史 `git grep -S "DebugModeOptions"` 只有使用点
（`App.xaml.cs` / `AppStartupCoordinator.cs` / 测试），没有定义点。结果 main 分支
**无法编译**（`CS0234: 命名空间"Pulsar.Core"中不存在类型或命名空间名"Debug"`），
且任何新 worktree/CI 都会复现。

排查路径：`git status` 看不到文件 → `git check-ignore -v` 命中
`.gitignore:31: [Dd]ebug/`。

## 根本原因

标准 dotnet `gitignore` 模板的构建产物规则：

```gitignore
# Build results
[Dd]ebug/
[Dd]ebugPublic/
[Rr]elease/
...
[Bb]in/
[Oo]bj/
[Ll]og/
[Ll]ogs/
```

`[Dd]ebug/` 会匹配**任意层级**下名为 `Debug` 或 `debug` 的**目录**——包括
`Pulsar/Pulsar/Core/Debug/` 这样的源码命名空间目录。文件在磁盘上存在、能被编译器
引用，但 `git add -A` / `git commit -All` 静默跳过，构建只在原作者的工作目录通过，
一提交就丢。

## 解决方案

在 `.gitignore` 项目专属区（通用模板规则之后）显式重新包含源码目录：

```gitignore
# Source namespace dirs that collide with generic build-output rules above:
# `[Dd]ebug/` would silently ignore Pulsar/Pulsar/Core/Debug/ (a real source
# folder) — this is exactly why Core/Debug/DebugModeOptions.cs was missing
# from commits and main did not compile. Re-include the source folder.
!Pulsar/Pulsar/Core/Debug/
```

验证：`git check-ignore -v Pulsar/Pulsar/Core/Debug/DebugModeOptions.cs` 应退出
非 0（未被忽略），`git status` 应列出该目录。

本次同时**重建**了这两个丢失文件（从全部使用点还原 API：`FromArgs`/`Disabled`/
`IsUiDebug`/`EnableHotkeyHooks`/管道与配置目录命名对齐 E2E 客户端、`DebugPkiRedaction`
的 `RedactSecretDisplay`/`RedactAccount`），main 恢复可编译（0 警告 0 错误）。

## 修改的文件

| 文件 | 变更说明 |
|------|---------|
| `.gitignore` | 新增 `!Pulsar/Pulsar/Core/Debug/` 重新包含源码目录 |
| `Pulsar/Pulsar/Core/Debug/DebugModeOptions.cs` | **重建**（丢失文件的还原实现） |
| `Pulsar/Pulsar/Core/Debug/DebugPkiRedaction.cs` | **重建**（丢失文件的还原实现） |

## 架构教训

1. **`git status` 看不到 ≠ 文件不存在**：编译通过但提交缺失，先 `git check-ignore -v`
   排查——通用 gitignore 模板的 `[Dd]ebug/`、`[Bb]in/`、`[Ll]og/` 等规则会命中源码目录。
2. **"构建只在原作者目录通过"是危险信号**：新 worktree/CI 一拉就炸 = 有文件没提交。
   提交前用 `git status --short` + 新 worktree 构建自检。
3. **源码命名避开通用忽略词**：命名空间目录避免叫 `Debug`/`bin`/`obj`/`Logs`；
   若必须用，在 `.gitignore` 项目专属区显式 `!` 排除，并写注释说明冲突。
