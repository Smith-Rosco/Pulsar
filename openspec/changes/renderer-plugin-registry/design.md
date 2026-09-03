# Design: Renderer Plugin Registry

## 约束回顾

1. 宿主 `IServiceProvider` 构建后不可变 → 不能运行时 `AddSingleton`，必须用独立可变注册表。
2. `IRadialRenderer` 定义在宿主程序集；`PluginLoadContext` 对宿主程序集回退默认 ALC，接口类型可对齐——但插件不得自带 `Pulsar.dll` 副本（文档约束，非代码可查）。
3. `PluginExecutionContext` 仅在执行期开启（`ExecuteCoreAsync` 的 `BeginScope`），**激活/`OnEnableAsync` 阶段没有环境上下文** → 注册 API 必须显式传 `ownerId`，不能用 AsyncLocal 自动归属。
4. 权限模型：未知令牌一律视为拒绝；`PluginProfile.GrantedPermissions` 是唯一授权事实源。

## 关键裁决

### D1 注册表只放插件渲染器，内置保持 DI 单例

内置 3 渲染器继续走 `IEnumerable<IRadialRenderer>` 构造注入。注册表仅承载插件贡献项。理由：
- 内置项无需 owner 生命周期，放进注册表徒增清理语义；
- `GetService<IRadialRenderer>()` 回落 Default 的既有语义（Default 注册在最后）不被扰动；
- 测试既有构造 `new StyleRendererFactory(renderers)` 保持兼容。

### D2 注册 API 带 owner，内核无条件清理

`Register(IRadialRenderer renderer, string ownerId)` / `UnregisterOwner(string ownerId)`。ownerId = 插件 Id。`PluginRuntimeKernel` 在禁用完成与 `UnloadAllAsync` 循环内调用 `UnregisterOwner(pluginId)`——**即使插件自己的 `OnDisableAsync` 没注销也兜底**，防 `SlotOrb`/装饰 Canvas 残留引用。清理在 `OnDisableAsync` 之后执行，插件若主动注销过则幂等（返回 0）。

### D3 防遮蔽：内置 id 列入注册表保留名单

插件若注册 `Default`/`ClassicRing`/`Glassmorphism` 同名 id，将劫持渲染选择。注册表构造接收保留 id 集（App.xaml.cs 用 3 个内置 `RendererId` 常量组装），`Register` 拒绝保留 id 与重复 id，返回 `false` 并由调用方日志。

### D4 权限校验以委托注入，Core/Rendering 不依赖插件运行时

`RadialRendererRegistry(Func<string?, bool>? canRegisterOwner)`。App.xaml.cs 组装：

```csharp
ownerId => configService.GetSnapshot().Plugins.TryGetValue(ownerId, out var p)
           && p.GrantedPermissions.Contains(PluginPermissions.UiRender)
```

未知 owner / 未授予 `ui.render` → `Register` 返回 false。内置插件（Tier=Core）不经过注册表，天然不受限；外部插件必须先在 manifest 声明 `ui.render` 并获用户授权——既有的激活前权限门控（`PluginPermissionService.Evaluate`）保证未授权插件根本不会被激活，注册表校验是纵深防御。

### D5 SlotOrb 静态缓存订阅 Changed 失效

`SlotOrb` 缓存键是 config revision，注册表变化不 bump revision → 插件渲染器被清理后缓存残留悬挂实例。首次解析时懒订阅 `registry.Changed`（静态一次性），处理器清空 `_cachedRenderer`/`_cachedRendererRevision`，下次 hover 重新经工厂解析（回落 Default）。VM 侧 `ApplyRadialRendering` 每次唤出都重新 `Create`，无需处理。

### D6 设置页动态枚举

`SettingsViewModel.General` 新增 `RendererOptions`（`Id` + `DisplayName`）：内置 3 项经 `_loc` 取既有键（`Settings.Appearance.RendererStyle.*`），插件项 DisplayName = 原始 Id；订阅 `Changed` 刷新集合。XAML 从硬编码 `ComboBoxItem` 改为 `ItemsSource` + `SelectedValuePath="Id"`。配置里残留已卸载插件的 id 时：运行期回落 Default（工厂既有语义），ComboBox 显示空（不做伪选项）。

## 风险与放弃项

- **collectible 卸载**：当前 `UnloadAllAsync` 不卸载 ALC；若未来启用，插件渲染器实例回收需配合注册表强清理（已由 D2 铺路）。
- **插件渲染器线程亲和性**：`RenderDecorations` 触碰 WPF 可视树，插件需自行保证 UI 线程调度——在 PLUGIN_DEVELOPMENT.md 的插件开发者视角补一句提示（文档任务）。
- **放弃**：注册表持久化（重启后插件重新注册，无需持久层）；渲染器资源包（XAML dict）插件化（后续 change）。
