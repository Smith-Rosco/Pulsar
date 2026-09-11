# ADR-034: Relative-time formatting single owner (unify the drifted ceiling)

**Status**: Accepted（2026-09-11 实施：build 0 警告 0 错误；全量回归与守卫测试见 Change History）
**Date**: 2026-09-11
**Deciders**: Project owner (milo)
**Origin**: 架构审查 round 2026-09-11（`%TEMP%\architecture-review-*.html`，候选 #3「相对时间格式化单一 owner」，`Strong`）

---

## Context

「相对时间」渲染（刚刚 / N 分钟前 / N 小时前 / N 天前 / 具体日期）在本仓存在**三份独立实现**，且「N 天前」的上限已经漂移：

| # | 实现 | 状态 | 天数上限 | 绝对回退格式 | resx 键族 |
|---|---|---|---|---|---|
| 1 | `Helpers/PluginAnalyticsFormatter.cs` `FormatTimeAgo` | 生产可达 | **30** | `yyyy-MM-dd` | `Plugin.*` |
| 2 | `ViewModels/Settings/PluginViewModel.cs` `FormatTimeAgo` | **死代码** | 30 | `yyyy-MM-dd` | `Plugin.*` |
| 3 | `ViewModels/Settings/UsageStatsReadModel.cs` `FormatLastUsed` | 生产可达 | **7** | `MM-dd` | `Settings.Analytics.*` |

### #2 是死代码的实证

`PluginViewModel._formatter`（字段声明 `PluginAnalyticsFormatter?`）是 `readonly`，且在**唯一构造器**（`PluginViewModel.cs:185`）中无条件赋值 → `LastUsedSummary`（`:77`）的 `??` 右支**永不可达**，而私有 `FormatTimeAgo`（`:232`）的唯一调用点就是该右支。同一模式在该类的 8 个成员上重复（`:75/:76/:77/:78/:111/:112/:119/:123`）——`?` 可空标注暗示了一个不存在的可选依赖。

### 同一阶梯，两处漂移

三份实现的**阶梯形状一致**（`<1min` → `<60min` → `<24h` → `<N days` → 绝对日期），算术等价（`DateTime.UtcNow - utc` 与 `ToLocalTime()` 后再减 `DateTime.Now` 在非 DST 边界上相同）。差异只有两类：

- **合法的分面差异**：resx 键族不同（`Plugin.*` 是 `"{0} minutes ago"` 详述风格，供插件详情卡；`Settings.Analytics.*` 是 `"{0}m ago"` 紧凑风格，供密集表格），绝对回退格式相应为全日期 vs 短日期。
- **纯漂移**：天数上限 30 vs 7。没有任何解释能说明这两个场景为何需要不同的「多久算久远」阈值，而且 `PluginUsageStats.DailyStats` 本身保留 30 天数据。

### 覆盖率

三处**几乎零测试**：全仓只有 1 条 `LastUsedFormatted.Should().NotBeNullOrEmpty()`（`UsageStatsReadModelTests.cs`）与 1 条手写字符串赋值（`PluginAnalyticsDetailViewModelTests.cs`）。阶梯边界从未被断言。

---

## Decision

**`Pulsar.Core.Formatting.RelativeTimeFormatter` 是相对时间渲染的唯一 owner。**

1. 新增 `Pulsar/Pulsar/Core/Formatting/RelativeTimeFormatter.cs`：

   ```csharp
   public const int RecentDaysThreshold = 30;   // 唯一写入点

   public static string Format(
       DateTime utcTimestamp, DateTime utcNow,
       ILocalizationService localizationService,
       string keyPrefix, string absoluteFormat);
   ```

   模块内持有**全部**阶梯边界（1 分钟 / 60 分钟 / 24 小时 / `RecentDaysThreshold`）、UTC 口径与桶顺序。调用点只声明两件**分面**的事：resx 键族前缀与绝对日期格式。

2. **天数上限统一为 30**（owner 裁定，2026-09-11）。`Settings.Analytics` 表格在 1–29 天显示相对时间（`29d ago`），第 30 天起显示 `MM-dd`。
   - 选 30 的理由：三份实现中 2 份已是 30；且与数据层 `DailyStats` 保留 30 天 / `RecentExecutions` 用 6 天窗口的既有口径同族，而不是凭空多出第三个数字。
   - **这是唯一一处用户可见文案变化**（分析页表格「距上次使用 8–29 天」的行从 `08-30` 变为 `12d ago`）。

3. **分面差异保留为参数，不进模块**：键族前缀（`PluginAnalyticsFormatter.RelativeTimeKeyPrefix` / `UsageStatsReadModel.RelativeTimeKeyPrefix`）与绝对格式（`yyyy-MM-dd` / `MM-dd`）。理由：这是「详情卡 vs 密集表格」的真实排版差异，不是漂移。**但阈值没有参数可传 —— 调用点无处安放第四个数字**，这正是本次收敛的机制保证。

4. 三处实现收敛：
   - `PluginAnalyticsFormatter.FormatTimeAgo` 改为单行委托；
   - `PluginViewModel.FormatTimeAgo` **删除**，`_formatter` 字段去掉可空标注，8 处不可达 `??` 右支一并删除（`?`/回退分支消失后不变量才只有一个入口）；
   - `UsageStatsReadModel.FormatLastUsed` 改为委托，并把时钟读数从方法内部的 `DateTime.Now` 改为由 `Project()` 已算出的 `now` 传入（该方法已有可注入 `_clock`）——首次成为确定性可测的纯投影。

5. **缺失键不再靠内联英文兜底**：`ILocalizationService` 缺键时返回键名本身（`LocalizationService.cs:48`），可被守卫发现。改为由守卫测试断言「formatter 请求的每个键在两个 resx 都存在」。

---

## Alternatives Considered

**A. 保留三份实现，用 3 条守卫测试钉住一致性。** 守卫能发现漂移，但**不消除**——三个阶梯仍各自是权威，下一次改动仍要同时改三处；且 #2 那份死代码会一直在。**拒绝**。

**B. 阈值做成显式入参（行为零变化）。** 把漂移从「事故」变成「声明」，但两处仍可合法地不同，等于承认「多久算久远」没有答案。owner 明确选择统一（见 Decision 2）。**拒绝**。

**C. 统一到 7 天。** 会让插件分析卡片/详情在第 7 天起改显具体日期，改动面比 A 方案更大且无解释优势。**拒绝**。

**D. 把整个 `PluginAnalyticsFormatter` 合并进 `RelativeTimeFormatter`。** `PluginAnalyticsFormatter` 还持有用量摘要 / 健康徽章 / 成功率等**非时间**职责，与相对时间无关。只收敛时间阶梯。**拒绝**。

**E. 让共享模块直接依赖 `ILocalizationService` 并吞掉键族差异（单一键族）。** 会把「详情卡详述 / 表格紧凑」的真实排版差异抹平，属于为收敛而收敛。**拒绝**。

## Consequences

- 正向：一个阶梯、一个阈值、一处改动；`PluginViewModel` 少 9 处死代码（1 个方法 + 8 个不可达分支）；`UsageStatsReadModel` 的投影在注入时钟下确定。
- 交互变化：分析页表格 8–29 天区间的「上次使用」列由绝对日期变为相对时间。**属可感知文案变化，已由 owner 裁定**（Decision 2）。
- 测试：新增 `Pulsar.Tests/Core/Formatting/RelativeTimeFormatterTests.cs`（阶梯边界矩阵 + 阈值上下沿 + 未来时间钳制 + 键族参数化 + 两条 resx/占位符守卫）、`Pulsar.Tests/Helpers/PluginAnalyticsFormatterTests.cs`（`Plugin.*` 族首次有覆盖）、`UsageStatsReadModelTests` 两条消费级断言（8 天仍相对 —— 钉死 7 天漂移不会回潮）。
- **契约**：`PluginAnalyticsFormatter.RelativeTimeKeyPrefix` / `UsageStatsReadModel.RelativeTimeKeyPrefix` 是 public const，被守卫测试消费以绑定「前缀 → 两个 resx 的四个键」；新增使用相对时间的分面必须提供这四个后缀的键。

## References

- 架构审查 round 2026-09-11 候选 #3（`Docs/journal/NEXT.md`）
- `Pulsar/Pulsar/Core/Formatting/RelativeTimeFormatter.cs`（唯一 owner）
- `Pulsar/Pulsar/Helpers/PluginAnalyticsFormatter.cs:83-89` · `Pulsar/Pulsar/ViewModels/Settings/PluginViewModel.cs:45,75-78,111-112,119,123` · `Pulsar/Pulsar/ViewModels/Settings/UsageStatsReadModel.cs:20-27,146,187,222,439-450`
- `Pulsar/Pulsar/Services/PluginUsageTracker.cs:94`（`LastUsed` 的 UTC 口径来源）
- `Pulsar/Pulsar/Core/Localization/LocalizationService.cs:30-73`（缺键返回键名）
- 同族先例：ADR-030（半径单一公式）、ADR-033（对话框目录单一登记面）
