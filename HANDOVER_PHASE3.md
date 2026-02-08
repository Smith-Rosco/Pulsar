# Pulsar 插件系统重构交接文档 (Phase 3 -> Phase 4)

**日期**: 2026-02-07
**状态**: Phase 3 完成 (业务逻辑迁移)
**下一步**: Phase 4 (UI 适配与旧代码清理)

---

## 📋 当前进展

我们已经完成了业务逻辑向插件架构的迁移。现在系统中存在两套并行机制：
1.  **新插件系统**: `PluginRegistry` 已在 `App.xaml.cs` 中初始化，加载了 `WinSwitcher`、`BasicCommand` 和 `Pki` 三个插件。
2.  **旧 Handler 系统**: 为了保持项目可编译且 UI 不崩溃，旧的 `ActionRegistry` 和 `*Handler` 类仍然被保留并注册，但已被标记为 `[Obsolete]`。

### ✅ 已完成工作 (Phase 3)

#### 1. 插件实现
-   **PkiPlugin**: 实现了 `IPulsarPlugin`，替代了 `PkiHandler`。支持 `fill` (或 `inject`) 动作。
-   **WinSwitcherPlugin**: 实现了 `IPulsarPlugin`，替代了 `LauncherHandler`。支持 `activate`、`launch`、`switch` 动作。
-   **SimpleCommandPlugin**: 实现了 `IPulsarPlugin`，替代了 `SimpleCommandHandler`。支持 `run`、`sendkeys` 动作。

#### 2. 基础设施
-   **PluginRegistry**: 创建了新的插件注册中心，负责加载和执行插件。
-   **App.xaml.cs**: 更新了依赖注入容器，同时注册了新旧两套系统。

#### 3. 兼容性处理
-   所有旧的 Handler 类 (`LauncherHandler`, `SimpleCommandHandler`, `PkiHandler`, `ActionRegistry`) 均已标记为 `[Obsolete]`，将在 Phase 4 中删除。

---

## 🚀 下一步任务 (Phase 4: UI 适配)

接下来的工作是将 UI 层 (`ViewModels`) 切换到新的插件系统，并彻底移除旧代码。

### 1. ViewModel 重构 (Priority: High)
- [ ] **RadialMenuViewModel**:
    -   不再依赖 `ActionRegistry`。
    -   注入 `PluginRegistry`。
    -   在 `ExecuteSelection` 中，根据 `PluginSlot` 的信息调用 `pluginRegistry.ExecuteAsync`。
    -   移除对 `GridItemBase` 及其子类的依赖，改用 `PluginSlot` 数据模型。

### 2. 配置绑定更新
- [ ] **SlotViewModel**:
    -   更新属性以适配 `PluginSlot` (Label, IconKey, IsActive 等)。
- [ ] **SettingsViewModel**:
    -   更新设置界面的保存逻辑，确保写入 `Profiles.json` 而非旧格式。

### 3. 清理旧代码 (The Great Cleanup)
- [ ] 移除 `Models/AppConfig.cs` 及相关旧模型。
- [ ] 移除 `Core/Handlers/` 下的所有旧 Handler。
- [ ] 移除 `Services/ActionRegistry.cs`。
- [ ] 清理 `App.xaml.cs` 中被标记为临时的注册代码。

---

## ⚠️ 注意事项

-   **编译警告**: 目前编译时会出现大量关于使用过时类 (`CS0618`) 的警告，这是正常的，随着 Phase 4 的进行，这些警告将自然消失。
-   **插件参数**: 新的插件系统通过 `Dictionary<string, string> args` 传递参数。在对接 UI 时，请确保 `PluginSlot.Args` 被正确传递给 `ExecuteAsync`。
-   **PKI**: `PkiPlugin` 需要 `secretId` 参数（GUID 字符串）。在 UI 绑定时需注意这一点。

祝 Phase 4 顺利！
