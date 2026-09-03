# Lesson: 外部插件运行时安装/卸载的五个坑（discovery 快照、懒激活、ALC 文件锁、GC 延迟、残骸目录）

> 2026-09-03 · renderer-plugin-registry 联调中发现 · commit `fc08ac5` + `223a36a` + `f4c8ae9`

## 症状

1. **安装成功但插件永远不激活**：安装日志无报错，但 `Profiles.json` 的 `plugins` 区为空，插件 `OnEnableAsync` 从未运行。
2. **运行中卸载必失败**：`Directory.Delete(path, recursive: true)` 抛 `IOException`，且失败后留下「部分卸载」的残骸目录。
3. **残骸目录死锁**：残骸目录（只剩 DLL、无 manifest）既不出现在插件列表（无法卸载），又让覆盖安装报 "already installed"。
4. **重启后插件贡献消失**：安装时渲染器注册成功、下拉能选到；重启后「Discovered 1 plugins」但渲染器下拉里没有该插件——`OnEnableAsync` 没跑。
5. **卸载偶发 `Access to path … denied`**：日志里 `Unloaded assembly context` + `Deactivated plugin` 都执行了，但紧接着的目录删除仍 5 次失败。

## 根因

### 1. Discovery 是启动时的一次性快照

`PluginCatalog` 的 descriptor 只在 `AppStartupCoordinator → DiscoverDeferredAsync()` 注册一次。
运行时安装 ZIP 只写磁盘；安装流程随即调 `GrantPermissionsAsync`，而它内部
`GetDescriptor(pluginId)` 查不到 → **只记一条 WARN 就静默返回**（不抛错！），授权永远落不了盘。

```
安装 → InstallFromFileAsync(写磁盘) → GrantPermissionsAsync(查 catalog = null → WARN 返回)
```

**修复**：`PluginRuntimeKernel.RefreshDiscoveryAsync()`（`InvalidateDiscoveryCache()` + 重扫扩展目录），
安装流程改为「刷新发现 → 授权 → `GetOrActivatePluginAsync` 立即激活（默认 profile `Enabled=true`）」。

**教训**：任何"安装后立刻操作 catalog/state"的流程都必须先刷新发现；`GrantPermissionsAsync`
这类静默失败（WARN 不抛错）的 API 在 UI 流程里调用时要检查副作用是否真的发生。

### 2. Collectible ALC 不 Unload = DLL 永久锁定

`PluginLoadContext : AssemblyLoadContext(isCollectible: true)`，但 discovery 加载程序集做反射后
**没有人跟踪这个上下文、也没有人调 `Unload()`** → DLL 文件句柄保持到进程退出 →
运行中删除插件目录必然 `IOException`（Windows 文件锁）。

**修复**：
- `PluginLoadContext.IsUnloadInitiated` / `InitiateUnload()`（卸载后的 ALC 不可复用）
- `PluginLoader._externalContexts`（按插件文件夹名 = pluginId 跟踪），`TryUnloadExternalContext(pluginId)`
- `PluginRuntimeKernel.DeactivatePluginAsync(pluginId)`：完整停用链
  `OnUnloadAsync → 移除 state store → 移除 catalog descriptor → UnregisterOwner(渲染器) →
  InvalidateDiscoveryCache → Unload ALC`

**教训**：`isCollectible: true` 只是"可以卸载"，不是"自动卸载"。必须显式跟踪 + 调 `Unload()`，
且卸载前要断开所有强引用（插件实例、descriptor.ImplementationType、discovery 缓存里的旧 descriptor）。

### 3. 卸载的删除顺序 + 残骸目录识别

- `Directory.Delete(recursive)` 逐文件删除，**先删了 manifest.json、卡在被锁的 DLL** → 半残目录。
- `IsPluginInstalled` 只查 `Directory.Exists` → 残骸被误判为已安装。
- 扫描器（`LocalPluginScanner`）要求 manifest → 残骸不上列表 → 无卸载入口。

**修复**：
- `IsPluginInstalled` 要求目录内存在 `plugin.manifest.json` / `manifest.json`（残骸 = 未安装）
- `InstallFromFileAsync` 安装前清理残骸目录（`DeleteDirectoryWithRetry`，5 次 × 250ms——
  collectible ALC 卸载后文件句柄释放有短暂异步延迟）
- 卸载顺序：**回收权限（descriptor 还在 catalog 时）→ Deactivate → 删文件**

### 4. 懒激活：外部插件重启后环境贡献消失

外部插件启动时只**发现**（descriptor 入 catalog），不**激活**。渲染器等贡献是在
`OnEnableAsync` 里向 `IRadialRendererRegistry` 注册的；不激活 → 注册代码不执行 → 重启后下拉少一项。

**修复**：`AppStartupCoordinator` 在 `DiscoverDeferredAsync()` 后立即遍历
`GetAllPluginDescriptors()`，对 `IsExternal && IsPluginEnabled` 的插件 `GetOrActivatePluginAsync`。

**教训**：区分"发现（discover）"与"激活（activate）"两个阶段。凡是在生命周期钩子里注册
环境贡献（渲染器、协议、菜单项）的插件，启动时就必须走激活；纯动作插件可以继续懒激活。

### 5. Collectible ALC 的 Unload() 是异步的（GC 驱动）

`AssemblyLoadContext.Unload()` **只是发起**拆卸，真正的类型/程序集回收要等 GC 收集该上下文。
在此之前 DLL 文件锁不释放 → `DeactivatePluginAsync` 之后立刻删目录仍抛 access-denied。

**修复**：`DeactivatePluginAsync` 在 `TryUnloadExternalContext` 成功后强制回收：

```csharp
if (_loader.TryUnloadExternalContext(pluginId))
{
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
}
```

**教训**：`Unload()` 返回 ≠ 文件锁已释放。删除插件目录前必须确定性 GC 回收，否则只能靠
`DeleteDirectoryWithRetry` 的延迟重试碰运气。

## 排查入口

| 信号 | 位置 |
|---|---|
| `Cannot grant permissions for unknown plugin` | `%APPDATA%/Pulsar/Logs/pulsar-*.log` |
| `External plugin folder has no valid manifest and was skipped` | 同上（残骸目录特征） |
| `Plugin {id} is already installed. Please uninstall it first.` | 覆盖安装被残骸目录挡住 |
| 重启后 `Discovered N plugins` 但无 `Activated plugin` | 懒激活，`AppStartupCoordinator` 未跑激活链 |
| `Unloaded assembly context` 后仍 `Access to path … denied` | ALC 卸载是 GC 驱动的，删除前未强制回收 |
| 插件日志无 `OnEnableAsync` 记录 | `%APPDATA%/Pulsar/Logs/Plugins/{pluginId}-*.log` |
