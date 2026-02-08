# Pulsar 开发文档 - 插件系统重构指南

**版本**: v4.0.0-dev
**基于**: ARCHITECTURE.md v2.0 + 当前实现 v3.2.0-beta
**目标读者**: AI 开发助手
**最后更新**: 2026-02-07
**策略**: **不兼容旧配置 (Breaking Change)** - 直接切换到新架构

## 📋 文档目的

本文档为 AI Agent 提供从当前 `ActionRegistry` 架构向 ARCHITECTURE.md 定义的完整插件系统迁移的执行指南。
**注意**: 本次重构不保留对 `appsettings.json` 的兼容性支持。系统将强制使用 `Profiles.json`。

---

## 🎯 重构目标

### 核心目标
1. ✅ **统一上下文传递**: 实现 `PulsarContext` 结构，消除插件自行获取窗口信息的开销
2. ✅ **插件接口标准化**: 从 `IActionHandler` 升级为 `IPulsarPlugin`
3. ✅ **动态插件加载**: 支持从 `Plugins/` 目录加载外部 C# DLL
4. ✅ **配置系统重写**: 废弃 `AppConfig`，全面启用 `ProfilesConfig` (对应 `Profiles.json`)
5. ⏳ **FFI 支持** (Phase 3): 为 Rust DLL 预留接口

### 保持不变
- ❌ **不改**: 双模态物理隔离 (Launcher vs Command Mode)
- ❌ **不改**: 幽灵窗口驻留机制
- ❌ **不改**: PKI 的焦点回旋镖逻辑

---

## 📂 目录结构规划

### 目标结构 (v4.0.0)
```
Pulsar/
├── Core/
│   ├── Plugin/
│   │   ├── IPulsarPlugin.cs          # [NEW] 插件接口
│   │   ├── PulsarContext.cs          # [NEW] 上下文对象
│   │   ├── PluginResult.cs           # [NEW] 执行结果
│   │   └── PluginLoader.cs           # [NEW] 插件加载器
│   └── Adapters/
│       └── LegacyHandlerAdapter.cs   # [TEMP] 用于代码过渡的适配器
├── Plugins/                          # [NEW] 内置插件目录
│   ├── WinSwitcher/
│   │   └── WinSwitcherPlugin.cs      # [MIGRATE] 从 LauncherHandler
│   ├── VbaRunner/
│   │   └── VbaAdapterPlugin.cs       # [NEW] EXE 调用封装
│   └── RustPki/                      # [FUTURE] Rust 版本 PKI
├── Features/Pki/
│   └── PkiPlugin.cs                  # [REFACTOR] 从 PkiHandler 改造
├── Models/
│   └── ProfilesConfig.cs             # [NEW] 唯一配置模型 (原 AppConfig 删除)
└── Services/
    ├── PluginRegistry.cs             # [NEW] 替代 ActionRegistry
    └── ConfigService.cs              # [REWRITE] 仅读取 Profiles.json
```

---

## 🔧 技术规范

### 1. 核心接口定义

#### 1.1 PulsarContext.cs
```csharp
// File: Core/Plugin/PulsarContext.cs
namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// 不可变的上下文快照，在唤起轮盘瞬间冻结
    /// </summary>
    public readonly struct PulsarContext
    {
        // === 窗口信息 ===
        public IntPtr TargetWindowHandle { get; init; }
        public string TargetProcessName { get; init; }  // 大写，如 "EXCEL"
        public int TargetProcessId { get; init; }

        // === 用户输入 ===
        public string? SelectedText { get; init; }      // 预读取的选中文本
        public string? ClipboardText { get; init; }

        // === 共享存储 (用于插件间通信) ===
        public IReadOnlyDictionary<string, object>? SessionData { get; init; }

        // === 工厂方法 ===
        public static PulsarContext Capture(IWindowService windowService)
        {
            var hwnd = windowService.GetPreviousWindow();
            var processName = windowService.GetProcessName(hwnd);
            var pid = windowService.GetProcessId(hwnd);

            return new PulsarContext
            {
                TargetWindowHandle = hwnd,
                TargetProcessName = processName?.ToUpperInvariant() ?? string.Empty,
                TargetProcessId = pid,
                ClipboardText = Clipboard.GetText()
            };
        }
    }
}
```

**执行约束**:
- ✅ 必须在 `RadialMenuViewModel.Show()` 方法的**第一行**调用 `PulsarContext.Capture()`
- ✅ 禁止在 `PulsarContext` 创建后修改其字段
- ⚠️ `SelectedText` 读取建议异步预热

#### 1.2 IPulsarPlugin.cs
```csharp
// File: Core/Plugin/IPulsarPlugin.cs
namespace Pulsar.Core.Plugin
{
    public interface IPulsarPlugin
    {
        /// <summary>
        /// 插件唯一标识符 (建议使用反向域名，如 "com.pulsar.pki")
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 插件显示名称
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 冷启动初始化 (在 App 启动时调用一次)
        /// </summary>
        void Initialize(IServiceProvider services);

        /// <summary>
        /// 执行插件动作
        /// </summary>
        /// <param name="action">动作名 (如 "run", "fill")</param>
        /// <param name="args">静态参数 (来自 Profiles.json)</param>
        /// <param name="context">运行时上下文</param>
        Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context
        );
    }
}
```

#### 1.3 PluginResult.cs
```csharp
// File: Core/Plugin/PluginResult.cs
namespace Pulsar.Core.Plugin
{
    public enum VisualCue
    {
        None,
        ShowCheckMark,      // 绿色勾号 Toast
        ShakeWindow,        // 窗口抖动
        ErrorRed            // 红色边框闪烁
    }

    public readonly struct PluginResult
    {
        public bool Success { get; init; }
        public string? Message { get; init; }
        public VisualCue Cue { get; init; }

        public static PluginResult Ok(string? message = null) =>
            new() { Success = true, Message = message, Cue = VisualCue.ShowCheckMark };

        public static PluginResult Error(string message) =>
            new() { Success = false, Message = message, Cue = VisualCue.ErrorRed };
    }
}
```

---

### 2. 配置系统重写

#### 2.1 新配置结构 (Profiles.json)
这是**唯一**支持的配置文件格式。

```json
{
  "Settings": {
    "CenterSlotBehavior": "MRU_Window",
    "TriggerDistance": 100.0,
    "LauncherTheme": "Dark"
  },
  "Profiles": {
    "EXCEL": {
      "CommandMode": {
        "Slot_1": {
          "plugin": "com.pulsar.vba",
          "action": "run",
          "args": { "script": "format.vbs" }
        },
        "Slot_3": {
          "plugin": "com.pulsar.pki",
          "action": "fill",
          "args": { "secretId": "rdp-server-a" }
        }
      }
    },
    "Global": {
      "SwitchMode": {
        "Slot_1": {
          "plugin": "com.pulsar.winswitcher",
          "action": "activate",
          "args": { "app": "chrome" }
        }
      }
    }
  }
}
```

#### 2.2 配置模型
```csharp
// File: Models/ProfilesConfig.cs
namespace Pulsar.Models
{
    public class ProfilesConfig
    {
        public ProfileSettings Settings { get; set; } = new();
        public Dictionary<string, ProcessProfile> Profiles { get; set; } = new();
    }

    public class ProcessProfile
    {
        public Dictionary<string, PluginSlot>? CommandMode { get; set; }
        public Dictionary<string, PluginSlot>? SwitchMode { get; set; }
    }

    public class PluginSlot
    {
        [JsonPropertyName("plugin")]
        public string PluginId { get; set; } = string.Empty;

        [JsonPropertyName("action")]
        public string Action { get; set; } = string.Empty;

        [JsonPropertyName("args")]
        public Dictionary<string, string> Args { get; set; } = new();
    }
}
```

---

### 3. 插件加载器

#### 3.1 PluginLoader.cs
```csharp
// File: Core/Plugin/PluginLoader.cs
namespace Pulsar.Core.Plugin
{
    public class PluginLoader
    {
        private readonly string _pluginDirectory;
        private readonly IServiceProvider _services;

        public PluginLoader(IServiceProvider services, string pluginDir)
        {
            _services = services;
            _pluginDirectory = pluginDir;
        }

        public List<IPulsarPlugin> LoadAll()
        {
            var plugins = new List<IPulsarPlugin>();

            // 1. 加载内置插件 (当前程序集)
            var builtinPlugins = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IPulsarPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .Select(t => (IPulsarPlugin)Activator.CreateInstance(t)!)
                .ToList();
            plugins.AddRange(builtinPlugins);

            // 2. 加载外部插件 (Plugins/ 目录)
            // (代码略：遍历 DLL -> Assembly.LoadFrom -> 实例化)
            
            // 3. 初始化
            foreach (var plugin in plugins)
                plugin.Initialize(_services);

            return plugins;
        }
    }
}
```

---

## 📝 TODO 清单

### Phase 1: 核心接口与基础层

#### TODO-1.1: 创建插件接口文件
- [ ] 创建 `Core/Plugin/PulsarContext.cs`
- [ ] 创建 `Core/Plugin/IPulsarPlugin.cs`
- [ ] 创建 `Core/Plugin/PluginResult.cs`
- [ ] 创建 `Core/Plugin/PluginLoader.cs`

**验证标准**: `dotnet build` 编译成功。

#### TODO-1.2: 改造 WindowService
- [ ] 在 `IWindowService` 增加上下文捕获能力
- [ ] 修改 `RadialMenuViewModel`，在 `Show()` 第一行调用 `CaptureContext()` 并存储

### Phase 2: 配置系统替换 (Breaking Change)

#### TODO-2.1: 实现新配置模型
- [ ] 创建 `Models/ProfilesConfig.cs`
- [ ] 彻底删除 `Models/AppConfig.cs`
- [ ] 彻底删除 `Models/GridItemBase.cs` 及其子类 (LauncherItem, CommandItem)
  - *注意*: 需要新的 UI ViewModel 来绑定 `PluginSlot`，或者重写 `GridItemBase` 仅作为 UI 模型而非存储模型。

#### TODO-2.2: 重写 ConfigService
- [ ] 修改 `ConfigService.cs`，不再读取 `appsettings.json`
- [ ] 实现 `LoadProfiles()` 读取 `Profiles.json`
- [ ] 若文件不存在，写入默认的 `Profiles.json` 模板

**验证标准**:
- 启动应用，检查目录下生成了 `Profiles.json`
- `appsettings.json` 被忽略

### Phase 3: 业务逻辑迁移

#### TODO-3.1: 迁移 PKI 为插件
- [ ] 创建 `Features/Pki/PkiPlugin.cs` 实现 `IPulsarPlugin`
- [ ] 将 `PkiHandler` 的逻辑移入插件
- [ ] 废弃 `PkiHandler` 类
- [ ] 修改 `RadialMenuViewModel`，当点击 PKI 类型的 Slot 时，调用 `plugin.ExecuteAsync`

#### TODO-3.2: 迁移 Launcher 为插件
- [ ] 创建 `Plugins/WinSwitcher/WinSwitcherPlugin.cs`
- [ ] 实现 `activate` 和 `launch` 动作
- [ ] 废弃 `LauncherHandler`

#### TODO-3.3: 迁移 Command 为插件
- [ ] 创建 `Plugins/BasicCommand/SimpleCommandPlugin.cs`
- [ ] 废弃 `SimpleCommandHandler`

### Phase 4: UI 适配

#### TODO-4.1: UI 绑定重构
- [ ] 由于 `GridItemBase` 结构改变，需更新 `RadialMenuViewModel` 的数据绑定逻辑
- [ ] 确保 UI 能正确从 `ProfilesConfig` 加载图标、标题
- [ ] 修复设置界面 (`SettingsViewModel`)，使其读写 `Profiles.json`

---

## ⚠️ 关键技术约束

### 1. 焦点管理铁律
```csharp
// ❌ 错误: 在插件内部查找窗口
var hwnd = GetForegroundWindow(); // 此时已经是 Pulsar 自己!

// ✅ 正确: 使用上下文传递
var hwnd = context.TargetWindowHandle; // 唤起瞬间捕获的句柄
```

### 2. 插件异常隔离
插件执行必须包裹在 try-catch 中，插件崩溃不应导致主程序退出。

### 3. "No Backward Compatibility" 策略
- 遇到旧的 `appsettings.json` -> **忽略**
- 遇到旧的代码 (如 `LauncherItem`) -> **删除/重写**
- 遇到编译错误 -> **修复** (不要为了兼容保留死代码)

---

## 🔄 执行建议

1. **Phase 1** 搭建地基。
2. **Phase 2** 是最痛苦的 "Breaking Change"，一旦开始，原来的 UI 和逻辑会全部断开。建议一口气完成 Phase 2 和 Phase 3 的核心部分，才能让应用重新运行起来。
3. 如果 UI 绑定报错太多，可以先创建一个临时的 `GridItemViewModel` 用于 View 层显示，将 `PluginSlot` 映射过去。

---
