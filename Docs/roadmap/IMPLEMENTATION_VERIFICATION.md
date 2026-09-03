# Roadmap 实现状况对比验证报告

> **验证日期**: 2026-09-03 | **验证对象**: [README.md](./README.md)（2026-08-31 提案）+ [RIGHT_DRAG_GESTURE_ANALYSIS.md](./RIGHT_DRAG_GESTURE_ANALYSIS.md)
> **验证方式**: roadmap 条目 → openspec 变更归档 → `Pulsar/` 源码实证 → 编译 + 全量测试

---

## 0. 结论摘要

| 指标 | 结果 |
|---|---|
| 编译 | ✅ 通过（0 警告 0 错误，`dotnet build Pulsar.sln`） |
| 测试 | ✅ **880 通过 / 0 失败 / 0 跳过** |
| 落地的 openspec 变更 | **10 个**（2026-09-01 ~ 09-02 归档） |
| 方向一（手势） | 🟢 **核心 6 项全部落地**，3 项衍生能力未做 |
| 方向二（渲染器） | 🟢 **骨架 + 首发形态落地**，4 个打磨项未做 |
| 方向三（级联子菜单） | 🟢 **全部落地**，仅剩 1 项人工 QA |
| 短期路线「多语言扩展」 | 🟢 已闭环（2026-09-03 按决策收敛为中英双语：zh-CN 经 XML 级校验 1037/1037 全覆盖；zh-TW / ja 明确不做） |
| 长期「方向四 AI」 | ⚪ 未启动（符合规划，属长期） |

**一句话**：roadmap 提出后 2 天内，方向一/二/三的主干已由 10 个 openspec 变更全部落地并通过 880 项测试；剩余缺口集中在「打磨型能力」（多触发键、扇区命中、配色微调、多语言），而非主干。

---

## 1. 方向一：手势盲操细节深化（最高优先）

### 1.1 StarPie 借鉴要点逐项验证

| Roadmap 要点 | openspec 变更 | 代码实证 | 状态 |
|---|---|---|---|
| **外甩取消**（> 外径×1.5 虚化取消） | `2026-09-02-flick-out-cancel` (12/12) | `MenuSession.UpdateFlickOutEscapeState()` / `IsFlickOutEscaped`；`ProfileSettings.GestureFlickOutCancelEnabled`(默认 true) + `GestureFlickOutRadiusMultiplier`(默认 **1.5**，对齐 StarPie)；仅 `InvocationSource == RightDragGesture` 参与 | ✅ 完成 |
| **进程隔离双模态**（白/黑名单） | `2026-09-02-gesture-isolation-filter` (16/16) | `GestureIsolationService` / `GestureIsolationNative` / `GestureIsolationMode{Allowlist,Blocklist}`；`ProfileSettings.GestureIsolationProcesses` | ✅ 完成 |
| **全屏防误触**（`Progman`/`WorkerW`/`Shell_TrayWnd` 旁路） | 同上 | `GestureIsolationBlockFullscreen`（默认 true）+ `IsFullscreenShellClass()` + `IsFullscreen()`（`FullscreenTolerance = 2`） | ✅ 完成 |
| **位移阈值 + 未达阈值重放**（专项文档 §5.1，根治主诉） | `2026-09-01-fix-right-drag-release-passthrough` (28/28)<br>`specs/right-drag-threshold-replay` | `GestureSummonMode{Immediate,OnThreshold}`；`GestureDragThreshold`（默认 **25**，对齐 StarPie）；`RightDragGestureDetector.FeedDisplacement()` → `SubThresholdRelease`；`GlobalMouseHook.ReplayRightClick()` + `_ignoreNextButtonDown/Up` 防递归 | ✅ 完成 |
| **Reset() 释放竞态修复**（专项文档 §5.2 / 根因 B） | `specs/right-drag-release-race` | `Configure()` 明确「Never touches an in-flight press state」；`RadialMenuViewModel.ApplyPendingGestureConfig()` 延后应用 | ✅ 完成 |
| **派发优先级提升**（专项文档 §5.3 / 根因 C） | `specs/right-drag-dispatch-timing` | `RadialMenuViewModel` 手势路径统一 `DispatcherPriority.Input` | ✅ 完成 |
| **修饰键穿透**（`DisableOnCtrl/Shift/Alt`） | — | 无相关实现。与 Pulsar「修饰键唤出」方向相反，roadmap 自评「需做选项」 | ❌ 未做 |
| **多触发键**（右键/中键/侧键/键盘长按） | — | 全仓无 `WM_MBUTTON` / `WM_XBUTTON` / `TriggerButton` 处理；仍仅右键 + 修饰键 | ❌ 未做 |
| **4/8/12 扇区 + `Atan2` 角度命中** | — | 仍为分页模型 `SlotsPerPage`（4–12），无 `SectorCount` | ❌ 未做 |
| **hook 健康检查自愈**（专项文档机制 #3） | — | 无 `CheckHookHealth` / 重装定时器 | ❌ 未做 |

### 1.2 Roadmap §2.3 落地路径对照

| 步骤 | 计划做法 | 实际做法 | 偏差 |
|---|---|---|---|
| 1 | `MenuSession` 增加位移阈值 + 方向判定 | 阈值判定下沉到 `RightDragGestureDetector`（纯状态机、可单测），`MenuSession` 只消费 `IsFlickOutEscaped` | ✅ 更优（可测性更强） |
| 2 | `FullScreenHelper` 移植为 `IWindowEligibilityEvaluator` 装饰器 | 实现为独立 `IGestureIsolationService`（含 `IGestureIsolationNative` 接缝），未做装饰器 | ⚠️ 路径不同，但保留了同等的 P/Invoke 可测接缝，目的达成 |
| 3 | 手势触发提升为可配置策略，接入 `ProfileSettings` | 已落地 `GestureSummonMode` / `GestureDragThreshold` / 隔离系列 / 外甩系列，全部持久化并在 `SettingsGeneralPage.xaml` 暴露 | ✅ 完成 |
| 4 | 补 `RightDragGestureDetectorTests` / `MenuSessionGestureTests` | 新增 `RightDragGestureLeakTests` + `RightDragGestureIsolationTests`，扩展 `RightDragGestureDetectorTests` | ✅ 完成 |

---

## 2. 方向二：视觉形态与渲染器体系（高）

| Roadmap 要点 | openspec 变更 | 代码实证 | 状态 |
|---|---|---|---|
| **`IRadialStyleRenderer` 契约**（§3.4.1） | `2026-09-01-radial-renderer-contract` (19/19)<br>`specs/radial-renderer-contract` | `Core/Rendering/IRadialRenderer.cs`、`IRadialThemeTokens.cs`、`IRadialSlotHighlight.cs`、`RadialThemeTokenSet.cs`、`RendererResourcePack.cs`、`ModeToneTokenDecorator.cs` | ✅ 完成 |
| **`StyleRendererFactory` 注册 + 形态渲染器**（§3.4.2） | `2026-09-02-radial-style-renderers` (19/19) | `StyleRendererFactory`（case-insensitive 注册表 + 未知 id 安全回落 Default）；已实现 **Default / ClassicRing / Glassmorphism** 三套 | 🟡 部分（roadmap 目标 4 形态，StarPie 的 CleanSectors / CatPaw 未做；与 §3.4.5「先双形态验证」一致） |
| **主题预设**（§3.2 抹茶/冰川/莫兰迪/液态毛玻璃） | `specs/radial-theme-presets` + 随 renderers 变更落地 | `RadialThemePresetCatalog` + `RadialThemePresetResolver`：`MatchaForest` / `GlacialIce` / `MorandiMuted`（「液态毛玻璃」由 `GlassmorphismRadialRenderer` 承担）；`SettingsViewModel.General.ThemePreset` = Dark/Light + 预设，未知值安全回落 | ✅ 完成 |
| **SVG 图标解析**（§3.4.4） | `2026-09-02-custom-icon-library` (11/11) | `IconHelper.LoadSvgFromPath()` → 提取 `d` 属性 → `Geometry.Parse` → 冻结 `DrawingImage`；测试 `IconHelperSvgTests` | 🟡 部分（单 path 子集，非完整 SVG 渲染） |
| **自定义图标持久化目录 `%AppData%\Pulsar\CustomIcons\`**（§3.4.4） | 同上 | `Services/CustomIconStore.cs` + `ICustomIconStore`，DI 注册于 `App.xaml.cs`；`IconPickerViewModel` / `EditProfileViewModel` / `InputProfileViewModel` 已消费；测试 `CustomIconStoreTests`、`IconPickerImportTests` | ✅ 完成 |
| **光晕（Glow）token** | 随契约变更 | `IRadialThemeTokens.ActiveGlow` 已纳入 token 模型 | 🟡 仅 token（无 `GetEffectiveGlowColor/Radius/Opacity` 三项可调 UI） |
| **8 项配色微调 + 吸色器** | — | 无对应实现；仍走 Dark/Light + 预设二选一 | ❌ 未做 |
| **渲染器走插件机制**（§3.4.3 差异化） | — | `StyleRendererFactory` 仍由 DI `IEnumerable<IRadialRenderer>` 注入，未接 `IPluginRegistry`，第三方无法发布主题插件 | ❌ 未做（Pulsar 原定的超越 StarPie 的差异化点尚未兑现） |
| **`SlotStyles.xaml` 演进为渲染器资源包**（§3.4.2） | — | 已有 `RendererResourcePack.cs`，但样式仍集中在 `SlotStyles.xaml`，未拆分为每渲染器一字典 | ❌ 未做 |

---

## 3. 方向三：级联子菜单泛化（高）

| Roadmap 要点 | openspec 变更 | 代码实证 | 状态 |
|---|---|---|---|
| **`SubSlots` 集合进入 slot 模型**（§4.4.1） | `2026-09-02-cascade-submenu-foundation` (25/25)<br>`specs/cascade-submenu-model` | `Models/SubSlotDescriptor.cs`；`PluginSlot` 新增子动作集合（测试 `PluginSlotSubActionsTests`）；`CascadeSubMenuDescriptor{SubSlots, Label, LayoutStyle, StrategyId="cascade"}` | ✅ 完成 |
| **协调器泛化为策略**（§4.4.2） | `specs/submenu-coordinator-strategy` | `ViewModels/Strategies/CascadeSubMenuStrategy.cs` 与 `WindowSwitchSubMenuStrategy` 并列；测试 `SubMenuCoordinatorStrategyTests`、`CascadeSubMenuStrategyTests` | ✅ 完成 |
| **Ring / Fan 二级布局 + 命中算法**（§4.4.3） | `2026-09-02-cascade-submenu-layout` (14/15)<br>`specs/cascade-submenu-layout` | `Services/SubMenuLayoutEngine.cs`：`FanMaxSlots=3`、翼角 ±30°、`ComputeChildPositions` / `HitTestChild` → `HitTestFan`（最小角差判定，对齐 StarPie `HitTestFanSubs`）/ `HitTestRing`（内外环带判定）；>3 子项自动回落 Ring | 🟡 **仅剩任务 5.2 人工 QA**（配置 2–3 个子动作验证 Fan 渲染/选中、4+ 回落 Ring、分页、双主题） |
| **子菜单进入/退出** | `specs/cascade-submenu-entry` | `CascadeSubMenuEntryTests`；`MenuSession` 已接入 | ✅ 完成 |
| **二级动作编辑器**（§4.4.4） | `2026-09-02-cascade-submenu-editor` (16/16) | `ViewModels/Dialogs/SubSlotEditorRow.cs` + `SlotEditorViewModel` + `Views/Dialogs/Contents/SlotConfigurationDialogContent`；`FanSub`/`Ring` 布局选择已入 UI | ✅ 完成 |
| **智能默认注入**（§4.4.5） | `specs/cascade-smart-defaults` | `Services/SmartSubActionDefaults.cs` + `ISmartSubActionDefaults`，`App.xaml.cs` 注册；`SlotEditorWorkspace` / `SettingsViewModel` 在新建 slot 时注入（复制方位→剪切/粘贴/全选；系统工具→记事本/计算器/任务管理器） | ✅ 完成 |
| **编辑器基座重构**（附随） | `2026-09-02-refactor-slot-editor-visualization` (25/25) | `specs/slot-wheel-editor-architecture` / `slot-wheel-editor-visualization` | ✅ 完成 |

### ⚠️ Roadmap §4.5 风险项追踪

| 风险 | 处置 |
|---|---|
| Paging 与子环叠加时的分页语义 | `SubMenuLayoutEngine` 已覆盖（>3 回落 Ring + 分页），**但语义未在文档中固化**，建议补 ADR |
| Fan 高 DPI 像素命中 | 命中为纯几何计算（相对 `SubRingRadius`，与像素无关），**已规避**；待人工 QA 复核 |

---

## 4. Roadmap §5 建议路线对照

| 阶段 | roadmap 计划 | 实际（截至 2026-09-03） | 判定 |
|---|---|---|---|
| **短期（1–2 迭代）** | 方向一：手势细节 + **多语言扩展** | 手势细节 ✅ 全落地；多语言 ✅ 已按决策收敛为中英双语（zh-CN 覆盖校验完整，zh-TW / ja 不做） | 🟢 **完成** |
| **中期（3–5 迭代）** | 方向二：渲染器体系 + 方向三：级联子菜单 | 两者主干均落地（仅剩形态扩充与打磨），**2 天内完成，远快于 3–5 迭代** | 🟢 超额 |
| **长期** | 方向四：AI 差异化（动作推荐 / 自然语言命令 / 配置生成） | 未启动；依赖无头模拟器 + 插件系统 + Usage Analytics（后两者已在位） | ⚪ 符合预期 |

> **能力对比表（roadmap §1）已过时**：表内「视觉定制：仅 Dark/Light 两套主题，无图标导入」「手势细节：仅 Action/Switcher 两模式」「级联子菜单：无」三行已被本次实现推翻，建议同步更新。

---

## 5. 未落地项清单（按建议优先级）

| # | 缺口 | 归属 | 价值 | 建议 |
|---|---|---|---|---|
| 1 | **级联子菜单 Fan 人工 QA**（`cascade-submenu-layout` 任务 5.2） | 方向三 | 高 —— 唯一阻塞项 | 需人工验证：2–3 子项 Fan 渲染/选中、4+ 回落 Ring、分页、Dark/Light 双主题 |
| 2 | ~~多语言扩展（zh-TW / ja）~~ **已闭环（2026-09-03）** | 短期路线 | —— | 按用户决策收敛为中英双语；zh-CN 覆盖经 XML 级校验已完整（「仅 101/1037 键」系 grep 单行计数假象），清理 1 条孤儿键 `Plugin.Bookmarklet.MissingScriptPath` |
| 3 | **渲染器插件化**（接 `IPluginRegistry`） | 方向二 §3.4.3 | 高 —— Pulsar 相对 StarPie 的**唯一差异化点** | 让第三方可发布主题插件，否则与 StarPie 静态渲染器无差别 |
| 4 | **补齐 CleanSectors / CatPaw 形态** | 方向二 | 中 | roadmap 已规划「先双形态验证再扩 4 形态」，可排入下迭代 |
| 5 | **8 项配色微调 + 吸色器 + 光晕三参数** | 方向二 | 中 | 依赖渲染器体系稳定后再上 UI |
| 6 | **多触发键**（中键/侧键/键盘长按） | 方向一 | 中 | `GlobalMouseHook` 需扩 `TriggerButton` 抽象 |
| 7 | **修饰键穿透选项** | 方向一 | 低中 | roadmap 自评「与 Pulsar 方向相反，需做选项」 |
| 8 | **hook 健康检查自愈** | 方向一 | 中（稳定性） | StarPie 机制 #3，钩子卡死自动重装 |
| 9 | **`SlotStyles.xaml` 拆分为每渲染器资源包** | 方向二 §3.4.2 | 低中 | 当前 `RendererResourcePack` 仅为中间层 |
| 10 | **4/8/12 扇区 + 角度命中** | 方向一 | 低 | 与 Pulsar 分页模型冲突，建议**显式决定不采纳**而非悬置 |

---

## 6. 文档与工程卫生问题

> **2026-09-03 后续处理**：前四项已在本报告产出后同轮修复完毕；末项「分页语义未固化」亦已由 **[ADR-011](../decisions/011-cascade-submenu-layout-and-paging.md)** 固化 —— 本节五项全部闭环。

| 问题 | 说明 | 处置 |
|---|---|---|
| **roadmap 状态未回写** | `Docs/roadmap/README.md` 头部原标「**状态**: 提案中 (Proposed)」，但三个方向主干已落地 | ✅ **已修复** — 状态改为「大部分已落地」，§2/§3/§4 标题加状态标记并附落地摘要，§5 路线表增加「状态」列与 §5.1 下一迭代建议，§6 链接本报告 |
| **能力对比表过期** | §1 对比表中「无级联子菜单 / 无图标导入 / 仅两主题」等行已被实现推翻 | ✅ **已刷新** — 视觉定制、手势细节、差异化能力三行按实况重写 |
| **CHANGELOG 空缺** | `CHANGELOG.md` 的 `[Unreleased]` 与 `[1.8.0]` 原为「暂无 / TODO」，而 10 个 openspec 变更已归档 | ✅ **已补写** — 依据 git 提交记录补出 `[Unreleased]` / `[1.9.1]` / `[1.9.0]` / `[1.8.1]` / `[1.8.0]` 五段 |
| **遗留调试日志** | 生产代码原含 **31 处** `[DEBUG-RDX]` 标记日志，其中 **28 处为 `LogInformation`**：<br>`RadialMenuViewModel.cs`(19)、`GlobalMouseHook.cs`(8)、`GlobalMouseService.cs`(1)；另 3 处本已是 `LogDebug` | ✅ **已降级** — 28 处 `LogInformation` → `LogDebug`，消息文本与 `[DEBUG-RDX]` 标记保留以便继续排查；`GlobalMouseHook` 每条鼠标事件写 Information 级日志的膨胀风险已消除。降级后重新编译 0 警告 0 错误、测试 880 全通过 |
| **`Fan` 分页语义未固化** | 子环 + 分页叠加的语义只在代码里 | ✅ **已固化** — [ADR-011](../decisions/011-cascade-submenu-layout-and-paging.md) 记录 8 项语义裁决：独立页状态、共享 `SlotsPerPage`、滚轮按菜单状态路由、**Fan 为偏好 / Ring 为下限（按页回落）**、命中按页作用域、子环半径由根环推导并钳制画布安全区、**外甩取消始终以根环为基准**、中心槽页码指示 |

---

## 7. 验证方法说明

1. **roadmap 解析**：`Docs/roadmap/README.md`（3 方向 + 路线表）、`Docs/roadmap/RIGHT_DRAG_GESTURE_ANALYSIS.md`（6 项移植机制 + 4 节修复方案）。
2. **openspec 交叉**：`openspec/changes/archive/2026-09-*`（10 个变更，`tasks.md` 勾选率统计）+ `openspec/specs/`（新增 12 个 spec 目录）。
3. **代码实证**：对每一项在 `Pulsar/Pulsar/` 下做符号级检索（类名、方法名、配置项、DI 注册、XAML 暴露），而非仅凭文档断言。
4. **可执行验证**：
   - `dotnet build Pulsar.sln -c Debug` → **0 警告 0 错误**
   - `dotnet test Pulsar.sln -c Debug` → **通过 880 / 失败 0 / 跳过 0**
