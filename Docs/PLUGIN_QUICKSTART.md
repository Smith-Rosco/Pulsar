# Pulsar 插件系统现代化 - 快速开始指南

## 🎯 概述

Pulsar 插件系统已升级到 v2.0，支持运行时热插拔、版本管理和内存安全卸载。

---

## 🚀 快速开始

### 1. 使用新的插件注册中心

```csharp
// 在 App.xaml.cs 中注册
services.AddSingleton<PluginRegistryV2>();

// 使用
var registry = serviceProvider.GetRequiredService<PluginRegistryV2>();
```

### 2. 加载插件

```csharp
// 加载单个插件
await registry.LoadPluginAsync("path/to/plugin.dll");

// 检查是否加载成功
if (registry.IsPluginLoaded("com.example.plugin"))
{
    Console.WriteLine("插件已加载");
}
```

### 3. 执行插件

```csharp
// 执行方式与旧版本完全相同
var result = await registry.ExecuteAsync(
    pluginId: "com.example.plugin",
    action: "run",
    args: new Dictionary<string, string> { ["param"] = "value" },
    context: pulsarContext
);
```

### 4. 卸载插件

```csharp
// 运行时卸载插件
await registry.UnloadPluginAsync("com.example.plugin");

// 插件内存将被 GC 回收
```

### 5. 热重载插件

```csharp
// 更新插件后无需重启应用
await registry.ReloadPluginAsync("com.example.plugin");
```

---

## 📦 创建支持热插拔的插件

### Step 1: 创建插件清单

在插件 DLL 同目录创建 `plugin.manifest.json`:

```json
{
  "id": "com.example.myplugin",
  "version": "1.0.0",
  "minPulsarVersion": "4.0.0",
  "displayName": "My Awesome Plugin",
  "description": "A plugin that does awesome things",
  "author": "Your Name",
  "license": "MIT",
  "icon": "🚀",
  "entryPoint": "MyNamespace.MyPlugin",
  "dependencies": {},
  "permissions": [
    "clipboard.read",
    "window.focus"
  ],
  "tags": ["Productivity", "Automation"]
}
```

### Step 2: 实现插件接口

```csharp
using Pulsar.Core.Plugin;

namespace MyNamespace
{
    public class MyPlugin : IPulsarPlugin
    {
        public string Id => "com.example.myplugin";
        public string DisplayName => "My Awesome Plugin";
        public string Version => "1.0.0";
        public string Author => "Your Name";
        public string Description => "A plugin that does awesome things";
        public string Icon => "🚀";
        public bool CanDisable => true;

        public void Initialize(IServiceProvider services)
        {
            // 初始化逻辑
        }

        public async Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            // 执行逻辑
            return PluginResult.Success("执行成功");
        }
    }
}
```

### Step 3: 编译和部署

```bash
# 编译插件
dotnet build MyPlugin.csproj

# 部署到 Pulsar 插件目录
# 结构:
# Plugins/
#   com.example.myplugin/
#     MyPlugin.dll
#     plugin.manifest.json
#     (依赖的 DLL)
```

---

## 🔄 版本管理

### 语义化版本

插件使用 [SemVer](https://semver.org/) 版本格式: `MAJOR.MINOR.PATCH`

```json
{
  "version": "2.1.3"
}
```

### 依赖声明

```json
{
  "dependencies": {
    "com.pulsar.pki": "^2.0.0",      // >= 2.0.0 且 < 3.0.0
    "com.pulsar.crypto": "~1.2.0",   // >= 1.2.0 且 < 1.3.0
    "com.example.utils": "1.0.0"     // 精确版本
  }
}
```

### Pulsar 版本兼容性

```json
{
  "minPulsarVersion": "4.0.0",  // 最低要求
  "maxPulsarVersion": "5.0.0"   // 最高支持（可选）
}
```

---

## 🛡️ 权限系统

### 声明权限

```json
{
  "permissions": [
    "clipboard.read",
    "clipboard.write",
    "window.focus",
    "window.enumerate",
    "filesystem.read",
    "filesystem.write",
    "network.access",
    "process.launch"
  ]
}
```

### 权限说明

| 权限 | 描述 |
|------|------|
| `clipboard.read` | 读取剪贴板 |
| `clipboard.write` | 写入剪贴板 |
| `window.focus` | 切换窗口焦点 |
| `window.enumerate` | 枚举所有窗口 |
| `filesystem.read` | 读取文件系统 |
| `filesystem.write` | 写入文件系统 |
| `network.access` | 网络访问 |
| `process.launch` | 启动进程 |

---

## 🔧 高级特性

### 1. 生命周期钩子

```csharp
public class MyPlugin : IPulsarPlugin, IPluginLifecycle
{
    public async Task OnEnableAsync()
    {
        // 插件启用时调用
    }

    public async Task OnDisableAsync()
    {
        // 插件禁用时调用
    }

    public async Task OnUnloadAsync()
    {
        // 插件卸载前调用（清理资源）
    }
}
```

### 2. 可配置插件

```csharp
public class MyPlugin : IPulsarPlugin, IPluginConfigurable
{
    public PluginSettingDefinition GetSettingsDefinition()
    {
        return new PluginSettingDefinition
        {
            Properties = new List<PluginSettingProperty>
            {
                new() { Key = "apiKey", Type = "string", Label = "API Key" }
            }
        };
    }

    public void UpdateSettings(Dictionary<string, object> settings)
    {
        // 应用配置
    }

    public PluginConfigValidationResult ValidateSettings(Dictionary<string, object> settings)
    {
        // 验证配置
        return PluginConfigValidationResult.Valid();
    }
}
```

### 3. 插件元数据

```csharp
public class MyPlugin : IPulsarPlugin, IPluginMetadataProvider
{
    public PluginMetadata GetMetadata()
    {
        return new PluginMetadata
        {
            Id = Id,
            Display = new DisplayInfo
            {
                Name = DisplayName,
                Description = Description,
                IconKey = Icon
            },
            Capabilities = new PluginCapabilities
            {
                SupportedActions = new List<string> { "run", "configure" },
                RequiresForegroundWindow = true
            }
        };
    }
}
```

---

## 🐛 调试技巧

### 1. 检查插件状态

```csharp
var host = registry.GetPluginHost("com.example.plugin");
if (host != null)
{
    Console.WriteLine($"状态: {host.State}");
    Console.WriteLine($"存活: {host.IsAlive}");
    Console.WriteLine($"加载时间: {host.LoadedAt}");
}
```

### 2. 查看加载的程序集

```csharp
var context = host.GetLoadContext();
if (context != null)
{
    var assemblies = context.GetLoadedAssemblies();
    foreach (var asm in assemblies)
    {
        Console.WriteLine($"已加载: {asm.FullName}");
    }
}
```

### 3. 日志记录

```csharp
// 插件中使用 ILogger
public class MyPlugin : IPulsarPlugin
{
    private ILogger<MyPlugin>? _logger;

    public void Initialize(IServiceProvider services)
    {
        _logger = services.GetService<ILogger<MyPlugin>>();
        _logger?.LogInformation("插件初始化完成");
    }
}
```

---

## ⚠️ 注意事项

### 1. 内存管理

- 插件卸载后，确保没有外部引用持有插件对象
- 避免在静态字段中存储插件实例
- 使用 `IPluginLifecycle.OnUnloadAsync()` 清理资源

### 2. 线程安全

- 插件可能在多个线程中被调用
- 使用 `lock` 或 `SemaphoreSlim` 保护共享状态

### 3. 异常处理

- 插件异常会被 Circuit Breaker 捕获
- 3 次失败后插件将被自动禁用 60 秒
- 使用 `PluginResult.Error()` 返回错误而不是抛出异常

---

## 📚 示例项目

查看完整示例:
- `Plugins/Core/Pki/` - PKI 插件（核心插件示例）
- `Plugins/Extensions/BasicCommand/` - 基础命令插件（扩展插件示例）

---

## 🆘 常见问题

### Q: 插件卸载后内存没有释放？

**A**: 检查是否有外部引用持有插件对象。使用 dotMemory 分析内存泄漏。

### Q: 热重载后插件状态丢失？

**A**: 在 `OnUnloadAsync()` 中保存状态，在 `OnEnableAsync()` 中恢复。

### Q: 依赖冲突导致插件加载失败？

**A**: 确保插件清单中正确声明了依赖版本，使用独立的插件目录。

### Q: LSP 显示 NuGet.Versioning 错误？

**A**: 这是 IDE 缓存问题，运行 `dotnet build` 确认实际构建成功即可。

---

## 📞 获取帮助

- 查看详细文档: `Docs/PLUGIN_SYSTEM_MODERNIZATION_PHASE1.md`
- 查看架构文档: `ARCHITECTURE.md`
- 查看插件开发指南: `PLUGIN_DEVELOPMENT.md`

---

*最后更新: 2026-03-02*
