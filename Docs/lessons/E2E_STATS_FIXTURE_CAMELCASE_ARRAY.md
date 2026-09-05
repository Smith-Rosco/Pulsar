# E2E stats fixture 必须是 camelCase 数组——格式预检防静默空态

**日期：** 2026-09-05  
**严重程度：** 中（E2E 数据态工作流误判）  
**影响范围：** `Pulsar.E2E` 分析页数据态工作流、`Fixtures/PluginUsageStats.json` 的编写者  
**状态：** 已解决（含 AppLauncher 预检加固）

---

## 问题描述

分析页数据态 E2E 首次编写 stats fixture 时写成了**字典**：

```json
{ "com.pulsar.winswitcher": { "executions": 6, "successes": 6 } }
```

运行时行为：`PluginUsageTracker` 用 `System.Text.Json` 反序列化
`List<PluginUsageStats>`（camelCase 命名）→ 字典形状直接抛 `JsonException` →
tracker 回退为空数据 → 分析页**静默显示空态**，数据态断言全部失败且没有明显报错。
排查时容易误判为"页面没渲染"或"断言写错"，实际是 fixture 形状错了。

## 根本原因

- tracker 的持久化格式是 **camelCase JSON 数组**（`List<PluginUsageStats>`，
  元素含 `pluginId`/`executions`/`successes`/`totalDurationMs` 等），
  而手写 fixture 凭直觉写成了"按 pluginId 索引的字典"或 PascalCase。
- 反序列化失败被 tracker 以空数据吞掉（生产路径的合理容错），
  在 E2E 里却变成**无提示的错误**——"容错"与"测试确定性"冲突。

## 解决方案

### 1. 正确格式（`Pulsar.E2E/Fixtures/PluginUsageStats.json`）

```json
[
  { "pluginId": "com.pulsar.winswitcher", "executions": 6, "successes": 6, "totalDurationMs": 108, "lastUsedAt": "2026-09-05T08:00:00+08:00" }
]
```

- 顶层必须是**数组**；每个元素是对象且必须带**字符串** `pluginId`（camelCase）。
- 文件名必须恰好是 `PluginUsageStats.json`，且与被安装的 `Profiles.json` fixture **同目录**
  （AppLauncher 按 `Path.GetDirectoryName(fixturePath)` 找同名文件；命名成
  `stats-sample.json` 之类不会安装，只会静默不生效）。

### 2. 预检加固（本 lesson 配套改动）

`AppLauncher.Launch` 在安装 stats fixture 前调用
`StatsFixtureValidator.Validate(path)` 做结构预检：
根必须是数组、每项必须是对象且带非空字符串 `pluginId`；不符合时**立即抛错**并给出
可操作的错误信息（指向正确格式与常见错误），不再让错误形状静默进入运行阶段。
预检逻辑与生产代码零耦合（E2E 不引用 Pulsar 程序集，用 System.Text.Json 轻量解析）。

## 修改的文件

| 文件 | 变更说明 |
|------|---------|
| `Pulsar/Pulsar.E2E/Fixtures/PluginUsageStats.json` | 改为 camelCase 数组格式 |
| `Pulsar/Pulsar.E2E/Driver/StatsFixtureValidator.cs` | **新增**：fixture 结构预检（数组 + 每项字符串 pluginId） |
| `Pulsar/Pulsar.E2E/Driver/AppLauncher.cs` | 安装 stats fixture 前调用 `StatsFixtureValidator.Validate` |
| `Pulsar/Pulsar.Tests/E2E/StatsFixtureValidatorTests.cs` | **新增**：合法数组/空数组/字典/单对象/PascalCase/缺 pluginId/非 JSON 全覆盖 |

## 架构教训

1. **E2E fixture 形状必须对齐生产序列化格式**，不能凭直觉设计；先看 tracker 的持久化代码
   （`List<PluginUsageStats>` + camelCase）再写 fixture。
2. **测试数据错误要 fail fast**：任何"静默回退为空"的生产容错点，在 E2E 侧都要有对应的
   预检或断言，把"运行时才暴露的形状错误"提前到启动阶段。
3. **约定即文档**：fixture 命名（`PluginUsageStats.json`、与 Profiles.json 同目录）写进 lesson，
   防止下个会话再踩"命名不对没安装"的坑。
