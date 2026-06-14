# Pulsar 测试框架规范

本文档为 AI 和开发者提供 Pulsar 项目的测试编写指南，确保测试的一致性和可维护性。

## 目录

- [测试框架概述](#测试框架概述)
- [项目结构](#项目结构)
- [核心测试工具](#核心测试工具)
- [编写测试的最佳实践](#编写测试的最佳实践)
- [常见测试模式](#常见测试模式)
- [运行测试](#运行测试)

---

## 测试框架概述

### 技术栈

- **测试框架**: xUnit 2.9.2
- **断言库**: FluentAssertions 7.0.0
- **Mock 框架**: Moq 4.20.72
- **目标框架**: .NET 8.0 (net8.0-windows)

### 测试覆盖范围

当前测试套件包含 **331+ 个测试**，覆盖以下模块：

- **配置管理** (`Config/`): 配置加载、保存、验证、默认值
- **插件系统** (`Plugin/`): 插件注册、执行、断路器、热重载
- **插件安全** (`Plugin/Security/`): 权限管理、权限拦截
- **服务层** (`Services/`): 通知、补间动画、配置变更检测
- **ViewModel** (`ViewModels/`): 窗口切换策略、分组槽交互、脏状态检测
- **教程系统** (`Tutorial/`): 引导流程、状态机、触发器
- **扩展插件** (`Plugins/`): 书签运行器、命令插件解析

---

## 项目结构

```
Pulsar.Tests/
├── Config/                             # 配置服务测试
│   ├── ConfigServiceLoadTests.cs      # 配置加载测试
│   ├── ConfigServiceSaveTests.cs      # 配置保存测试
│   └── ProfilesConfigDefaultsTests.cs # 默认配置测试
├── Plugin/                             # 插件系统测试
│   ├── PluginRegistryExecutionTests.cs           # 插件执行测试
│   ├── PluginRegistryCircuitBreakerTests.cs      # 断路器测试
│   ├── PluginExecutionPipelineTimeoutTests.cs    # 超时测试
│   ├── HotReloadTests.cs                         # 热重载测试
│   └── Security/                                 # 插件安全测试
│       ├── PermissionInterceptorTests.cs         # 权限拦截器测试
│       └── PluginPermissionTests.cs              # 权限系统测试
├── Plugins/                            # 扩展插件测试
│   ├── BookmarkletRunnerPluginTests.cs # 书签运行器测试
│   └── Command/                        # 命令插件测试
│       ├── CommandPluginTests.cs       # 命令执行测试
│       └── KeysLexerTests.cs           # 按键解析测试
├── Services/                           # 服务层测试
│   └── (NotificationService, TweenService, ConfigChangeDetection)
├── Tutorial/                           # 教程系统测试
│   ├── TutorialStateMachineTests.cs    # 状态机测试
│   ├── TutorialTriggerTests.cs         # 触发器测试
│   ├── TutorialDataTests.cs            # 数据层测试
│   └── (8+ 更多教程相关测试文件)
├── ViewModels/                         # ViewModel 测试
│   ├── WindowSwitchStrategyTests.cs    # 窗口切换策略测试
│   ├── GroupedSlotInteractionTests.cs  # 分组槽交互测试
│   ├── SettingsViewModelDirtyStateTests.cs # 脏状态测试
│   └── DialogSlotEditorViewModelTests.cs   # 对话框槽编辑测试
├── TestHelpers/                        # 测试辅助工具
│   ├── PulsarContextFactory.cs         # PulsarContext 工厂类
│   └── (其他辅助工具)
└── TESTING_GUIDE.md                    # 本文档
```

---

## 核心测试工具

### 1. PulsarContextFactory

**位置**: `TestHelpers/PulsarContextFactory.cs`

**用途**: 创建用于测试的 `PulsarContext` 实例。

**为什么需要它？**

`PulsarContext` 只有私有构造函数，必须通过 `PulsarContext.Capture(IWindowService, ...)` 静态方法创建。在测试中，我们需要 Mock `IWindowService` 并提供测试数据。

**API**:

```csharp
// 创建默认测试上下文
var context = PulsarContextFactory.CreateTestContext();

// 创建带自定义参数的上下文
var context = PulsarContextFactory.CreateTestContext(
    targetWindowHandle: new IntPtr(12345),
    targetProcessName: "CHROME",
    targetProcessId: 5678
);

// 创建带自定义窗口列表的上下文
var windows = new List<ProcessWindowInfo> { /* ... */ };
var context = PulsarContextFactory.CreateTestContextWithWindows(
    windows,
    targetProcessId: 1234
);
```

**示例**:

```csharp
[Fact]
public async Task ExecuteAsync_ShouldReturnSuccess_WhenPluginSucceeds()
{
    // Arrange
    var registry = new PluginRegistry(_serviceProvider, _mockLogger.Object);
    var plugin = new TestPlugin(shouldSucceed: true);
    
    RegisterPlugin(registry, plugin);
    
    var context = PulsarContextFactory.CreateTestContext(); // ✅ 使用工厂方法
    var args = new Dictionary<string, string>().AsReadOnly();

    // Act
    var result = await registry.ExecuteAsync(plugin.Id, "test", args, context);

    // Assert
    result.Success.Should().BeTrue();
}
```

**❌ 错误示例**:

```csharp
var context = new PulsarContext(); // ❌ 编译错误：构造函数是私有的
```

---

## 编写测试的最佳实践

### 1. 测试命名规范

使用 **方法名_应该做什么_在什么条件下** 的格式：

```csharp
[Fact]
public async Task ExecuteAsync_ShouldReturnError_WhenPluginNotFound()
{
    // ...
}

[Fact]
public async Task LoadAsync_ShouldReturnDefaultConfig_WhenFileNotExists()
{
    // ...
}
```

### 2. AAA 模式 (Arrange-Act-Assert)

所有测试应遵循 AAA 模式：

```csharp
[Fact]
public async Task Example_Test()
{
    // Arrange - 准备测试数据和依赖
    var service = new MyService();
    var input = "test";
    
    // Act - 执行被测试的方法
    var result = await service.DoSomethingAsync(input);
    
    // Assert - 验证结果
    result.Should().Be("expected");
}
```

### 3. 使用 FluentAssertions

优先使用 FluentAssertions 进行断言，提高可读性：

```csharp
// ✅ 推荐
result.Success.Should().BeTrue();
result.Message.Should().Contain("success");
config.Plugins.Should().ContainKey("test.plugin");

// ❌ 不推荐
Assert.True(result.Success);
Assert.Contains("success", result.Message);
```

### 4. Mock 依赖服务

使用 Moq 创建 Mock 对象：

```csharp
private readonly Mock<ILogger<MyService>> _mockLogger;
private readonly Mock<IConfigService> _mockConfigService;

public MyServiceTests()
{
    _mockLogger = new Mock<ILogger<MyService>>();
    _mockConfigService = new Mock<IConfigService>();
    
    // 配置 Mock 行为
    _mockConfigService.Setup(x => x.Current).Returns(new ProfilesConfig());
}
```

### 5. 使用 IServiceProvider 进行依赖注入

```csharp
private readonly IServiceProvider _serviceProvider;

public MyTests()
{
    var services = new ServiceCollection();
    
    services.AddSingleton(_mockLogger.Object);
    services.AddSingleton(_mockConfigService.Object);
    
    _serviceProvider = services.BuildServiceProvider();
}
```

---

## 常见测试模式

### 模式 1: 测试插件执行

```csharp
[Fact]
public async Task ExecuteAsync_ShouldReturnSuccess_WhenPluginSucceeds()
{
    // Arrange
    var registry = new PluginRegistry(_serviceProvider, _mockLogger.Object);
    var plugin = new TestPlugin(shouldSucceed: true);
    
    // 使用反射注册插件（绕过 LoadAllAsync）
    typeof(PluginRegistry)
        .GetField("_plugins", BindingFlags.NonPublic | BindingFlags.Instance)
        ?.SetValue(registry, new Dictionary<string, IPulsarPlugin> { [plugin.Id] = plugin });
    
    var context = PulsarContextFactory.CreateTestContext();
    var args = new Dictionary<string, string>().AsReadOnly();

    // Act
    var result = await registry.ExecuteAsync(plugin.Id, "test", args, context);

    // Assert
    result.Success.Should().BeTrue();
    result.Message.Should().Be("Test success");
}
```

### 模式 2: 测试断路器 (Circuit Breaker)

```csharp
[Fact]
public async Task CircuitBreaker_ShouldTrip_AfterThreeConsecutiveFailures()
{
    // Arrange
    var registry = new PluginRegistry(_serviceProvider, _mockLogger.Object);
    var plugin = new FaultyTestPlugin(shouldThrow: true);
    
    RegisterPlugin(registry, plugin);
    
    var context = PulsarContextFactory.CreateTestContext();
    var args = new Dictionary<string, string>().AsReadOnly();

    // Act - 执行 3 次触发断路器
    var result1 = await registry.ExecuteAsync(plugin.Id, "test", args, context);
    var result2 = await registry.ExecuteAsync(plugin.Id, "test", args, context);
    var result3 = await registry.ExecuteAsync(plugin.Id, "test", args, context);
    var result4 = await registry.ExecuteAsync(plugin.Id, "test", args, context); // 断路器已打开

    // Assert
    result1.Success.Should().BeFalse("first failure should return error");
    result2.Success.Should().BeFalse("second failure should return error");
    result3.Success.Should().BeFalse("third failure should return error");
    result4.Message.Should().Contain("disabled for safety", "circuit breaker should be open");
}
```

**重要**: 断路器在第 3 次失败时触发，但**第 4 次调用**才会返回 "disabled for safety" 消息。

### 模式 3: 测试配置加载

```csharp
[Fact]
public async Task LoadAsync_ShouldLoadValidConfig_WhenFileExists()
{
    // Arrange
    var json = @"{
        ""plugins"": {
            ""test.plugin"": {
                ""enabled"": true,
                ""config"": {
                    ""key"": ""value""
                }
            }
        }
    }";
    
    await File.WriteAllTextAsync(_configPath, json);
    var service = CreateConfigService();

    // Act
    var config = await service.LoadAsync();

    // Assert
    config.Should().NotBeNull();
    config.Plugins.Should().ContainKey("test.plugin");
    config.Plugins["test.plugin"].Enabled.Should().BeTrue();
}
```

### 模式 4: 测试权限系统

```csharp
[Fact]
public void CheckPermission_ShouldThrowException_WhenPermissionDenied()
{
    // Arrange
    var interceptor = new PermissionInterceptor();
    var pluginId = "test.plugin";
    var permission = PluginPermission.ReadClipboard;
    
    // 不授予任何权限

    // Act & Assert
    var act = () => interceptor.CheckPermission(pluginId, permission, "TestOperation");
    
    act.Should().Throw<UnauthorizedAccessException>()
        .WithMessage("*does not have permission*");
}
```

---

## 运行测试

### 运行所有测试

```bash
dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj --verbosity quiet
```

### 运行特定测试类

```bash
dotnet test --filter "FullyQualifiedName~PluginRegistryExecutionTests"
```

### 运行特定测试方法

```bash
dotnet test --filter "FullyQualifiedName~ExecuteAsync_ShouldReturnSuccess_WhenPluginSucceeds"
```

### 运行特定命名空间的测试

```bash
dotnet test --filter "FullyQualifiedName~Pulsar.Tests.Config"
```

### 输出级别

- `--verbosity quiet`: 只显示摘要（推荐日常使用）
- `--verbosity minimal`: 显示失败的测试
- `--verbosity normal`: 显示所有测试详情
- `--verbosity detailed`: 显示详细调试信息

---

## 测试数据管理

### 临时文件

使用 `IDisposable` 模式管理临时文件：

```csharp
public class ConfigServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public ConfigServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"PulsarTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "Profiles.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
```

---

## 常见问题 (FAQ)

### Q1: 为什么不能直接 `new PulsarContext()`？

**A**: `PulsarContext` 的构造函数是私有的，必须通过 `PulsarContext.Capture()` 静态方法创建。在测试中使用 `PulsarContextFactory.CreateTestContext()` 来创建测试实例。

### Q2: 如何测试需要 `IConfigService` 的代码？

**A**: 使用 Moq 创建 Mock 对象，并在 `ServiceCollection` 中注册：

```csharp
var mockConfigService = new Mock<IConfigService>();
mockConfigService.Setup(x => x.Current).Returns(new ProfilesConfig());

var services = new ServiceCollection();
services.AddSingleton(mockConfigService.Object);
```

### Q3: 断路器测试为什么需要第 4 次调用？

**A**: 断路器在第 3 次失败时触发，但当前请求仍然会执行并返回异常消息。只有**下一次**（第 4 次）调用才会看到 "disabled for safety" 消息。

### Q4: 如何测试异步方法？

**A**: 测试方法使用 `async Task` 签名，并使用 `await` 调用被测方法：

```csharp
[Fact]
public async Task MyTest()
{
    var result = await service.DoSomethingAsync();
    result.Should().NotBeNull();
}
```

### Q5: 如何验证日志输出？

**A**: 使用 Moq 验证 `ILogger` 的调用：

```csharp
_mockLogger.Verify(
    x => x.Log(
        LogLevel.Error,
        It.IsAny<EventId>(),
        It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("expected message")),
        It.IsAny<Exception>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
    Times.Once);
```

---

## 测试覆盖率目标

- **核心功能**: 100% 覆盖（插件系统、配置管理）
- **业务逻辑**: 80%+ 覆盖
- **UI 层**: 不强制要求（WPF 测试复杂度高）

---

## 贡献指南

### 添加新测试时

1. **选择正确的测试类**: 根据功能模块选择或创建测试类
2. **使用 PulsarContextFactory**: 创建 `PulsarContext` 实例
3. **遵循 AAA 模式**: Arrange-Act-Assert
4. **使用 FluentAssertions**: 提高断言可读性
5. **添加描述性断言消息**: 使用 `because` 参数说明断言原因

### 示例

```csharp
[Fact]
public async Task NewFeature_ShouldWork_WhenConditionMet()
{
    // Arrange
    var context = PulsarContextFactory.CreateTestContext();
    var service = new MyService();
    
    // Act
    var result = await service.NewFeatureAsync(context);
    
    // Assert
    result.Success.Should().BeTrue("the new feature should succeed when condition is met");
    result.Data.Should().NotBeNull();
}
```

---

## 更新日志

- **2026-03-03**: 初始版本，91 个测试全部通过
- **2026-06-14**: 更新为 331+ 测试，新增 Services / ViewModels / Tutorial / Plugins 测试目录
  - 添加 `PulsarContextFactory` 测试辅助类
  - 修复 `PluginRegistry` 构造函数中 `_configService` 初始化问题
  - 修复断路器重置逻辑
  - 修复 JSON 数字类型规范化问题

---

## 参考资源

- [xUnit 文档](https://xunit.net/)
- [FluentAssertions 文档](https://fluentassertions.com/)
- [Moq 文档](https://github.com/moq/moq4)
- [Pulsar AGENTS.md](../AGENTS.md) - 项目架构和开发规范

---

**维护者**: Pulsar 开发团队  
**最后更新**: 2026-06-14
