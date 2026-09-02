## Context

契约层已在 `Core/Rendering/` 落地(见 proposal.md Why):`IRadialRenderer`(Id / Initialize / ResolveHighlight / RenderDecorations)、`IRadialThemeTokens` + `RadialThemeTokenSet`、`RadialThemePresetResolver`(System/Dark/Light/3 预设)、`ModeToneTokenDecorator`、`DefaultRadialRenderer`。`RadialMenuViewModel.ApplyRadialRendering(mode)` 已在菜单打开与 ConfigUpdated 时解析预设→token→mode-tone→`_renderer.Initialize`。`ProfileSettings.RadialRenderer`/`RadialThemePreset` 字段已存在(默认 `"Default"`/`"System"`),但渲染器目前是 DI 固定单例、设置页无选择器。需求契约见 specs;此处只讲怎么落地。

## Goals / Non-Goals

**Goals:**
- 让渲染器选择变为配置驱动:新增 `StyleRendererFactory`,按 `ProfileSettings.RadialRenderer` 解析实例,未知值回退 Default。
- 首期落地 ClassicRing(经典环)与 Glassmorphism(液态毛玻璃)两个新形态,各自实现高亮 + 装饰层(`RenderDecorations`),视觉差异化肉眼可辨。
- 设置页新增「外观」区:渲染器形态选择器 + 主题预设选择器,`ConfigEditSession`/`RebuildCache` 模式持久化。

**Non-Goals:**
- 不做渲染器插件化(`IPluginRegistry` 发布第三方渲染器)——留后续 change。
- 不做 4 形态全量;只做 Default/ClassicRing/Glassmorphism 三形态。
- 不做 SVG/自定义图标库(独立 change `custom-icon-library`)。
- 不改 `Profiles.json` 数据模型(渲染器/预设字段已存在)。

## Decisions

### D1: `StyleRendererFactory` 采用「注册表 + DI 单例」而非静态字典

`Core/Rendering/StyleRendererFactory.cs`,构造注入 `IEnumerable<IRadialRenderer>`,按 `Id` 建大小写不敏感字典。`Create(string id)` 未知 id 返回 Default 实例(而非抛错)。DI 中每个渲染器注册为单例、`DefaultRadialRenderer` 显式标记为回退。`RadialMenuViewModel` 注入工厂替换(或并列)现有 `IRadialRenderer`,`ApplyRadialRendering` 改为 `_renderer = factory.Create(settings.RadialRenderer)`。

- **为什么**:渲染器除 `ResolveHighlight` 纯函数外只有一个可覆写的 `Initialize(tokens)`,共享单例无并发风险;DI 注册让测试可注入任意渲染器集合,工厂保持纯逻辑可单测。
- **替代方案**:静态字典(难测、绕 DI)、每次新建(浪费)、策略定位器(过度设计)。

### D2: 渲染器形态视觉以「代码画笔 + token」为主,SlotStyles 演进为辅

ClassicRing: 高亮=环状描边(`Accent` 加粗 stroke + 轻微外发光,blur radius 降为 12);装饰=外圈细环 + 四象限刻度线,`RenderDecorations` 用 `StreamGeometry`/`EllipseGeometry` 画。Glassmorphism: 高亮=半透明填充层(orb fill alpha≈0.35)+ 1px `AccentHover` 描边,blur 半径 8 的柔和边缘;装饰=中心 orb 背后毛玻璃圆盘(多层 alpha 圆 + 顶部高光弧)。所有画笔从 `IRadialThemeTokens` 取,复用 `ModeToneTokenDecorator` 已注入的 accent。新增一个轻量 `RendererResourcePack`(per-renderer 字典:描边粗细/alpha/半径常量),避免在渲染器代码里散落魔法数字,但**不在 XAML 里裸奔 DynamicResource**(遵循 WPF_THEME_INJECTION_PITFALLS 教训)。

- **为什么**:两形态差异主要靠「高亮表现层」而非整套资源字典,代码画笔 + token 接缝最小、最符合既有 `IRadialRenderer` 纯函数纪律;XAML 资源包留作后续插件化时的外部契约。
- **风险**:装饰层若用 `DropShadowEffect` 会违反性能纪律 → 只允许 blur/渐变/透明度,与 `radial-renderer-contract` 性能需求一致。

### D3: 设置 UI 复用 `SettingsGeneralPage` + `SettingsViewModel` 现有编辑会话

在 `SettingsGeneralPage.xaml` 新增「外观」分组:渲染器形态 `ComboBox`(Default/ClassicRing/Glassmorphism)+ 主题预设 `ComboBox`(System/Dark/Light/MatchaForest/GlacialIce/MorandiMuted)。`SettingsViewModel` 增加只读选项源与可写属性,走 `ConfigEditSession`/`RebuildCache` 模式(参照 HOTKEY_SERVICE_STALE_CONFIG_OVERWRITE / CONFIG_EDIT_SESSION_STALE_REVISION 教训,绝不 `UpdateHotkey()` 回写)。保存后 `ConfigUpdated` → `ApplyRadialRendering` 立即重渲染菜单。

- **为什么**:General 页已承载手势设置,视觉类设置语义相近;复用现有编辑会话避免第二个配置文件写入者。
- **替代方案**:新建 SettingsAppearancePage(导航/DI/测试成本更高,本 change 体量内不划算)。

## Risks / Trade-offs

- **Decorations 抢焦点/拦截输入** → `RenderDecorations` 画的形状全部 `IsHitTestVisible=false`(与 `radial-renderer-contract`「不拦截指针」需求对齐);测试断言装饰层元素无 hit-test。
- **渲染器单例共享 token** → `Initialize` 每次打开重设 tokens,无跨模式残留;ModeTone 装饰器在调用点新建。
- **设置回写竞态** → 复用 `ConfigEditSession`/`RebuildCache`,测试覆盖二次保存(参照 CONFIG_EDIT_SESSION_STALE_REVISION 教训)。

## Migration Plan

1. 纯新增,无破坏性迁移:`StyleRendererFactory`/新渲染器均新增注册,默认 `RadialRenderer="Default"` 路径视觉不变。
2. 部署顺序:Core/Rendering 工厂 + 新渲染器 → App.xaml.cs 注册 → 设置 UI。
3. 回滚:改回 `RadialRenderer="Default"` 即回到原视觉。

## Open Questions

- 预设选择器是否需要「预览缩略图」而非纯文字 ComboBox?——不影响本 change 契约,可后置到视觉打磨迭代,故列为 deferrable。
- Glassmorphism 是否要在主菜单窗口加全局模糊(Window blur)还是仅装饰层模糊?——WPF 无原生 acrylic,首期只做装饰层局部模糊(成本可控),全局 acrylic 留长期。
