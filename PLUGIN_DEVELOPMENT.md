# Pulsar 插件开发指南

本文档为 Pulsar 插件开发者提供完整的开发规范、最佳实践和 API 参考。

---

## 📋 目录

1. [快速开始](#快速开始)
2. [核心概念](#核心概念)
3. [接口参考](#接口参考)
4. [配置系统](#配置系统)
5. [生命周期管理](#生命周期管理)
6. [错误处理](#错误处理)
7. [最佳实践](#最佳实践)
8. [测试指南](#测试指南)
9. [代码审查清单](#代码审查清单)

---

## 🚀 快速开始

### 最小可运行示例

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Core.Plugin;

namespace MyCompany.Pulsar.Plugins
{
    public class HelloWorldPlugin : IPulsarPlugin, IPluginTiered
    {
        // 必需属性
        public string Id => "com.mycompany.helloworld";
        public string DisplayName => "Hello World";
        public string Version => "1.0.0";
        public string Author => "Your Name";
        public string Description => "A simple example plugin";
        public string Icon => "\uE8F1"; // Segoe Fluent Icons: Emoji
        public bool CanDisable => true;
        public PluginTier Tier => PluginTier.Extension;

        // 初始化
        public void Initialize(IServiceProvider services)
        {
            // 获取依赖服务
            var logger = services.GetService(typeof(ILogger<HelloWorldPlugin>)) as ILogger<HelloWorldPlugin>;
            logger?.LogInformation("[HelloWorldPlugin] Initialized");
        }

        // 执行动作
        public async Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            if (action == "greet")
            {
                var name = args.TryGetValue("name", out var n) ? n : "World";
                return PluginResult.Ok($"Hello, {name}!");
            }
            
            return PluginResult.Error($"Unknown action: {action}");
        }
    }
}
```

### 在 Profiles.json 中使用

```json
{
  "Slots": [
    {
      "Label": "Say Hello",
      "PluginId": "com.mycompany.helloworld",
      "Action": "greet",
      "Args": {
        "name": "Pulsar"
      }
    }
  ]
}
```

---

## 🧩 核心概念

### 插件分层架构

Pulsar 插件分为两个层级：

| 层级 | 说明 | 特性 | 示例 |
|------|------|------|------|
| **Core Plugin** | 核心基础设施插件 | - 不可禁用<br>- 崩溃导致应用退出<br>- 无 Circuit Breaker 保护 | PKI、WinSwitcher |
| **Extension Plugin** | 扩展功能插件 | - 可禁用<br>- 崩溃隔离<br>- Circuit Breaker 保护 | VbaRunner、BookmarkletRunner |

**选择指南**:
- 如果插件是应用核心功能的一部分，选择 **Core**
- 如果插件是可选的增强功能，选择 **Extension**

### Circuit Breaker 熔断机制

Extension 插件受 Circuit Breaker 保护：

- **触发条件**: 1 分钟内崩溃 3 次
- **熔断时长**: 60 秒
- **恢复策略**: Half-Open 状态，允许单次重试

**状态转换**:
```
Closed (正常) → Open (熔断) → Half-Open (试探) → Closed (恢复)
     ↑                                              ↓
     └──────────────── 成功执行 ────────────────────┘
```

### PulsarContext 上下文

`PulsarContext` 在径向菜单调用时捕获，提供运行时环境信息：

```csharp
public class PulsarContext
{
    // 轻量级属性 (同步获取)
    public IntPtr TargetWindowHandle { get; }
    public string TargetProcessName { get; }  // 大写，如 "EXCEL"
    public int TargetProcessId { get; }
    public string TargetExePath { get; }
    
    // 重量级属性 (懒加载，按需异步获取)
    public Task<IReadOnlyList<ProcessWindowInfo>> GetTargetProcessWindowsAsync();
    public Task<string?> GetClipboardTextAsync();
    public Task<string?> GetSelectedTextAsync();
}
```

**性能优化建议**:
- 优先使用轻量级属性
- 仅在必要时调用懒加载方法
- 避免在循环中重复调用懒加载方法

---

## 📚 接口参考

### IPluginMetadataProvider 与 Slot 参数元数据

如果插件需要在 Slots 页面中提供可配置动作，推荐实现 `IPluginMetadataProvider` 并在 `GetMetadata()` 中返回 `PluginMetadata`、`SlotActionMetadata` 与 `SlotParameterMetadata`。

对内建插件和新插件，动作命名应遵循以下模型：

- `DisplayName` 使用用户意图驱动的规范名称，并与文档标题保持一致。
- `Actions` 只暴露规范的主要动作，供新的 slot 编辑流程展示。
- 兼容旧配置时，把旧动作保留为运行时别名，不要把别名也暴露成新的首选动作。
- 参数别名可以通过 `SlotActionMetadata.ParameterAliases` 与 `SlotParameterMetadata.Aliases` 保持兼容。

当前内建插件的规范示例：

- `com.pulsar.command` -> `Command Runner` -> primary actions: `run`, `sendkeys`
- `com.pulsar.winswitcher` -> `App Switcher` -> primary actions: `activate`, `switch`, `launch`
- `com.pulsar.pki` -> `Secret Fill` -> primary action: `fill`, legacy alias: `inject`
- `com.pulsar.system` -> `Pulsar Control` -> primary actions: `open-settings`, `quick-add-profile`, legacy namespaced aliases supported only for compatibility

对于每个会出现在 slot 编辑器中的参数，现在除了 `Key`、`Type`、`Label`、`IsRequired`、`Group`、`Validators` 这些基础字段外，还应补充分层编辑所需的展示提示：

```csharp
new SlotParameterMetadata
{
    Key = "path",
    Type = "string",
    Label = "Executable Path",
    IsRequired = true,
    Group = SlotParameterGroup.Required,
    SummaryLabel = "App",
    SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
    ConfiguredSummaryText = "path ready",
    MissingSummaryText = "path missing",
    PresentationHint = SlotParameterPresentationHint.QuickEdit,
    QuickEditPriority = 100,
    PickerIntent = SlotPickerIntent.Process,
    Validators = new List<ValidationRule> { new RequiredValidator() }
}
```

分层编辑字段约定：

- `SummaryLabel`: 列表摘要里显示的短标签，尽量比表单标签更短。
- `SummaryMode`: 决定摘要是显示原始值(`RawValue`)还是只显示安全状态(`SafeStateOnly`)；敏感值、长路径、脚本内容建议使用安全状态。
- `ConfiguredSummaryText` / `MissingSummaryText`: 为摘要提供稳定的已配置/未配置文案，例如 `selected`、`missing`、`args set`。
- `PresentationHint`: 使用 `QuickEdit` 将字段优先放入卡片内联编辑；使用 `DialogOnly` 将字段限制到完整配置对话框；`Auto` 交给通用回退规则。
- `QuickEditPriority`: 当有多个 quick-edit 候选字段时，用于稳定排序，值越大越靠前。

建议规则：

- 高频、低风险、单步可编辑字段放进 `QuickEdit`，例如标签、主目标路径、简单布尔开关。
- 需要长说明、依赖关系、复杂选择器或高级行为的字段使用 `DialogOnly`。
- 密钥、令牌、长脚本、冗长文件路径不要直接暴露原始摘要；优先使用 `SafeStateOnly`。

回退行为：

- 如果未提供 `PresentationHint`，设置页会优先选择非 `Advanced` 且非复杂字段作为 quick edit，并将其余字段保留在完整配置对话框。
- 如果未提供 `SummaryLabel`，UI 会回退到 `Label`。
- 如果未提供摘要文本，UI 会回退到通用状态文本，例如 `configured`、`missing`、`on`、`off`。
- 如果第三方插件元数据不完整，插件仍然可配置，只是摘要会更保守，更多字段会落入完整配置对话框。

### IPulsarPlugin (必须实现)

所有插件必须实现此接口：

```csharp
public interface IPulsarPlugin
{
    // 元数据
    string Id { get; }                    // 唯一标识符 (反向域名格式)
    string DisplayName { get; }           // 显示名称
    string Version { get; }               // 语义化版本 (如 "1.0.0")
    string Author { get; }                // 作者/维护者
    string Description { get; }           // 简短描述
    string Icon { get; }                  // Segoe Fluent Icons 或 Emoji
    bool CanDisable { get; }              // 是否允许禁用
    
    // 生命周期
    void Initialize(IServiceProvider services);
    
    // 执行
    Task<PluginResult> ExecuteAsync(
        string action,
        IReadOnlyDictionary<string, string> args,
        PulsarContext context
    );
}
```

**命名规范**:
- `Id`: 使用反向域名格式，如 `com.pulsar.winswitcher`
- `Version`: 遵循语义化版本 (Major.Minor.Patch)
- `Icon`: 使用 Unicode 字符 (如 `\uE8B8`) 或 Emoji (如 `🚀`)

### IPluginTiered (推荐实现)

声明插件层级：

```csharp
public interface IPluginTiered
{
    PluginTier Tier { get; }
}

public enum PluginTier
{
    Core,       // 核心插件
    Extension   // 扩展插件
}
```

**实现示例**:
```csharp
public class MyPlugin : IPulsarPlugin, IPluginTiered
{
    public PluginTier Tier => PluginTier.Extension;
    // ...
}
```

### IPluginConfigurable (可选)

为插件添加用户可配置的设置：

```csharp
public interface IPluginConfigurable : IPulsarPlugin
{
    /// <summary>
    /// 返回配置项定义 (用于 UI 渲染)
    /// </summary>
    IEnumerable<PluginSettingDefinition> GetSettingsDefinition();
    
    /// <summary>
    /// 应用配置变更
    /// </summary>
    void UpdateSettings(Dictionary<string, object> settings);
    
    /// <summary>
    /// 验证配置是否合法
    /// </summary>
    PluginConfigValidationResult ValidateSettings(Dictionary<string, object> settings);
}
```

**实现示例**:
```csharp
public class MyPlugin : IPulsarPlugin, IPluginConfigurable
{
    private bool _enableFeatureX = true;
    
    public IEnumerable<PluginSettingDefinition> GetSettingsDefinition()
    {
        yield return PluginSettingDefinition.Create(
            key: "EnableFeatureX",
            label: "Enable Feature X",
            type: PluginSettingType.Boolean,
            defaultValue: true,
            description: "Enable or disable feature X"
        );
    }
    
    public void UpdateSettings(Dictionary<string, object> settings)
    {
        if (settings.TryGetValue("EnableFeatureX", out var value))
        {
            _enableFeatureX = value is bool b ? b : bool.Parse(value.ToString()!);
        }
    }
    
    public PluginConfigValidationResult ValidateSettings(Dictionary<string, object> settings)
    {
        var result = new PluginConfigValidationResult { IsValid = true };
        
        // 验证逻辑
        if (settings.TryGetValue("EnableFeatureX", out var value))
        {
            if (value is not bool && !bool.TryParse(value?.ToString(), out _))
            {
                result.IsValid = false;
                result.Errors.Add("EnableFeatureX must be a boolean value");
            }
        }
        
        return result;
    }
}
```

### IPluginLifecycle (可选)

管理插件生命周期事件：

```csharp
public interface IPluginLifecycle
{
    /// <summary>
    /// 插件被启用时调用 (首次加载或用户手动启用)
    /// </summary>
    Task OnEnableAsync();
    
    /// <summary>
    /// 插件被禁用时调用 (用户手动禁用或 Circuit Breaker 触发)
    /// </summary>
    Task OnDisableAsync();
    
    /// <summary>
    /// 应用退出前调用，用于清理资源
    /// </summary>
    Task OnUnloadAsync();
}
```

**使用场景**:
- `OnEnableAsync`: 注册全局热键、启动后台服务
- `OnDisableAsync`: 取消注册热键、停止后台服务
- `OnUnloadAsync`: 释放非托管资源、保存状态

---

## ⚙️ 配置系统

### 支持的配置类型

```csharp
public enum PluginSettingType
{
    Boolean,    // Toggle Switch
    String,     // TextBox
    Path,       // File/Folder Picker
    Integer,    // Numeric Up/Down
    Selection,  // ComboBox (requires Options)
    Secret      // PasswordBox (masked)
}
```

### 配置定义示例

```csharp
public IEnumerable<PluginSettingDefinition> GetSettingsDefinition()
{
    // Boolean 开关
    yield return PluginSettingDefinition.Create(
        key: "AutoSave",
        label: "Auto Save",
        type: PluginSettingType.Boolean,
        defaultValue: true,
        description: "Automatically save changes"
    );
    
    // String 文本
    yield return PluginSettingDefinition.Create(
        key: "ApiEndpoint",
        label: "API Endpoint",
        type: PluginSettingType.String,
        defaultValue: "https://api.example.com",
        description: "API server URL"
    );
    
    // Integer 数字
    yield return PluginSettingDefinition.Create(
        key: "Timeout",
        label: "Timeout (seconds)",
        type: PluginSettingType.Integer,
        defaultValue: 30,
        description: "Request timeout in seconds"
    );
    
    // Selection 下拉选择
    yield return new PluginSettingDefinition
    {
        Key = "LogLevel",
        Label = "Log Level",
        Type = PluginSettingType.Selection,
        DefaultValue = "Info",
        Description = "Logging verbosity",
        Options = new List<string> { "Debug", "Info", "Warning", "Error" }
    };
    
    // Secret 密码
    yield return PluginSettingDefinition.Create(
        key: "ApiKey",
        label: "API Key",
        type: PluginSettingType.Secret,
        defaultValue: "",
        description: "Your API key (stored encrypted)"
    );
}
```

### 配置验证

```csharp
public PluginConfigValidationResult ValidateSettings(Dictionary<string, object> settings)
{
    var result = new PluginConfigValidationResult { IsValid = true };
    
    // 验证 Timeout 范围
    if (settings.TryGetValue("Timeout", out var timeoutObj))
    {
        if (timeoutObj is int timeout)
        {
            if (timeout < 1 || timeout > 300)
            {
                result.IsValid = false;
                result.Errors.Add("Timeout must be between 1 and 300 seconds");
            }
        }
    }
    
    // 验证 ApiEndpoint 格式
    if (settings.TryGetValue("ApiEndpoint", out var endpointObj))
    {
        var endpoint = endpointObj?.ToString();
        if (!string.IsNullOrEmpty(endpoint) && !Uri.TryCreate(endpoint, UriKind.Absolute, out _))
        {
            result.IsValid = false;
            result.Errors.Add("ApiEndpoint must be a valid URL");
        }
    }
    
    return result;
}
```

---

## 🔄 生命周期管理

### 生命周期流程图

```
[App 启动]
    ↓
[PluginLoader.LoadAll()] → 加载所有插件 DLL
    ↓
[plugin.Initialize(services)] → 注入依赖服务
    ↓
[plugin.OnEnableAsync()] → 启用插件 (如果实现 IPluginLifecycle)
    ↓
[运行时]
    ├─ [用户禁用] → plugin.OnDisableAsync()
    ├─ [用户启用] → plugin.OnEnableAsync()
    └─ [Circuit Breaker 触发] → plugin.OnDisableAsync()
    ↓
[App 退出]
    ↓
[plugin.OnUnloadAsync()] → 清理资源
```

### 实现示例

```csharp
public class MyPlugin : IPulsarPlugin, IPluginLifecycle
{
    private Timer? _backgroundTimer;
    
    public void Initialize(IServiceProvider services)
    {
        // 仅初始化依赖服务，不启动后台任务
    }
    
    public async Task OnEnableAsync()
    {
        // 启动后台任务
        _backgroundTimer = new Timer(OnTimerTick, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
        await Task.CompletedTask;
    }
    
    public async Task OnDisableAsync()
    {
        // 停止后台任务
        _backgroundTimer?.Dispose();
        _backgroundTimer = null;
        await Task.CompletedTask;
    }
    
    public async Task OnUnloadAsync()
    {
        // 清理资源
        await OnDisableAsync();
    }
    
    private void OnTimerTick(object? state)
    {
        // 后台任务逻辑
    }
}
```

---

## ⚠️ 错误处理

### PluginResult 返回值

```csharp
public class PluginResult
{
    public bool Success { get; }
    public string? Message { get; }
    public PluginErrorSeverity Severity { get; }
    
    // 成功
    public static PluginResult Ok(string? message = null);
    
    // 失败
    public static PluginResult Error(string message, PluginErrorSeverity severity = PluginErrorSeverity.Recoverable);
}
```

### 错误严重程度

```csharp
public enum PluginErrorSeverity
{
    /// <summary>
    /// 可恢复错误 (如用户输入错误)，不计入熔断
    /// </summary>
    Recoverable,
    
    /// <summary>
    /// 严重错误 (如配置错误、依赖缺失)，计入熔断
    /// </summary>
    Critical
}
```

### 错误处理策略

| 场景 | 处理方式 | 示例 |
|------|---------|------|
| 用户输入错误 | 返回 `Error(Recoverable)` | 参数缺失、格式错误 |
| 外部服务不可用 | 返回 `Error(Recoverable)` | 网络超时、API 限流 |
| 配置错误 | 返回 `Error(Critical)` | 必需配置缺失 |
| 依赖服务缺失 | 抛出异常 (在 `Initialize` 中) | `IWindowService` 未注册 |
| 未预期的异常 | 让异常传播 (由 PluginRegistry 捕获) | NullReferenceException |

### 实现示例

```csharp
public async Task<PluginResult> ExecuteAsync(
    string action,
    IReadOnlyDictionary<string, string> args,
    PulsarContext context)
{
    // 1. 验证参数 (Recoverable)
    if (!args.TryGetValue("url", out var url) || string.IsNullOrEmpty(url))
    {
        return PluginResult.Error("Missing required parameter: url", PluginErrorSeverity.Recoverable);
    }
    
    // 2. 验证配置 (Critical)
    if (string.IsNullOrEmpty(_apiKey))
    {
        return PluginResult.Error("API key not configured", PluginErrorSeverity.Critical);
    }
    
    try
    {
        // 3. 执行业务逻辑
        var result = await CallExternalApiAsync(url);
        return PluginResult.Ok($"Success: {result}");
    }
    catch (HttpRequestException ex)
    {
        // 4. 外部服务错误 (Recoverable)
        _logger?.LogWarning(ex, "API request failed");
        return PluginResult.Error($"API request failed: {ex.Message}", PluginErrorSeverity.Recoverable);
    }
    // 5. 未预期的异常让其传播，由 PluginRegistry 处理
}
```

---

## ✅ 最佳实践

### 1. 性能优化

**避免阻塞 UI 线程**:
```csharp
// ❌ 错误: 同步阻塞
public async Task<PluginResult> ExecuteAsync(...)
{
    Thread.Sleep(5000); // 阻塞 UI 线程
    return PluginResult.Ok();
}

// ✅ 正确: 异步执行
public async Task<PluginResult> ExecuteAsync(...)
{
    await Task.Delay(5000); // 不阻塞 UI 线程
    return PluginResult.Ok();
}
```

**使用 PulsarContext 懒加载**:
```csharp
// ❌ 错误: 总是加载重量级数据
public async Task<PluginResult> ExecuteAsync(..., PulsarContext context)
{
    var windows = await context.GetTargetProcessWindowsAsync(); // 即使不需要也加载
    // ...
}

// ✅ 正确: 按需加载
public async Task<PluginResult> ExecuteAsync(..., PulsarContext context)
{
    if (needWindowList)
    {
        var windows = await context.GetTargetProcessWindowsAsync();
    }
    // ...
}
```

### 2. 安全性

**不在日志中输出敏感信息**:
```csharp
// ❌ 错误: 泄露密码
_logger?.LogInformation("Login with password: {Password}", password);

// ✅ 正确: 脱敏
_logger?.LogInformation("Login attempt for user: {User}", username);
```

**验证所有外部输入**:
```csharp
// ❌ 错误: 直接使用用户输入
var filePath = args["path"];
File.Delete(filePath); // 危险!

// ✅ 正确: 验证路径
var filePath = args["path"];
if (Path.IsPathRooted(filePath) && File.Exists(filePath))
{
    File.Delete(filePath);
}
```

### 3. 日志记录

**使用 ILogger 而非 Debug.WriteLine**:
```csharp
// ❌ 错误
Debug.WriteLine($"[MyPlugin] Action executed");

// ✅ 正确
_logger?.LogInformation("[MyPlugin] Action executed");
```

**使用结构化日志**:
```csharp
// ❌ 错误: 字符串拼接
_logger?.LogInformation($"User {userId} executed action {action}");

// ✅ 正确: 结构化参数
_logger?.LogInformation("User {UserId} executed action {Action}", userId, action);
```

### 4. 资源管理

**正确释放资源**:
```csharp
public class MyPlugin : IPulsarPlugin, IPluginLifecycle
{
    private HttpClient? _httpClient;
    
    public async Task OnEnableAsync()
    {
        _httpClient = new HttpClient();
    }
    
    public async Task OnUnloadAsync()
    {
        _httpClient?.Dispose();
        _httpClient = null;
    }
}
```

---

## 🧪 测试指南

### 单元测试示例

```csharp
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;

public class MyPluginTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidArgs_ReturnsSuccess()
    {
        // Arrange
        var plugin = new MyPlugin();
        var mockLogger = new Mock<ILogger<MyPlugin>>();
        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(sp => sp.GetService(typeof(ILogger<MyPlugin>)))
            .Returns(mockLogger.Object);
        
        plugin.Initialize(mockServiceProvider.Object);
        
        var args = new Dictionary<string, string> { { "name", "Test" } };
        var context = CreateMockContext();
        
        // Act
        var result = await plugin.ExecuteAsync("greet", args, context);
        
        // Assert
        Assert.True(result.Success);
        Assert.Contains("Test", result.Message);
    }
    
    [Fact]
    public async Task ExecuteAsync_WithMissingArgs_ReturnsError()
    {
        // Arrange
        var plugin = new MyPlugin();
        plugin.Initialize(Mock.Of<IServiceProvider>());
        
        var args = new Dictionary<string, string>(); // 缺少 "name"
        var context = CreateMockContext();
        
        // Act
        var result = await plugin.ExecuteAsync("greet", args, context);
        
        // Assert
        Assert.False(result.Success);
        Assert.Equal(PluginErrorSeverity.Recoverable, result.Severity);
    }
    
    private PulsarContext CreateMockContext()
    {
        // 创建模拟上下文
        // ...
    }
}
```

---

## 📋 代码审查清单

在提交插件代码前，请确保通过以下检查：

### 基础要求
- [ ] 实现 `IPulsarPlugin` 所有必需属性
- [ ] `Id` 使用反向域名格式 (如 `com.pulsar.xxx`)
- [ ] `Version` 遵循语义化版本 (如 `1.0.0`)
- [ ] `Icon` 使用 Segoe Fluent Icons 或 Emoji
- [ ] 实现 `IPluginTiered` 并正确声明层级

### 功能要求
- [ ] `Initialize` 中验证所有依赖服务可用
- [ ] `ExecuteAsync` 中处理所有可能的异常
- [ ] 返回有意义的 `PluginResult` 消息
- [ ] 正确设置 `PluginErrorSeverity`

### 配置要求 (如果实现 IPluginConfigurable)
- [ ] `GetSettingsDefinition` 返回完整的配置定义
- [ ] `UpdateSettings` 中验证配置类型和范围
- [ ] 实现 `ValidateSettings` 并返回详细错误信息
- [ ] 配置变更后立即生效 (无需重启)

### 生命周期要求 (如果实现 IPluginLifecycle)
- [ ] `OnEnableAsync` 中启动后台任务/注册资源
- [ ] `OnDisableAsync` 中停止后台任务/取消注册
- [ ] `OnUnloadAsync` 中释放所有资源
- [ ] 生命周期方法中处理异常

### 性能要求
- [ ] 避免在 `Initialize` 中执行耗时操作
- [ ] 使用 `PulsarContext` 的懒加载特性
- [ ] 长时间操作使用 `async/await`
- [ ] 避免在循环中重复调用懒加载方法

### 安全要求
- [ ] 不在日志中输出敏感信息 (密码、Token)
- [ ] 验证所有外部输入 (args 参数)
- [ ] 使用 `ILogger` 而非 `Debug.WriteLine`
- [ ] 正确处理文件路径 (防止路径遍历攻击)

### 文档要求
- [ ] `Description` 清晰描述插件功能
- [ ] 代码中添加 XML 注释
- [ ] 提供使用示例 (在 Profiles.json 中)
- [ ] 更新 CHANGELOG (如果有)

---

## 📦 插件能力矩阵

| 插件类型 | 必须实现 | 推荐实现 | 可选实现 | 说明 |
|---------|---------|---------|---------|------|
| **Core Plugin** | `IPulsarPlugin`<br>`IPluginTiered` | - | `IPluginConfigurable`<br>`IPluginLifecycle` | 核心插件，不可禁用 |
| **Extension Plugin** | `IPulsarPlugin`<br>`IPluginTiered` | - | `IPluginConfigurable`<br>`IPluginLifecycle` | 扩展插件，可禁用 |
| **Configurable Plugin** | + `IPluginConfigurable` | - | - | 需要用户配置的插件 |
| **Lifecycle-Aware Plugin** | + `IPluginLifecycle` | - | - | 需要管理资源的插件 |

---

## 🔗 相关文档

- [AGENTS.md](./AGENTS.md) - AI Agent 操作指南
- [ARCHITECTURE.md](./ARCHITECTURE.md) - 系统架构文档
- [插件设置页重构指南.md](./插件设置页重构指南.md) - UI 重构文档

---

**版本**: 1.0.0  
**最后更新**: 2026-03-01  
**维护者**: Pulsar Team
