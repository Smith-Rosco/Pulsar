## Context

Pulsar 是一个 Windows 桌面生产力启动器，采用 WPF (.NET 8) + WPF-UI 框架构建。当前 UI 在快速迭代中形成了以下技术现状：

- 设置窗口导航由硬编码的 `NavigationViewItem` 列表构成，无分组概念
- Radial Menu 使用固定 500×500 Canvas 布局，Slot 位置由 ViewModel 计算绝对坐标
- JellyOrb HitTest 使用默认矩形区域（ContentPresenter 边界），非圆形
- SlotConfigurationDialogContent 是单页线性表单，所有字段同时可见
- Theme 资源中无统一的间距/动效 Token，各控件使用硬编码数值
- "Save Changes" 作为 NavigationViewItem 存在，语义错误

**约束**：
- 必须遵守 Pulsar 的 Multi-Headed UI 架构（不依赖 App.xaml 全局样式）
- 所有 Window/Page 必须通过 `IThemeService.ApplyTheme()` 注入主题
- Page 必须在 `InitializeComponent()` 之后调用 `ApplyTheme()`
- 按钮必须使用 `PulsarPrimaryButtonStyle` / `PulsarSecondaryButtonStyle` / `PulsarDangerButtonStyle`
- 不能使用 `Appearance="Primary"`

## Goals / Non-Goals

**Goals:**
- 修复设置导航的信息架构，消除语义错误的导航项
- 将 Slot Context 切换器提升为页面主体的一等公民
- 实现圆形精确 HitTest，消除 Radial Menu 误触
- 为 Radial Menu 引入 DPI-aware 布局，适配高分屏
- 将 SlotConfiguration 重构为 3 步向导，降低认知负荷
- 建立 Design Token 资源体系（间距 + 动效时长）
- 优化 IconPicker 的内置图标与自定义上传的视觉层级

**Non-Goals:**
- 不修改插件系统核心逻辑
- 不引入新的第三方 UI 库
- 不重写 Theme 颜色系统（仅新增 Token）
- 不实现响应式/自适应窗口尺寸（SettingsWindow）

## Decisions

### D1: 设置导航重组策略

**决策**：使用 WPF-UI `NavigationView` 的 FooterMenuItems 区域承载非导航性动作（如版本信息），将 "Save" 重构为页面内 `PulsarPrimaryButtonStyle` 按钮，放置在每个有编辑状态的页面的底部固定栏。

**Plugins 合并**：在导航中保留单一 "Plugins" 入口，在对应 Page 内使用 TabControl 分隔 "Built-in" 和 "External" 两个子视图。

**备选方案**：在 NavigationView 中增加嵌套导航子项 → 拒绝，WPF-UI NavigationView 的嵌套层级在当前版本中样式支持不稳定。

### D2: Radial Menu DPI 适配

**决策**：在 `RadialMenuWindow` 的 code-behind 中，于 `Loaded` 事件读取 `PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice` 获取 DPI 缩放系数，将半径计算传入 ViewModel 的 `LayoutRadius` 属性，ViewModel 据此重新计算各 Slot 的 X/Y 坐标。

**窗口尺寸**：窗口尺寸随 DPI 同步缩放，公式：`WindowSize = BaseSize × DpiScale`，BaseSize 保持 500。

**备选方案**：使用 `ViewBox` 自动缩放整个 Canvas → 拒绝，ViewBox 会导致文字和图标模糊。

### D3: 圆形 HitTest 实现

**决策**：重写 JellyOrb 的 `HitTestCore`，继承自 `UIElement` 的 `HitTestCore(PointHitTestParameters)` 方法，判断点击点到中心距离是否 ≤ 半径，实现圆形命中区域。

**备选方案**：在外层放透明 Ellipse 覆盖 → 拒绝，会遮挡内部控件的交互事件。

### D4: SlotConfiguration 向导模式

**决策**：在现有 `SlotConfigurationDialogContent.xaml` 内引入步骤状态机，通过 `CurrentStep`（int 0/1/2）属性控制三个子 Panel 的 `Visibility`，配合 `BooleanToVisibilityConverter`。不创建新对话框，保持 `DialogService` 调用不变。

ViewModel 新增：`CurrentStep`、`CanGoNext`、`CanGoBack`、`NextStepCommand`、`PreviousStepCommand`。

**步骤定义**：
- Step 0：插件类型选择（大卡片 ItemsControl）
- Step 1：参数配置（现有参数字段模板）
- Step 2：个性化（图标、颜色、标签）

**备选方案**：使用独立的三个 Dialog → 拒绝，破坏单一对话框的用户感知连贯性。

### D5: Design Token 体系

**决策**：在 `Theme.Dark.xaml` 和 `Theme.Light.xaml` 中新增以下资源键，各控件 XAML 通过 `StaticResource` 引用：

```
<!-- 间距 Token -->
Spacing.XS = 4
Spacing.SM = 8
Spacing.MD = 16
Spacing.LG = 24
Spacing.XL = 32

<!-- 动效 Token -->
Animation.Duration.Fast = 0:0:0.15
Animation.Duration.Normal = 0:0:0.25
Animation.Duration.Slow = 0:0:0.35
Animation.Easing.Standard = CubicEase (EaseInOut)
```

**实施范围**：本次仅对本 change 涉及的文件替换硬编码值，不做全量替换（避免大范围回归风险）。

## Risks / Trade-offs

- **[Risk] SlotConfiguration 向导破坏现有参数编辑流程** → Mitigation: Step 1 直接复用现有 `DialogSlotParameterFieldTemplate`，不重写数据模板，保持字段逻辑不变
- **[Risk] DPI 适配在多显示器切换时未及时更新** → Mitigation: 监听 `Window.DpiChanged` 事件，触发重新布局
- **[Risk] 圆形 HitTest 重写影响 Accessibility 工具（如 Narrator）的命中检测** → Mitigation: 保留 `AutomationPeer` 默认矩形区域，仅修改视觉命中
- **[Risk] Theme Token 新增 key 与 WPF-UI 内部 key 命名冲突** → Mitigation: 所有自定义 Token 使用 `Pulsar.` 前缀命名空间

## Migration Plan

1. 先实施 Design Token（影响面最小，为后续步骤提供基础）
2. 实施 HitTest 修复（独立，零破坏性）
3. 实施 DPI 适配（依赖 ViewModel 布局逻辑）
4. 实施设置导航重组（影响导航结构）
5. 实施 Slot Context 切换器重定位
6. 实施 SlotConfiguration 向导（最复杂，最后）
7. 实施 IconPicker 优化

每步完成后运行 `dotnet build` 验证无编译错误。

**回滚**：所有修改均在现有文件上进行，通过 Git 可逐步回滚。

## Open Questions

- Plugins 页面合并后，Built-in/External TabControl 的默认选中 tab 是否需要记忆用户上次的选择？（建议：不需要，默认 Built-in）
- SlotConfiguration 向导的步骤指示器（Step dots）是否需要支持点击跳转？（建议：否，强制线性流程）
