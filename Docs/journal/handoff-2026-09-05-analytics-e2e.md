# Handoff — 使用分析页 P0/P1 + E2E 验证（2026-09-05）

> 交接给新豆包会话。**先读本文件，再按 AGENTS.md §8 会话仪式读 journal**（`Docs/journal/NEXT.md` + `Docs/journal/2026-09-05.md` 尾部）。

## 2026-09-05 19:xx 追加（本会话完成：P1 对齐修复 + E2E scroll/dump 步骤）

**P1 对齐真相与修复（已提交 `1f4dedc`，UIA 权威验证）**：
- 根因：Wpf.Ui `CardControl` 模板是 3 列 Grid（Icon(Auto) | Header(*) | Content(Auto)），Content 槽内容**按内容宽贴卡片右缘**——统计卡片体（徽章+度量）被右对齐到 851（卡片 638..1219 内），度量列被压成内容宽 76px，与表头列（# 48 + 4×125）永不对齐。
- 修复：把卡片体网格移入 `<ui:CardControl.Header>`（星列，页面热力卡同款模式），col2 空 → 卡片体满宽 549 → 徽章 654..702 对 `#`、度量 702/827/953/1077 对表头 702/827/953/1078 ✓。
- 验证：E2E v6 `dump` UIA 树三卡全对齐（前后差 ≤1px）；截图像素/OCR 复核一致。注意**本页所有 CardControl 的 Content 槽都是右对齐贴边**（KPI/热力行/推荐行）——用户只投诉统计列表，其余保持原样。
- 遗留知识：树坐标 ≈ 截图物理像素 ÷1.5（DPI），窗口位置每次运行会漂移。

**E2E scroll + dump 步骤（已提交 `71e70de`）**：
- 截图不自动滚动 → 新增 `scroll` 步骤（按 AutomationId 找 ScrollViewer，UIA ScrollPattern `SetScrollPercent(-1, vertical)` 降级水平，Sleep 250ms 等重布局）；工作流在 screenshot 前插 scroll（13→14 步）。
- 新增 `dump` 步骤：成功路径导出完整 UIA 树到 artifacts（诊断神器，本次定位全靠它）。`list-steps` 已更新；parser 测试 +2。
- 验证：build 0 警告 0 错误；E2E 32/32；全量 **1072/1072**；数据态工作流 v6 PASS 22.1s（PID 39856）。

**分支状态**：`feat/analytics-ui-polish` 现有 7 个提交（上会话 5 + 本会话 2），工作区干净；**合并回 main 未执行**（WorkBuddy 并行中，需协调）。
**剩余**：P3 低干扰模式、P4 空态 CTA 等（未批准）；合并后 journal/NEXT 由 main 侧更新。

---

## 0. 当前最重要的事（先做这个）

- **WorkBuddy 正在原始仓库（`E:\8_Project\10_C#\Pulsar_Project`，main worktree）同步 apply changes。你绝不能在 main worktree 里改任何文件。**
- 所有后续工作必须在**独立 worktree** 中完成：`git worktree add -b feat/<name> E:\...\Pulsar_Project_wt`（AGENTS.md §10：一条分支一个 worktree，journal/CHANGELOG 只在 main 提交，feature worktree 通过 `git show main:Docs/journal/...` 读 journal）。
- main worktree 工作区有**其他并行工作流的未跟踪文件**，一律不要动、不要提交：`Docs/reports/2026-09-05-STARPIE_COMPARISON.html`、`openspec/changes/2026-09-05-*`（5 个目录）。

## 1. 已完成并提交（commit `ead2499` on main，25 文件 +916/−161，勿重做）

- **分析页 P0/P1**（用户已批准）：
  - 时间筛选死 UI 修复：ComboBox 改 `SelectedValuePath="Tag"` + `SelectedValue` 绑定 enum，删除死命令 `SetFilter`
  - ViewLogs 落地：VM 可选注入 `IPluginLogService?`/`IDialogService?`，打开 `PluginLogViewerViewModel` 对话框
  - 自然周/月口径：ThisWeek=本周一零点（`StartOfWeek`）、ThisMonth=当月 1 号；趋势图本地日期键（原 UtcNow 在 UTC+8 凌晨错位一天）
  - 热力图 + 今日/本周 KPI 随筛选联动：`PluginUsageStats` 新增 `DailySlotUsage`/`DailyHourlyUsage`（向后兼容旧 JSON），tracker 记录/30 天清理/深克隆同步
  - 暗色空态文字 bug：`EmptyState.xaml` Title TextBlock 补 `Foreground="{DynamicResource TextFillColorPrimaryBrush}"`（顺带修复 5 处复用点）
- **E2E 打通**（debug 作用域，生产路径零行为变化）：
  - `DebugCommandServer` 新增 `open-settings` 命令；`SettingsWindow` 导航项加 `Pulsar.Settings.Nav.<Id>`；分析页加 `Pulsar.Settings.Analytics.EmptyState/FilterCombo/RefreshButton`
  - `PluginUsageTracker` 在 `--ui-debug` 下重定向到 `%AppData%\Pulsar.Debug\PluginUsageStats.json`（绝不碰用户真实统计）
  - E2E 新增 `command` 步骤类型；`AppLauncher` 支持同名 `PluginUsageStats.json` fixture（无则删残留保证空态确定性）；修复 `WorkflowRunner` 未创建 `artifacts\<runId>` 目录 bug
  - 新工作流/fixture：`Workflows/settings-analytics-empty-dark.json`、`settings-analytics-data-dark.json`、`Fixtures/default-profiles-dark.json`、`Fixtures/PluginUsageStats.json`；两轮均 PASS（23.5s/20.9s）
  - 验证结论：暗色空态白字（像素统计 max=255）；数据态 KPI 70/3/12/50 与手算一致（自然周运行时正确）
- 测试：全量 1060/1060 绿；`WorkflowParserTests` 13/13（含 command 步骤）
- journal：`Docs/journal/2026-09-05.md` 已追加 3 个 Session 块；NEXT.md 已更新

## 2. 待办（按优先级）

### P1 — UI 对齐/美化（用户已给图，最高优先）
- 用户截图：`Docs\media\screenshots\使用分析末尾.png`（同目录有 `-small.png` 缩小版）。问题：**统计列表上方的表头行（# / 执行次数 / 成功率 / 平均时长 / 最后使用）按钮不对齐、不美观**，如"执行次数"按钮偏左、"成功率"等四个按钮宽度与下方卡片列不对齐。对应 `Views/Pages/SettingsAnalyticsPage.xaml` 的 Sort Column Headers 段（Grid 5 列：第一列 Auto+# 32px，其余 `*` + `PulsarSecondaryButtonStyle` 按钮 `HorizontalAlignment="Left"`）。下方卡片为 `UniformGrid Columns=3`（执行次数/成功率/平均时长），与表头 4 列结构错位。修复方向：表头与卡片列宽统一（建议表头也用 3 列对齐卡片，或卡片改 4 列加"最后使用"列），去掉按钮化表头改用普通可点击文本（视觉更轻）。
- 用户手动加的 NEXT.md 待办（同方向）：**设置页保存按钮改为图标**（当前是"保存更改"文字按钮，`SettingsWindow.xaml` TitleBar 右侧）；**设置页各种长文本描述精简或将详情藏入 tooltip**（保证观感简洁有序）。
- 注意 AGENTS.md 约束：本地化键走 resx 双语、按钮用 `PulsarPrimaryButtonStyle` 等（禁用 `Appearance="Primary"`）、ApplyTheme 在 InitializeComponent 之后。

### P2 — E2E 经验固化为文档 + 代码加固（用户明确要求）
- 本次踩坑（写进 `Docs/lessons/` 每坑一文件 或 `Docs/ops/` 手册，建议两者结合）：
  1. `Pulsar.Settings.NavView` 的 AutomationId 在 Wpf.Ui NavigationView 复合控件上**不暴露** → 用真实存在的 `Pulsar.Settings.SaveChangesButton` 断言窗口
  2. stats fixture 首版写成字典 → 实际 tracker 持久化是 **camelCase JSON 数组**（`List<PluginUsageStats>`，Save/Load 均 `JsonNamingPolicy.CamelCase`）→ 反序列化抛 JsonException、页面静默回退空态
  3. fixture 命名必须与 AppLauncher 约定一致（同名 `PluginUsageStats.json`，否则不安装）
  4. E2E 脚手架 run 目录 bug（已修，勿重报）
- 代码加固建议：`AppLauncher` 安装 stats fixture 前做**格式预检**（`JsonDocument` 解析：根必须是数组且每项含 `pluginId` 字符串；注意 E2E 项目不引用 Pulsar，只能做轻量结构校验），失败时给出清晰报错（指出应为 camelCase 数组 + 参照 `Fixtures/PluginUsageStats.json`），避免下次再静默空态。

### P3 — E2E 低干扰模式（用户问了，可选）
- 用户想一边打游戏一边跑 E2E。当前 `UiaDriver.ClickElement` 用真实 SendInput 鼠标点击 + `open-settings` 命令 `Show()+Activate()` 抢焦点 → 会打断游戏。
- 方案：窗口 `ShowWithoutActivation`（或 Show 后不 Activate）+ 点击改用 UIA `InvokePattern`/`SelectionPattern`（FlaUI 支持，不需要前台窗口、不移动鼠标）。这样窗口出现但不抢焦点、鼠标不动，可边玩边测。注意：InvokePattern 需要目标控件支持（WPF Button/NavigationViewItem 一般支持）。

### P4 — 分析页 P2 项（用户未批准，仅记录）
空态 CTA、排序状态可视化、插件下钻、自动洞察。

## 3. 验证命令速查

```bash
# 构建/测试（在 worktree 里跑，bin/obj 各自独立，NuGet 缓存读安全）
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev.ps1 build
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev.ps1 test        # 全量 ~1060
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev.ps1 test --filter "FullyQualifiedName~SettingsAnalytics"

# E2E（从 Pulsar\Pulsar.E2E 目录跑；exe 在 bin\x64\Debug\net8.0-windows\Pulsar.E2E.exe）
.\bin\x64\Debug\net8.0-windows\Pulsar.E2E.exe run --workflow Workflows\settings-analytics-empty-dark.json --app '<worktree>\Pulsar\Pulsar\bin\Debug\net8.0-windows\Pulsar.exe' --artifacts artifacts --run-id <id>
```

## 4. 会话仪式提醒

- 开始：读 `Docs/journal/NEXT.md` + 今日日文件尾部（本会话最新 Session 块 ~17:5x）
- 结束：`Docs/journal/2026-09-05.md` 追加 `## Session (HH:MM)` 块（≤~25 行，append-only）+ 更新 NEXT.md；journal 只在 main 上提交（feature worktree 不提交 journal）
