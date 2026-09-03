# Renderer Plugin Registry

让第三方插件向环形菜单贡献自定义渲染器（`IRadialRenderer`），兑现 roadmap §3.4.3「渲染器插件化」——Pulsar 相对 StarPie 的唯一差异化点。

## Why

roadmap 方向二 §3.4.3 明确规划：渲染器应走 Pulsar 插件机制，第三方可发布主题插件，超越 StarPie 的静态渲染器。当前 3 个内置渲染器全部经 DI 硬编码注册（`App.xaml.cs`），宿主容器构建后不可变；设置页的渲染器选择器也只枚举硬编码的 3 项。插件系统（`IPulsarPlugin` + `PluginLoadContext` 隔离 + 权限门控）已就位，但没有任何 UI 能力贡献点。

## What Changes

- **可变渲染器注册表**：新增 `IRadialRendererRegistry`（单例、线程安全），插件渲染器按 `(rendererId, ownerId)` 注册/注销；内置渲染器 id 列入保留名单，插件不得占用/遮蔽。
- **工厂接入**：`StyleRendererFactory.Create(id)` 解析顺序 = 插件注册表 → 内置 DI 集 → Default 回落；新增 `GetAvailableRenderers()` 供设置页枚举全部可用项。
- **插件贡献入口**：插件经宿主 DI 解析 `IRadialRendererRegistry`，在 `OnEnableAsync` 注册、（可选）`OnDisableAsync` 注销；内核在插件禁用/卸载时**无条件**按 owner 清理，防悬挂引用。
- **权限门控**：新增权限令牌 `ui.render`；注册策略按 `PluginProfile.GrantedPermissions` 校验 ownerId，未授予/未知 owner 一律拒绝。内置渲染器不走注册表，不受影响。
- **缓存失效**：`SlotOrb` 静态渲染器缓存订阅注册表 `Changed` 事件，插件渲染器移除后立即回落，不留悬挂引用。
- **设置 UI**：渲染器选择器从硬编码 3 项改为动态枚举（内置项保留本地化名，插件项显示其 Id），注册表变化时刷新。

## Capabilities

### New Capabilities

- `plugin-renderer-registry`: 插件渲染器贡献点 —— 可变注册表、owner 归属与自动清理、`ui.render` 权限门控、工厂解析优先级、设置页动态枚举。

### Modified Capabilities

- `radial-renderer-contract`: 解析语义从「内置 DI 集」扩展为「插件注册表优先、内置次之、Default 兜底」；未知 id 回落 Default 的既有需求不变。

## Impact

- **Affected code**:
  - `Core/Rendering/IRadialRendererRegistry.cs`（新）+ `RadialRendererRegistry.cs`（新）。
  - `Core/Rendering/StyleRendererFactory.cs`（解析顺序 + 枚举）。
  - `Core/Plugin/PluginPermissions.cs`（`ui.render` 令牌）。
  - `Core/Plugin/Runtime/PluginRuntimeKernel.cs`（禁用/卸载时按 owner 清理）。
  - `App.xaml.cs`（DI：注册表 + 保留名单 + 注册策略）。
  - `Views/Controls/SlotOrb.xaml.cs`（缓存失效）。
  - `ViewModels/SettingsViewModel.General.cs` + `Views/Pages/SettingsGeneralPage.xaml`（动态选择器）。
- **Compatibility**: `StyleRendererFactory` 单参构造保持可用（registry 为可选参数），既有测试与调用点零改动；`RadialRenderer` 默认值 `"Default"` 行为不变。
- **Out of scope**: 渲染器资源包（XAML 资源字典）插件化、插件渲染器的本地化显示名（v1 显示 Id）、collectible ALC 卸载后的渲染器实例回收。
