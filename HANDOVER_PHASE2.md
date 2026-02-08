# Pulsar 插件系统重构交接文档 (Phase 2 -> Phase 3)

**日期**: 2026-02-07
**状态**: Phase 2 完成 (配置系统替换)
**下一步**: Phase 3 (业务逻辑迁移)

---

## 📋 当前进展

我们正在按照 `DEV_PLUGIN_SYSTEM.md` 对 Pulsar 进行重大重构，目标是引入基于插件的架构。

### ✅ 已完成阶段

#### Phase 1: 核心接口与基础层
- 创建了 `PulsarContext` (上下文快照)
- 定义了 `IPulsarPlugin` 接口和 `PluginResult`
- 实现了 `PluginLoader` (支持内置和外部 DLL 加载)
- 验证：项目编译通过

#### Phase 2: 配置系统替换 (Breaking Change)
- 创建了新的配置模型 `ProfilesConfig` (对应 `Profiles.json`)
- 重写了 `ConfigService`，不再读取 `appsettings.json`
- 删除了旧的 `AppConfig` (但保留了 **临时兼容层** 以通过编译)
- 更新了 `SettingsViewModel` 和 `RadialMenuViewModel` 使用临时兼容方法
- 验证：项目编译通过，生成默认 `Profiles.json`

---

## ⚠️ 关键技术说明 (给接手者)

为了保持项目可编译，我在 **Phase 2** 采取了渐进式策略，引入了一些**临时代码**，需要在后续阶段清理。

### 1. 临时兼容层 (`Models/AppConfig.cs`)
-保留了 `AppConfig`, `AppSettings`, `GridItemBase`, `LauncherItem`, `CommandItem` 类。
- **注意**: 这些类被标记为 `[Obsolete]`，仅用于让现有的 UI 代码 (ViewModel) 不报错。
- **计划**: 在 **Phase 4 (UI 适配)** 中，随着 ViewModel 的重构，这些类将被彻底删除。

### 2. 临时服务方法 (`IConfigService`)
- 增加了 `LoadLegacyAsync()` 和 `SaveLegacyAsync()`。
- **作用**: 返回空的/默认的 `AppConfig` 对象，骗过旧的 ViewModel。
- **计划**: 在 **Phase 4** 中移除。

### 3. SecretItem 的特殊处理
- `SecretItem` (PKI 模块) 暂时继承自临时的 `GridItemBase`。
- 实现了独立的 `INotifyPropertyChanged` 以支持数据绑定。

---

## 🚀 下一步任务 (Phase 3: 业务逻辑迁移)

接下来的工作重点是将现有的硬编码业务逻辑迁移为独立的插件。

### 1. 迁移 PKI 为插件 (Priority: High)
- [ ] 创建 `Features/Pki/PkiPlugin.cs` 实现 `IPulsarPlugin`
- [ ] 将 `PkiHandler` 的逻辑移入插件
- [ ] 废弃 `PkiHandler` 类
- [ ] 修改 `RadialMenuViewModel`，当点击 PKI 类型的 Slot 时，调用 `plugin.ExecuteAsync`

### 2. 迁移 Launcher 为插件
- [ ] 创建 `Plugins/WinSwitcher/WinSwitcherPlugin.cs`
- [ ] 实现 `activate` 和 `launch` 动作
- [ ] 废弃 `LauncherHandler`

### 3. 迁移 Command 为插件
- [ ] 创建 `Plugins/BasicCommand/SimpleCommandPlugin.cs`
- [ ] 废弃 `SimpleCommandHandler`

---

## 🛠️ 如何开始

1. **阅读文档**: 仔细阅读根目录下的 `DEV_PLUGIN_SYSTEM.md`，特别是 **Phase 3** 部分。
2. **检查代码**: 查看 `Core/Plugin/` 目录下的接口定义。
3. **执行任务**: 按照上述清单逐个迁移功能模块。

**提示**:
- 在迁移逻辑时，尽量复用现有的 Service (如 `IWindowService`)。
- 插件的 `ExecuteAsync` 方法应返回 `PluginResult`。
- 别忘了在 `PluginLoader` 中注册新创建的内置插件 (如果它们在主程序集中，PluginLoader 会自动发现)。

祝好运！
