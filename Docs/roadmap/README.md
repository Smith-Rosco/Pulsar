# Pulsar 长远开发方向规划

> **状态**: 提案中 (Proposed) | **日期**: 2026-08-31 | **来源**: 深度对比 `StarPie` (Ref/StarPie-main)

本文档基于对参考项目 **StarPie** 的深度源码对比,为 Pulsar 提出后续长远开发方向。
前三个方向已完成深入探索,并有独立专项分析 [RIGHT_DRAG_GESTURE_ANALYSIS.md](./RIGHT_DRAG_GESTURE_ANALYSIS.md)。

---

## 1. 定位与能力对比

| 维度 | **Pulsar** | **StarPie** |
|---|---|---|
| 核心输入 | 热键唤起 (`Ctrl+Q`/`Ctrl+Shift+Q`) + 修饰键+右键拖拽 | 鼠标右键/中键/侧键/键盘长按 拖拽唤起 |
| 架构 | 插件系统(双层+断路器+权限门控)、DI、MVVM、330+ 测试 | 单体 53 文件,静态类+事件,无单元测试 |
| 视觉定制 | 仅 Dark/Light 两套主题,4 个样式文件 | **4 形态 × 4 渲染器 + 8 主题预设 + 8 项配色微调 + 光晕 + 图标导入 + 吸色** |
| 手势细节 | 仅 Action/Switcher 两模式,修饰键判定 | **级联子轮盘、外甩取消、全屏防误触、修饰键穿透、黑白名单双模态、多触发键** |
| 差异化能力 | PKI 秘密注入、窗口切换 MRU+预览、无头模拟器、AI 开发流程 | 配置导入导出、智能前台 Toggle、常驻窗口切换器、4/8/12 扇区 |
| 多语言 | EN + zh-CN | 简中/繁中/英/日 四语热切换 |
| 工程基建 | ADR + openspec + lessons 文档体系 | 21 项 GUI 自动化测试 |

**一句话**: Pulsar 是"插件化 + AI 原生"的工程强者;StarPie 是"手势盲操 + 视觉打磨"的产品强者。
StarPie 在手势交互细节与视觉定制的成熟度上明显领先,这正是 Pulsar 可借鉴的最大缺口。

---

## 2. 方向一: 手势盲操细节深化(最高优先)

> 对应 StarPie: `GestureController.cs`、`MouseHook.cs`、`FullScreenHelper.cs`

### 2.1 现状(Pulsar)
- `RightDragGestureDetector.cs` 是纯状态机:依赖修饰键(默认 Action=Shift / Switcher=Control)在右键按下瞬间判定。
- `RadialMenuViewModel.FeedRightDragGesture` 在 Down/Up 处吞掉事件(Handled)。
- **局限**: 无位移阈值判定、无外甩取消、无全屏/进程隔离、无多触发键。

### 2.2 借鉴要点(StarPie)
| 能力 | StarPie 实现 | 说明 |
|---|---|---|
| 外甩取消 | `ProcessMove` 中 `_lastEscapedState`,光标 > 外径×1.5 即虚化取消 | 比"拉回中心"自然得多,符合肌肉记忆 |
| 进程隔离双模态 | `CheckIsIsolated`: `IsolationMode` 白名单/黑名单 | Pulsar 只有 WinSwitcher 黑名单,不覆盖手势 |
| 全屏防误触 | `FullScreenHelper` 旁路 `Progman`/`Shell_TrayWnd` | 桌面/任务栏误判全屏的坑已踩过 |
| 修饰键穿透 | `DisableOnCtrl/Shift/Alt` 时放行 | 与 Pulsar"修饰键唤出"方向相反,需做选项 |
| 4/8/12 扇区 | `SectorCount` + `Math.Atan2` 角度命中 | Pulsar 是"分页 4-12 slot",非"扇区数+角度命中" |
| 多触发键 | 右键/中键/侧键/键盘单键/长按 | 详见专项文档 |

### 2.3 落地路径(建议)
1. 在 `MenuSession` 增加**位移阈值 + 方向判定**的状态(参照 StarPie `ProcessMove` 的 `Math.Atan2` 算法)。
2. 将 `FullScreenHelper` 的窗口类名旁路逻辑移植为 `IWindowEligibilityEvaluator` 的一个装饰器(已有 ADR-010 的评估器接缝)。
3. 把"手势触发"从 `RadialMenuViewModel` 提升为可配置策略,接入 `ProfileSettings`。
4. 逐项用 `RightDragGestureDetectorTests` / `MenuSessionGestureTests` 模式补测试。

### 2.4 风险
- 位移阈值判定会与现有"按下即唤出"语义冲突,需双模式(立即唤出 vs 位移唤出)可选。
- 全屏检测的窗口类名硬编码需版本回归,StarPie 已踩过 `Progman`/`WorkerW` 误判坑。

---

## 3. 方向二: 视觉形态与渲染器体系(高)

> 对应 StarPie: `IRadialStyleRenderer.cs`、`StyleRendererFactory.cs`、`BaseStyleRenderer.cs`、各渲染器

### 3.1 现状(Pulsar)
- 两套主题 `Themes/Theme.Dark.xaml` + `Theme.Light.xaml`;样式仅 `ButtonStyles`/`SlotStyles`/`ScrollViewerStyles`/`TooltipStyles`。
- 主题注入走 `IThemeService.ApplyTheme()`(ADR 已固化)。

### 3.2 借鉴要点(StarPie)
`IRadialStyleRenderer` 接口抽象出渲染器的可插拔契约:
```
DefaultSectorBrush / HighlightSectorBrush / SectorBorderBrush / TextColorBrush
CoreBgBrush / BorderThickness / HighlightBorderThickness
Initialize(theme, config)
RenderDecorations(canvas, coreGrid, cx, cy, wheelRadius, coreRadius)
ApplySectorHighlight(path, isHighlighted)
ApplyExitHighlight(exitIcon, isHighlighted)
```
- `StyleRendererFactory.CreateRenderer(style)` → ClassicRing / Glassmorphism / CleanSectors / CatPaw。
- `BaseStyleRenderer.Initialize` 展示了**配色解析层次**: System→跟随系统、Light→标准亮色回退、命名主题(MatchaForest/GlacialIce/MorandiMuted)、`CustomPreset_*`→用户预设、`Custom`→逐项微调。每层 hex 字符串 → `SolidColorBrush`,并有 fallback。
- `GetEffectiveGlowColor/Radius/Opacity` 统一了高亮光晕的三种可调参数。
- 主题预设(抹茶森林/冰川透蓝/莫兰迪柔灰/液态毛玻璃)+ 8 项配色微调 + 光晕 + 吸色 + 预设管理。
- 自定义 SVG/PNG 图标导入,持久化到用户目录。

### 3.3 Pulsar 侧现状衔接
- Pulsar 已有 `IconHelper`(`Helpers/IconHelper.cs`),已支持 PNG/ICO/JPG/BMP 直载 + EXE/LNK 图标提取(`GetIconFromPath`/`ExtractExeIcon`)+ `ConcurrentDictionary` 缓存。**缺口**: 不支持 SVG 矢量解析(`Geometry.Parse`),且无"用户自定义图标库"持久化目录。
- Pulsar 现有 `SlotStyles.xaml` 全部走 `DynamicResource` 主题画笔(`ControlFillColor*`/`TextFillColor*`),风格统一,可作为渲染器的颜色 token 层。
- `IconSelector.xaml`/`IconPickerContent.xaml` 是现成的图标选取 UI 入口,可扩展"导入自定义图标"。
- 主题注入已有 `IThemeService.ApplyTheme()`(InitializeComponent 之后)的纪律,渲染器画笔应复用同一接缝。

### 3.4 落地路径(建议)
1. 定义 `IRadialStyleRenderer`(放 `Core/Rendering/`),以 `StyleRendererFactory` 注册。
2. Pulsar 现有 `SlotStyles.xaml` 可演进为渲染器资源包(每个渲染器一个资源字典)。
3. **差异化**: 让渲染器走 Pulsar 插件机制(`IPluginRegistry`),第三方可发布主题插件 — 超越 StarPie 静态渲染器。
4. 自定义图标导入: 扩展现有 `IconHelper` 支持 `Geometry.Parse(SvgPathData)`,新增 `CustomIconStore` 持久化到 `%AppData%\Pulsar\CustomIcons\`。
5. 首个渲染器以"经典环 + 液态毛玻璃"双形态验证,再扩展 4 形态。

### 3.5 风险
- WPF 动态资源继承在多窗口下易碎(参见 lessons),渲染器需以代码画笔 + `IThemeService` 注入,避免 XAML DynamicResource 裸奔。
- 每个渲染器需配套 `ApplyTheme()` 顺序(InitializeComponent 之后),复用现有教训。

---

## 4. 方向三: 级联子菜单泛化(高)

> 对应 StarPie: `ActionItem.SubActions`、`SubActionEditorWindow`、`SlotViewModel`/`SubSlotViewModel`

### 4.1 现状(Pulsar)
- `RadialMenuSubMenuCoordinator` 只服务"窗口切换子菜单":把某进程窗口填入中心+槽位,策略为 `WindowSwitchStrategy`/`BackActionStrategy`。
- Slot 模型(`SlotViewModel`)无子动作树概念。

### 4.2 借鉴要点(StarPie)
- `ActionItem.SubActions: List<ActionItem>` — 每个主动作可挂 1~4 个二级子动作。
- 两种子菜单形态: **Wheel 外圈同心子环** / **Fan 蜂窝扇**(`SubmenuStyle`,Fan 以父扇区为圆心扇形展开)。
- 独立子主题/配色(`UseIndependentSubWheelTheme`)、`SubActionEditorWindow` 编辑器。
- `SubSlotViewModel` 复用同一 `ActionItem` 模型(`Type/Parameter/Arguments/IconKey/CustomIconSvg`),二三级共用一套数据结构与编辑器 — 切换形态无需重配。
- Fan 命中: `HitTestFanSubs` 把子扇区投影到父扇区极坐标,以"最近角差"判定;`GetFanSlotIndex`/`GetFanSubOffset` 负责 1~3 项的对称排布(1 居中 / 2 上下翼 / 3 三角)。
- 智能默认注入: 自动为"复制方位"级联剪切/粘贴/全选,"系统工具方位"级联记事本/计算器/任务管理器。

### 4.3 Pulsar 侧现状衔接
- Pulsar 的 slot 模型(`SlotViewModel`)是扁平 `ObservableCollection<SlotViewModel>`,`ActionStrategy` 模式已能承载任意行为;子菜单现在被 `RadialMenuSubMenuCoordinator` 独占为"窗口切换"。
- `ISlotLayoutEngine`(`SlotLayoutEngine.cs`)是现成的布局接缝,可在其上新增 `RingSubLayout` / `FanSubLayout` 两种二级布局。
- `ConfigureSubMenu` 现有流程(中心=Back、槽位=WindowSwitchStrategy、分页、缩略图、`SubMenuColorPalette`)可作为"子菜单配置模板",泛化为通用 `SubMenuStrategy`。
- `SlotConfigurationDialogContent` / `SlotWheelEditor` 是二级动作编辑器的可扩展基座。
- 已有分页(`IPagingController`)、子菜单过渡动画(`RadialMenuVisualStateCoordinator`)、拖拽换页(`drag-session-wheel-paging`),二级子环可复用同一动画控制器。

### 4.4 落地路径(建议)
1. 给 `PluginSlot`/`SlotViewModel` 增加 `SubSlots` 集合,复用 `ISlotLayoutEngine` 做子环布局。
2. `RadialMenuSubMenuCoordinator` 泛化为"子菜单协调器":窗口切换子菜单只是其中一种策略。
3. Fan 几何复用 `SlotLayoutEngine` 新增扇形布局,命中算法参考 StarPie `HitTestFanSubs`。
4. 编辑器复用 `SlotConfigurationDialogContent` 扩展二级编辑。
5. 参考 StarPie 的"智能默认注入",在首次引导/新建 profile 时自动注入常用二级动作。

### 4.5 风险
- Paging 与子环叠加时的分页语义需明确定义。
- Fan 在高 DPI 下的像素命中已有 StarPie 踩坑记录,需在 `MouseTrackingService` 层保证 DPI 换算。

---

## 5. 建议路线

| 阶段 | 内容 | 备注 |
|---|---|---|
| 短期(1-2 迭代) | 方向一:手势细节 + 多语言扩展 | 改动集中、测试可覆盖 |
| 中期(3-5 迭代) | 方向二:渲染器体系 + 方向三:级联子菜单 | 依赖 `IRadialStyleRenderer` 落地 |
| 长期 | 方向四+:AI 差异化(AI 动作推荐 / 自然语言命令 / 配置生成) | 依托无头模拟器 + 插件系统 + Usage Analytics |

---

## 6. 相关文档

- 专项分析: [RIGHT_DRAG_GESTURE_ANALYSIS.md](./RIGHT_DRAG_GESTURE_ANALYSIS.md)
- 参考项目: `E:\8_Project\10_C#\Ref\StarPie-main`
- 现有架构: [ARCHITECTURE.md](../../ARCHITECTURE.md)
- 决策记录: [Docs/decisions/](../decisions/)
