# Pulsar Plugin Template

这是一个 Pulsar 插件开发模板，帮助你快速创建自己的插件。

## 📁 文件结构

```
PluginTemplate/
├── ExamplePlugin.csproj    # 项目文件
├── ExamplePlugin.cs         # 插件主代码
├── manifest.json            # 插件元数据
└── README.md                # 本文档
```

## 🚀 快速开始

### 1. 复制模板

```bash
cp -r PluginTemplate MyAwesomePlugin
cd MyAwesomePlugin
```

### 2. 修改项目名称

重命名文件：
- `ExamplePlugin.csproj` → `MyAwesomePlugin.csproj`
- `ExamplePlugin.cs` → `MyAwesomePlugin.cs`

### 3. 修改代码

**MyAwesomePlugin.cs**:
```csharp
namespace MyAwesomePlugin  // 修改命名空间
{
    public class MyAwesomePlugin : IPulsarPlugin  // 修改类名
    {
        public string Id => "com.yourname.awesomeplugin";  // 修改 ID
        public string DisplayName => "My Awesome Plugin";   // 修改名称
        // ... 其他属性
    }
}
```

**manifest.json**:
```json
{
  "Id": "com.yourname.awesomeplugin",
  "DisplayName": "My Awesome Plugin",
  "Version": "1.0.0",
  "EntryPoint": "MyAwesomePlugin.MyAwesomePlugin"  // 命名空间.类名
}
```

### 4. 编译插件

```bash
dotnet build -c Release
```

编译成功后，会在 `bin/Release/net8.0-windows/` 目录生成：
- `MyAwesomePlugin.dll` - 插件主文件
- `manifest.json` - 元数据文件
- `MyAwesomePlugin-1.0.0.zip` - 打包好的插件包

### 5. 安装插件

**方法 A：手动复制**
```bash
# 复制到 Pulsar 插件目录
cp -r bin/Release/net8.0-windows/* "%AppData%\Pulsar\Plugins\com.yourname.awesomeplugin\"
```

**方法 B：使用 Marketplace**
1. 打开 Pulsar → Settings → Marketplace
2. 点击 "Install from File"
3. 选择 `MyAwesomePlugin-1.0.0.zip`
4. 重启 Pulsar

## 📚 插件开发指南

### IPulsarPlugin 接口

所有插件必须实现 `IPulsarPlugin` 接口：

```csharp
public interface IPulsarPlugin : IDisposable
{
    string Id { get; }              // 唯一标识符
    string DisplayName { get; }     // 显示名称
    string Description { get; }     // 描述
    string Version { get; }         // 版本号
    string Author { get; }          // 作者
    string Icon { get; }            // 图标

    void Initialize(IServiceProvider serviceProvider);
    Task<PluginResult> ExecuteAsync(string action, string[] args, PulsarContext context);
}
```

### PulsarContext 上下文

`PulsarContext` 提供了丰富的上下文信息：

```csharp
public async Task<PluginResult> ExecuteAsync(string action, string[] args, PulsarContext context)
{
    // 获取当前活动窗口
    var window = context.ActiveWindow;
    var title = window?.Title;
    var processName = window?.ProcessName;

    // 获取剪贴板内容
    var clipboardText = await context.GetClipboardTextAsync();

    // 设置剪贴板
    await context.SetClipboardTextAsync("Hello World");

    // 模拟按键
    await context.SendKeysAsync("{CTRL}C");

    // 显示通知
    return PluginResult.Success("操作成功！");
}
```

### 插件动作（Actions）

插件可以定义多个动作：

```csharp
public async Task<PluginResult> ExecuteAsync(string action, string[] args, PulsarContext context)
{
    switch (action.ToLower())
    {
        case "hello":
            return await SayHelloAsync(context);

        case "copy":
            return await CopyTextAsync(args, context);

        case "search":
            return await SearchAsync(args, context);

        default:
            return PluginResult.Failed($"Unknown action: {action}");
    }
}
```

### 返回结果

使用 `PluginResult` 返回执行结果：

```csharp
// 成功（会显示通知）
return PluginResult.Success("操作成功！");

// 失败（会显示错误通知）
return PluginResult.Failed("操作失败：文件不存在");

// 静默成功（不显示通知）
return PluginResult.Silent();
```

### 依赖注入

在 `Initialize` 方法中获取服务：

```csharp
private ILogger<MyPlugin>? _logger;

public void Initialize(IServiceProvider serviceProvider)
{
    _logger = serviceProvider.GetService<ILogger<MyPlugin>>();
    _logger?.LogInformation("Plugin initialized");
}
```

## 🎨 manifest.json 字段说明

| 字段 | 必需 | 说明 | 示例 |
|------|------|------|------|
| `Id` | ✅ | 唯一标识符（反向域名） | `com.yourname.plugin` |
| `DisplayName` | ✅ | 显示名称 | `My Plugin` |
| `Version` | ✅ | 版本号（语义化版本） | `1.0.0` |
| `Description` | ✅ | 插件描述 | `A useful plugin` |
| `Author` | ✅ | 作者名称 | `Your Name` |
| `EntryPoint` | ✅ | 入口类（命名空间.类名） | `MyPlugin.MyPlugin` |
| `Icon` | ❌ | 图标（Emoji/路径/Key） | `🔌` |
| `Tags` | ❌ | 标签数组 | `["productivity"]` |
| `Dependencies` | ❌ | 依赖插件 | `{"other-plugin": ">=1.0.0"}` |
| `MinPulsarVersion` | ❌ | 最低 Pulsar 版本 | `4.0.0` |

## 📦 发布插件

### 1. 创建 GitHub Release

```bash
git tag v1.0.0
git push origin v1.0.0
```

在 GitHub 上创建 Release，上传 `MyAwesomePlugin-1.0.0.zip`

### 2. 添加到 Marketplace

编辑 `%AppData%\Pulsar\PluginRepository\index.json`：

```json
[
  {
    "Id": "com.yourname.awesomeplugin",
    "Name": "My Awesome Plugin",
    "Version": "1.0.0",
    "Description": "An awesome plugin",
    "Author": "Your Name",
    "DownloadUrl": "https://github.com/yourname/awesomeplugin/releases/download/v1.0.0/MyAwesomePlugin-1.0.0.zip",
    "Sha256": "...",
    "Tags": ["productivity"],
    "Category": "Productivity",
    "Rating": 5.0,
    "DownloadCount": 0,
    "Dependencies": [],
    "IsInstalled": false
  }
]
```

## 🔧 调试插件

### 方法 1：附加到进程

1. 启动 Pulsar
2. Visual Studio → Debug → Attach to Process
3. 选择 `Pulsar.exe`
4. 在插件代码中设置断点

### 方法 2：日志输出

```csharp
_logger?.LogInformation("Debug info: {Value}", someValue);
```

查看日志：`%AppData%\Pulsar\Logs\pulsar-yyyyMMdd.log`

## 📖 示例插件

查看内置插件源码学习：
- `Pulsar/Plugins/WinSwitcher/` - 窗口切换插件
- `Pulsar/Plugins/BasicCommand/` - 基础命令插件
- `Pulsar/Plugins/Core/Pki/` - PKI 凭证管理插件

## 🆘 常见问题

### Q: 插件没有加载？
A: 检查：
1. `manifest.json` 中的 `Id` 和 `EntryPoint` 是否正确
2. DLL 文件是否在正确的目录
3. 查看日志文件是否有错误信息

### Q: 如何访问 Pulsar 的服务？
A: 在 `Initialize` 方法中通过 `IServiceProvider` 获取

### Q: 插件崩溃会影响 Pulsar 吗？
A: 不会，插件有 Circuit Breaker 保护，崩溃 3 次后会自动禁用

## 📞 获取帮助

- 文档：`Docs/PLUGIN_DEVELOPMENT.md`
- 示例：`Docs/PLUGIN_QUICKSTART.md`
- Issues：GitHub Issues

---

**Happy Coding! 🚀**
