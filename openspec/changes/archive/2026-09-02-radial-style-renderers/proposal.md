## Why

方向二「视觉形态与渲染器体系」的契约层已完成(`radial-renderer-contract` 归档):`IRadialRenderer`、typed tokens、预设解析器、ModeTone 装饰器均已落地,但**只有 `DefaultRadialRenderer` 单一形态**,设置页也没有渲染器/预设选择器 —— 用户无法体验差异化视觉,已有字段 `RadialRenderer`/`RadialThemePreset` 形同虚设。

## What Changes

- **渲染器形态体系**(roadmap 3.4.2/3.4.5):
  - 新增 `StyleRendererFactory`,按 `ProfileSettings.RadialRenderer` 从注册表解析渲染器(Default / ClassicRing / Glassmorphism),未知值安全回退到 Default。
  - 新增 `ClassicRingRadialRenderer`(经典环)与 `GlassmorphismRadialRenderer`(液态毛玻璃)两个形态,各自实现 `ResolveHighlight` + `RenderDecorations`(装饰层绘制),装饰画笔从 token 解析,不硬编码进 slot 模板。
  - 新增 `RendererResourcePack` 收敛 per-renderer 数值常量(描边/透明度/半径)。
- **外观设置 UI**:
  - `SettingsGeneralPage` 新增「外观」区:渲染器形态选择器(Default/ClassicRing/Glassmorphism)+ 主题预设选择器(`RadialThemePreset` 现存字段首次接入 UI)。
  - 选择经 `ConfigEditSession`/`RebuildCache` 持久化,`ConfigUpdated` → `ApplyRadialRendering` 立即重渲染。
- **不包含**:渲染器插件化(`IPluginRegistry` 发布第三方渲染器)留后续 change;4 形态全量收敛为首期 Default/ClassicRing/Glassmorphism 三形态;SVG/自定义图标库为独立 change(`custom-icon-library`)。

## Capabilities

### New Capabilities

- `radial-style-renderers`: 多形态渲染器体系 —— `StyleRendererFactory` 按配置选择渲染器、ClassicRing/Glassmorphism 两形态(含装饰层)、`RendererResourcePack` 常量包、设置页渲染器+预设选择器。

### Modified Capabilities

- `radial-renderer-contract`: 新增需求 —— 渲染器选择从「DI 固定单例」变为「`StyleRendererFactory` 按 `ProfileSettings.RadialRenderer` 解析」,未知配置值安全回退;既有契约需求不变。
- `radial-theme-presets`: 新增需求 —— 预设值除代码可配置外,设置页提供 UI 选择器;预设解析行为不变。

## Impact

- **Affected code**:
  - `Core/Rendering/StyleRendererFactory.cs`(新)、`Core/Rendering/ClassicRingRadialRenderer.cs`(新)、`Core/Rendering/GlassmorphismRadialRenderer.cs`(新)、`Core/Rendering/RendererResourcePack.cs`(新)。
  - `ViewModels/RadialMenuViewModel.cs`(`ApplyRadialRendering` 改走工厂)。
  - `Views/Pages/SettingsGeneralPage.xaml` + `SettingsViewModel`(外观选择区)。
  - `App.xaml.cs` `ConfigureServices`(注册 factory + 新渲染器)。
  - `Resources/Strings.resx` + `Strings.zh-CN.resx`(外观设置键)。
- **Dependencies**: 复用 `IRadialRenderer`/`IRadialThemeTokens`/`RadialThemePresetResolver`/`ModeToneTokenDecorator` 现有接缝;不触碰 `Profiles.json` 数据模型(两字段已存在)。
- **No breaking changes**: `RadialRenderer`/`RadialThemePreset` 字段默认 `"Default"`/`"System"`,默认渲染路径视觉不变;新渲染器为可选项。
