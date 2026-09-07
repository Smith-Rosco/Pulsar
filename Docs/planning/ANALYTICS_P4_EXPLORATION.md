# P4 探索：分析页进阶项（空态 CTA / 排序状态可视化 / 插件下钻 / 自动洞察）

> 探索文档（2026-09-05，worktree `feat/analytics-ui-polish`）。只读现状 + 方案与工作量，未实现。
> 结论先行：**自动洞察已基本落地**（推荐引擎 + UI 都在），其余三项中**排序可视化**最小、**空态 CTA** 依赖组件已齐、**插件下钻**工作量最大。建议排序可视化 + 空态 CTA 先行（合 0.5-1 天），下钻单独评估。

---

## 1. 空态 CTA（最低优先，工作量小）

**现状（证据）**：
- `Views/Controls/EmptyState.xaml(.cs)` 已完整支持 CTA：`ActionText` / `ActionIcon` / `ActionCommand` / `ActionButtonStyle` / `ActionVisibility` / `Hint`（DP 注册见 EmptyState.xaml.cs:19-35）。
- 已有两个成熟用例：`SettingsSlotsPage.xaml:127-148`（ActionText=AddFirstSlot + `PulsarPrimaryButtonStyle` + AddSlotDialogCommand）、`SettingsPluginsPage.xaml:593-601`（ActionText=ClearSearch + 条件 ActionVisibility）。
- **分析页空态只传了 Icon/Title**（`SettingsAnalyticsPage.xaml:79-82`），无 Hint 无 Action —— 缺口即在此。

**方案**：空态加 `Hint`（"使用径向菜单执行插件后，这里会展示使用统计"）+ 一个 CTA。
- CTA 候选：a) "去插件管理"→ 跳 Plugins 页；b) "配置插槽"→ 跳 Slots 页；c) 无跳转，纯提示。**建议 a + Hint**：没统计的常见根因是插件未启用/未配置，跳 Plugins 页最直接。
- 跳转机制：`SettingsShellViewModel` 是单例（App.xaml.cs:369），其 `NavigateAsync(pageId, userInitiated: true)` 走 `_navigationGuard`（SettingsShellViewModel.cs:44-65）。注入到 `SettingsAnalyticsPageViewModel`（transient）无循环依赖（Shell 不依赖 Analytics VM）。
- 页面 ID 常量：`SettingsPageIds.Plugins`（SettingsPageCatalog.cs:46）。

**工作量**：VM 注入 1 个构造参数 + 1 个 RelayCommand（~15 行）；XAML ~5 行；resx 双语 2 键。**约 0.5-1 小时**。风险：导航 guard 在空态无脏状态，安全。

---

## 2. 排序状态可视化（最小，纯 XAML）

**现状（证据）**：
- 排序逻辑完整：`SettingsAnalyticsPageViewModel.SetSort(string)`（VM:164-179）按列切换/翻转；`SortColumn`/`SortAscending` 为 `[ObservableProperty]`（VM:69-73）。
- `UsageStatsReadModel.ApplySort`（UsageStatsReadModel.cs:163-188）支持 4 列（Executions/SuccessRate/Duration/LastUsed）升降序，重排 Rank。
- **缺口**：表头 5 个 TextBlock（`AnalyticsSortHeaderText` 样式，SettingsAnalyticsPage.xaml:326-363）只有 hover 强调色，**无任何当前排序列/方向的视觉指示**——用户点排序后看不到状态。

**方案**：表头改 `Grid`（TextBlock + 箭头 SymbolIcon），箭头 `Visibility` 绑定当前列与方向。
- 实现：每列表头箭头控件绑定 `SortColumn`（列枚举名比较）+ `SortAscending`，可用 `MultiDataTrigger` 或一个小型 `IValueConverter`（入参 column 字符串，比较当前列 → 返回 ▲/▼/Collapsed）。不引新依赖。
- 注意 `SetSort` 以字符串参数工作（XAML CommandParameter="Executions" 等），枚举名与 `SortColumn` 同名，比较时直接 `SortColumn.ToString()`。

**工作量**：1 个 Converter（或触发器组）+ XAML 每列 ~4 行。**约 1-2 小时**。风险：无（纯展示，不动逻辑）。**建议最先做**——与上轮对齐修复同属"列表观感"收尾。

---

## 3. 插件下钻（工作量最大，建议单独评估）

**现状（证据）**：
- `AnalyticsItem`（Models/UsageStatsDisplayModels.cs:31-63）已携带大量未展示数据：7 天 `TrendData`（DailyTrendItem，含 BarHeight 迷你图）、`SlotUsage` 字典、`TodayFormatted`/`RecentFormatted`、`TaskModeCount`/`ActionModeCount`、`SlotBreakdown`/`ModeSummary`。
- 对话框基础设施齐全：`DialogService.ShowCustomAsync<T>()` + `DialogSizeConstraints`（DIALOG_SYSTEM.md），现有范例 `PluginLogViewerViewModel`（ViewLogs 命令，VM:181-197）——可选注入 `IPluginLogService?`/`IDialogService?` 的既有模式。
- 元数据源：`IPluginRegistry.GetDescriptor(pluginId)` → `PluginDescriptor`（Core/Plugin/PluginDescriptor.cs:12-52：Description/Version/Author/Icon/Tier/Permissions…）。
- 单插件推荐已存在：`IPluginRecommendationEngine.GetRecommendationsForPlugin(pluginId)`（IPluginRecommendationEngine.cs）。

**方案**：卡片点击（或加"详情"按钮）→ 新对话框 `PluginAnalyticsDetailViewModel`：头部（图标/名/版本/作者/描述）+ 大号趋势图（TrendData）+ 槽位分布（SlotUsage）+ 模式分布（Task/Action）+ 单插件推荐（GetRecommendationsForPlugin）+ "查看日志"入口。
- 需按 DIALOG_SYSTEM 规则新建：VM（ViewModels/Dialogs/）+ UserControl（Views/Dialogs/Contents/）+ **DialogHostWindow.xaml 注册 DataTemplate** + `DialogSizeConstraints.Large`。
- 数据全在内存快照，无新查询；仅需把 AnalyticsItem 传给 VM。

**工作量**：新对话框全链路（VM + View + DataTemplate + 测试）**约 2-4 小时**。收益：把已采集未展示的数据（趋势/槽位/模式）真正用起来。风险：中（新增 UI 面需 E2E 或人工 QA；注意 AGENTS.md：对话框必须指定 DialogSizeConstraints + 注册 DataTemplate）。

---

## 4. 自动洞察（已基本实现，探索结论：补强为主）

**现状（证据）——已落地**：
- `PluginRecommendationEngine`（Services/PluginRecommendationEngine.cs）已有 5 类检查器：未使用插件（>30 天）、高错误率（>20%）、熔断、不活跃（>7 天且 ≥50 次）、槽位优化（FavoriteSlot≥3 且 ≥100 次）。全部基于 `IPluginRegistry`/`IPluginUsageTracker`/`IPluginHealthMonitor` 纯查询，无副作用。
- 页面底部已有 "Recommendations" 卡片（SettingsAnalyticsPage.xaml:532-644）：严重度图标（Info/Warning/Error 三色）+ 插件名/标题/消息 + 按 `ActionCommand` 条件显示 Disable（Danger 样式）/ViewLogs（Secondary 样式）按钮；VM 已接 `GetRecommendations()`（VM:118-125）。
- resx 双语键齐备（`Plugin.Recommendation.*`，Strings.resx:1942+）。

**可补强点（按价值排序）**：
1. **趋势型洞察（缺口最大）**：现引擎是"状态快照"型（当前错误率/闲置天数），无时间对比。可加：近 7 天 vs 前 7 天的执行量/成功率 Δ（如 "command 成功率 ↓12%：96% → 84%"）。数据源 `DailyStats`/`TrendData` 已具备（UsageStatsReadModel.cs:329-346 有 7 天趋势构建逻辑，可抽公共方法）。
2. **OptimizeSlotPlacement 无动作按钮**（小坑）：引擎产出 `ActionLabel=""` 且未设 `ActionCommand`（PluginRecommendationEngine.cs:202-221），而 UI 按钮按 `ActionCommand` 硬编码显示 Disable/ViewLogs（XAML:607-635）——该推荐**两个按钮都不显示**。可补一个"前往插槽"动作或干脆去掉该检查。
3. **排序/去重**：按 Severity（Error > Warning > Info）排序，同插件合并。
4. **小瑕疵（顺带修）**：`DisablePlugin`/`ExportCsv` 的 catch 分支 ErrorMessage 是硬编码英文（VM:217、241），违反本地化约束——应走 `_loc` + resx。

**工作量**：趋势洞察 1 个检查器 + 公共趋势计算抽取 + resx + 单测（引擎纯逻辑可测）**约 2-3 小时**；其余各 0.5 小时内。

---

## 5. 汇总与建议

| P4 项 | 现状 | 缺口 | 工作量 | 建议 |
|---|---|---|---|---|
| 排序状态可视化 | 排序逻辑全有 | 无视觉指示 | ~1-2h | ✅ 做（最小，与上轮对齐收尾同批） |
| 空态 CTA | EmptyState CTA 组件齐 | 分析页未接线 | ~0.5-1h | ✅ 做（组件现成） |
| 自动洞察 | 引擎+UI 已落地 | 无趋势对比；1 个小坑 | 2-3h（趋势）/0.5h（修坑） | 🟡 补强趋势洞察 + 修 OptimizeSlotPlacement 坑 |
| 插件下钻 | 数据/对话框基建齐 | 无详情对话框 | 2-4h | 🟡 单独评估（收益高，成本最高） |

**建议顺序**：排序可视化 + 空态 CTA（合计 <0.5 天）→ 趋势洞察 + 修坑（0.5 天）→ 下钻（单独排期）。全部改动遵守：resx 双语、Pulsar 按钮样式、ApplyTheme 在 InitializeComponent 后、对话框注册 DataTemplate。
