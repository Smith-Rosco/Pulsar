# Pulsar Plugin Template (Enhanced)

这是一个 **增强版** Pulsar 插件开发模板，展示了完整的插件开发最佳实践。

## 📁 文件结构

```
PluginTemplate/
├── ExamplePlugin.csproj      # 项目文件（支持 XAML）
├── ExamplePlugin.cs           # 插件主代码（新版 API）
├── ExampleHelper.cs           # 辅助类（多文件示例）
├── ExampleDialog.xaml         # XAML 对话框
├── ExampleDialog.xaml.cs      # 对话框代码隐藏
├── manifest.json              # 插件元数据
└── README.md                  # 本文档
```

## ✨ 新特性（v2.0）

相比旧版模板，增强版包含：

- ✅ **新版 API**：使用 `IReadOnlyDictionary<string, string>` 参数（替代 `string[]`）
- ✅ **多文件结构**：展示如何组织复杂插件（Helper 类）
- ✅ **XAML UI 支持**：包含完整的 WPF 对话框示例
- ✅ **依赖注入**：演示如何使用 `ILogger` 等 Pulsar 服务
- ✅ **插件分层**：实现 `IPluginTiered` 接口（Extension 插件）
- ✅ **错误处理**：使用结构化日志和 `PluginResult.Error()`

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
- `ExampleHelper.cs` → `MyAwesomeHelper.cs`
- `ExampleDialog.xaml` → `MyAwesomeDialog.xaml`
- `ExampleDialog.xaml.cs` → `MyAwesomeDialog.xaml.cs`

### 3. 修改代码

**MyAwesomePlugin.cs**:
```csharp
namespace MyAwesomePlugin  // 修改命名空间
{
    public class MyAwesomePlugin : IPulsarPlugin, IPluginTiered
    {
        public string Id => "com.yourname.awesomeplugin";  // 修改 ID
        public string DisplayName => "My Awesome Plugin";   // 修改名称
        public PluginTier Tier => PluginTier.Extension;
        // ... 其他属性
    }
}
```

**MyAwesomeDialog.xaml**:
```xml
<Window x:Class="MyAwesomePlugin.MyAwesomeDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="My Awesome Dialog">
    <!-- 修改类名 -->
</Window>
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

### IPulsarPlugin 接口（新版）

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
    
    // 新版 API：使用 IReadOnlyDictionary 参数
    Task<PluginResult> ExecuteAsync(
        string action, 
        IReadOnlyDictionary<string, string> args, 
        PulsarContext context);
}
```

### IPluginTiered 接口

Extension 插件应实现此接口：

```csharp
public interface IPluginTiered
{
    bool CanDisable { get; }        // 是否可以禁用
    PluginTier Tier { get; }        // 插件层级（Core/Extension）
}
```

### 新版 API：参数传递

**旧版（已弃用）**:
```csharp
public async Task<PluginResult> ExecuteAsync(string action, string[] args, PulsarContext context)
{
    var name = args.Length > 0 ? args[0] : "World";  // 位置参数
}
```

**新版（推荐）**:
```csharp
public async Task<PluginResult> ExecuteAsync(
    string action, 
    IReadOnlyDictionary<string, string> args, 
    PulsarContext context)
{
    // 键值对参数，更清晰
    var name = args.TryGetValue("name", out var n) ? n : "World";
    
    // 检查必需参数
    if (!args.TryGetValue("path", out var path))
    {
        return PluginResult.Error("Missing required parameter: path");
    }
}
```

### PulsarContext 上下文

`PulsarContext` 提供了丰富的上下文信息：

```csharp
public async Task<PluginResult> ExecuteAsync(
    string action, 
    IReadOnlyDictionary<string, string> args, 
    PulsarContext context)
{
    // 获取当前活动窗口
    var window = context.ActiveWindow;
    var title = window?.Title;
    var processName = window?.ProcessName;

    // 获取剪贴板内容（Lazy 加载）
    var clipboardText = await context.GetClipboardTextAsync();

    // 设置剪贴板
    await context.SetClipboardTextAsync("Hello World");

    // 显示通知
    return PluginResult.Ok("操作成功！");
}
```

### 插件动作（Actions）

插件可以定义多个动作：

```csharp
public async Task<PluginResult> ExecuteAsync(
    string action, 
    IReadOnlyDictionary<string, string> args, 
    PulsarContext context)
{
    return action.ToLowerInvariant() switch
    {
        "hello" => await SayHelloAsync(args, context),
        "copy" => await CopyTextAsync(args, context),
        "search" => await SearchAsync(args, context),
        _ => PluginResult.Error($"Unknown action: {action}")
    };
}
```

### 返回结果（新版）

使用 `PluginResult` 返回执行结果：

```csharp
// 成功（会显示通知）
return PluginResult.Ok("操作成功！");

// 失败（会显示错误通知）
return PluginResult.Error("操作失败：文件不存在");

// 静默成功（不显示通知）
return PluginResult.Silent();
```

**注意**：旧版的 `PluginResult.Success()` 和 `PluginResult.Failed()` 已弃用，请使用 `Ok()` 和 `Error()`。

### 依赖注入

在 `Initialize` 方法中获取服务：

```csharp
private ILogger<MyPlugin>? _logger;

public void Initialize(IServiceProvider serviceProvider)
{
    // 获取日志服务
    _logger = serviceProvider.GetService(typeof(ILogger<MyPlugin>)) as ILogger<MyPlugin>;
    _logger?.LogInformation("[{PluginName}] Plugin initialized", DisplayName);
    
    // 获取其他服务
    var windowService = serviceProvider.GetService(typeof(IWindowService)) as IWindowService;
}
```

### 多文件项目结构

对于复杂插件，建议使用多文件结构：

```
MyPlugin/
├── MyPlugin.cs              # 主插件类
├── Services/
│   ├── DataService.cs       # 数据服务
│   └── ApiClient.cs         # API 客户端
├── Helpers/
│   ├── StringHelper.cs      # 字符串工具
│   └── FileHelper.cs        # 文件工具
├── Views/
│   ├── SettingsDialog.xaml  # 设置对话框
│   └── ResultWindow.xaml    # 结果窗口
└── Models/
    └── PluginConfig.cs      # 配置模型
```

**示例（ExampleHelper.cs）**:
```csharp
namespace MyPlugin
{
    public class MyHelper
    {
        private readonly ILogger? _logger;

        public MyHelper(ILogger? logger)
        {
            _logger = logger;
        }

        public string ProcessData(string input)
        {
            _logger?.LogDebug("Processing: {Input}", input);
            return input.ToUpperInvariant();
        }
    }
}
```

### XAML UI 组件

插件可以包含 WPF 对话框：

**ExampleDialog.xaml**:
```xml
<Window x:Class="MyPlugin.MyDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="My Dialog"
        Width="400"
        Height="200"
        WindowStartupLocation="CenterOwner">
    <Grid Margin="20">
        <TextBox x:Name="InputTextBox"/>
        <Button Content="OK" Click="OkButton_Click"/>
    </Grid>
</Window>
```

**ExampleDialog.xaml.cs**:
```csharp
namespace MyPlugin
{
    public partial class MyDialog : Window
    {
        public string UserInput { get; private set; } = string.Empty;

        public MyDialog()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            UserInput = InputTextBox.Text;
            DialogResult = true;
            Close();
        }
    }
}
```

**在插件中使用**:
```csharp
private async Task<PluginResult> ShowDialogAsync(PulsarContext context)
{
    string? result = null;

    // 必须在 UI 线程上显示对话框
    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
    {
        var dialog = new MyDialog
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            result = dialog.UserInput;
        }
    });

    return result != null 
        ? PluginResult.Ok($"User input: {result}") 
        : PluginResult.Ok("Dialog cancelled");
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
_logger?.LogWarning("Warning: {Message}", warningMessage);
_logger?.LogError(ex, "Error occurred");
```

查看日志：`%AppData%\Pulsar\Logs\pulsar-yyyyMMdd.log`

## 📖 示例插件

查看内置插件源码学习：
- `Pulsar/Plugins/Extensions/BasicCommand/` - 基础命令插件（简单）
- `Pulsar/Plugins/Extensions/VbaRunner/` - VBA 运行器（复杂，含 XAML）
- `Pulsar/Plugins/Core/Pki/` - PKI 凭证管理插件（Core 插件）

## 🆘 常见问题

### Q: 插件没有加载？
A: 检查：
1. `manifest.json` 中的 `Id` 和 `EntryPoint` 是否正确
2. DLL 文件是否在正确的目录
3. 查看日志文件是否有错误信息
4. 确保使用了新版 API（`IReadOnlyDictionary` 参数）

### Q: 如何访问 Pulsar 的服务？
A: 在 `Initialize` 方法中通过 `IServiceProvider` 获取

### Q: 插件崩溃会影响 Pulsar 吗？
A: 不会，Extension 插件有 Circuit Breaker 保护，崩溃 3 次后会自动禁用

### Q: 旧版 API 还能用吗？
A: 可以，但建议迁移到新版 API（`IReadOnlyDictionary`），旧版可能在未来版本中移除

### Q: 如何在插件中显示 UI？
A: 使用 `Dispatcher.InvokeAsync()` 在 UI 线程上显示 WPF 窗口（参见 XAML UI 组件章节）

## 🔄 从旧版模板迁移

如果你有使用旧版模板的插件，迁移步骤：

1. **更新 ExecuteAsync 签名**:
```csharp
// 旧版
public async Task<PluginResult> ExecuteAsync(string action, string[] args, PulsarContext context)

// 新版
public async Task<PluginResult> ExecuteAsync(
    string action, 
    IReadOnlyDictionary<string, string> args, 
    PulsarContext context)
```

2. **更新参数访问**:
```csharp
// 旧版
var name = args.Length > 0 ? args[0] : "default";

// 新版
var name = args.TryGetValue("name", out var n) ? n : "default";
```

3. **更新返回值**:
```csharp
// 旧版
return PluginResult.Success("OK");
return PluginResult.Failed("Error");

// 新版
return PluginResult.Ok("OK");
return PluginResult.Error("Error");
```

4. **实现 IPluginTiered**（可选）:
```csharp
public class MyPlugin : IPulsarPlugin, IPluginTiered
{
    public bool CanDisable => true;
    public PluginTier Tier => PluginTier.Extension;
}
```

## 📞 获取帮助

- 文档：`Docs/PLUGIN_DEVELOPMENT.md`
- 示例：`Docs/PLUGIN_QUICKSTART.md`
- Issues：GitHub Issues

---

**Happy Coding! 🚀**
