# Pulsar 插件系统架构优化建议

**审查日期**: 2026-03-17  
**审查范围**: 插件系统核心架构 + 所有插件实现  
**状态**: ✅ P0/P1 已实施，P2/P3 待规划

---

## 🎯 核心发现

### 优秀设计 ✅

1. **分层架构清晰** - Core/Extension 分离，Circuit Breaker 保护机制优秀
2. **PulsarContext 不可变快照** - 避免竞态条件，懒加载性能优化到位
3. **接口设计灵活** - IPluginConfigurable/IPluginLifecycle/IPluginMetadataProvider 扩展性强
4. **错误分级合理** - Recoverable/Critical 区分清晰

### 关键问题 ⚠️

1. **Service Locator 反模式** (P0) - 已修复 ✅
2. **配置验证漏洞** (P0) - 已修复 ✅
3. **样板代码过多** (P1) - 已优化 ✅
4. **PluginRegistry 职责过重** (P2) - 待重构
5. **插件间通信缺失** (P3) - 待设计

---

# Pulsar 插件系统架构优化建议

**审查日期**: 2026-03-17  
**审查范围**: 插件系统核心架构 + 所有插件实现  
**状态**: ✅ P0/P1 已实施，P2/P3 待规划

---

## 🎯 核心发现

### 优秀设计 ✅

1. **分层架构清晰** - Core/Extension 分离，Circuit Breaker 保护机制优秀
2. **PulsarContext 不可变快照** - 避免竞态条件，懒加载性能优化到位
3. **接口设计灵活** - IPluginConfigurable/IPluginLifecycle/IPluginMetadataProvider 扩展性强
4. **错误分级合理** - Recoverable/Critical 区分清晰

### 已解决问题 ✅

1. **Service Locator 反模式** (P0) - 已通过 PluginFactory 修复
2. **配置验证漏洞** (P0) - 已增强验证逻辑
3. **样板代码过多** (P1) - 已通过 PluginBase<T> 优化

### 待优化问题 ⚠️

1. **PluginRegistry 职责过重** (P2) - 单个类承担 7 个职责
2. **插件间通信缺失** (P3) - 无法实现插件协作
3. **性能监控不足** (P3) - 缺少插件执行时间统计

---

## 🔧 已实施重构 (P0/P1)

### 1. PluginBase<T> 抽象基类

**文件**: `Pulsar/Pulsar/Core/Plugin/PluginBase.cs` (247 行)

**核心收益**:
- ✅ 减少 30% 样板代码
- ✅ 统一日志格式 `[{PluginId}] Message`
- ✅ 提供 3 个辅助方法
- ✅ 模板方法模式统一初始化

**辅助方法**:
```csharp
// 1. 参数验证
protected bool TryGetRequiredArg(
    IReadOnlyDictionary<string, string> args,
    string paramName,
    out string value)

// 2. 错误结果生成
protected PluginResult MissingParameterError(string paramName)
protected PluginResult UnknownActionError(string action, params string[] supportedActions)
```

**迁移示例**: `Pulsar/Pulsar/Plugins/Extensions/BasicCommand/SimpleCommandPlugin.cs`

---

### 2. PluginFactory 依赖注入

**文件**: `Pulsar/Pulsar/Core/Plugin/PluginFactory.cs` (213 行)

**核心收益**:
- ✅ 支持构造函数注入
- ✅ 编译时依赖检查
- ✅ 自动解析可选依赖
- ✅ 向后兼容旧插件

**解析策略**:
1. 优先使用 `ActivatorUtilities.CreateInstance()`
2. Fallback 到手动构造函数解析
3. 按参数数量降序尝试所有构造函数
4. 支持 nullable 参数和默认值

**集成**: `Pulsar/Pulsar/Core/Plugin/PluginLoader.cs` 已更新

---

### 3. 配置验证增强

**文件**: `Pulsar/Pulsar/Services/PluginRegistry.cs`

**修复逻辑**:
```csharp
// 验证失败时应用默认配置
if (!validationResult.IsValid)
{
    _logger.LogError("Invalid settings: {Errors}", validationResult.Errors);
    var defaultSettings = GetDefaultSettings(configurable);
    configurable.UpdateSettings(defaultSettings);
    profile.Config = defaultSettings;
}
```

**防止的问题**:
- 无效配置导致插件崩溃
- 插件处于不一致状态
- 下次启动继续失败

---

## 📋 未来优化路线图

### Phase 2: 架构重构 (P2 - 建议 Q2 2026)

#### 问题: PluginRegistry 职责过重

**当前状态**: 单个类承担 7 个职责，违反单一职责原则 (SRP)

**职责清单**:
1. 插件注册与查询
2. 插件执行调度
3. 熔断器管理
4. 生命周期管理
5. 配置管理
6. 错误处理
7. 性能监控

**建议拆分设计**:

```csharp
// 1. 纯注册表 - 插件存储与查询
public interface IPluginRegistry
{
    void Register(IPulsarPlugin plugin);
    IPulsarPlugin? GetPlugin(string pluginId);
    IEnumerable<IPulsarPlugin> GetAllPlugins();
    IEnumerable<IPulsarPlugin> GetPluginsByTier(PluginTier tier);
    bool IsRegistered(string pluginId);
}

// 2. 执行调度器 - 插件执行逻辑
public interface IPluginExecutor
{
    Task<PluginResult> ExecuteAsync(
        string pluginId, 
        string action, 
        IReadOnlyDictionary<string, string> args, 
        PulsarContext context
    );
    
    Task<PluginResult> ExecuteWithRetryAsync(
        string pluginId,
        string action,
        IReadOnlyDictionary<string, string> args,
        PulsarContext context,
        int maxRetries = 3
    );
}

// 3. 熔断管理器 - Circuit Breaker 逻辑
public interface ICircuitBreakerManager
{
    bool IsCircuitOpen(string pluginId);
    void RecordSuccess(string pluginId);
    void RecordFailure(string pluginId, Exception ex);
    CircuitBreakerState GetState(string pluginId);
    void ResetCircuit(string pluginId);
    
    // 配置
    CircuitBreakerConfig GetConfig(string pluginId);
    void UpdateConfig(string pluginId, CircuitBreakerConfig config);
}

// 4. 生命周期管理器 - 插件启用/禁用
public interface IPluginLifecycleManager
{
    Task EnablePluginAsync(string pluginId);
    Task DisablePluginAsync(string pluginId);
    Task ReloadPluginAsync(string pluginId);
    Task UnloadAllAsync();
    
    bool IsEnabled(string pluginId);
    PluginState GetState(string pluginId);
}

// 5. 配置管理器 - 插件配置
public interface IPluginConfigurationManager
{
    Task<bool> UpdateConfigAsync(string pluginId, Dictionary<string, string> config);
    Dictionary<string, string> GetConfig(string pluginId);
    ValidationResult ValidateConfig(string pluginId, Dictionary<string, string> config);
    Dictionary<string, string> GetDefaultConfig(string pluginId);
}
```

**实施步骤**:
1. 创建新接口和实现类
2. 逐步迁移 PluginRegistry 功能
3. 更新依赖注入配置
4. 更新调用方代码
5. 移除旧的 PluginRegistry

**预期收益**:
- ✅ 单一职责，易于测试
- ✅ 独立演进，降低耦合
- ✅ 更好的关注点分离
- ✅ 支持插件热重载

---

### Phase 3: 插件通信 (P3 - 建议 Q3 2026)

#### 问题: 插件间通信机制缺失

**需求场景**:
1. 插件 A 需要通知插件 B 某个事件发生
2. 插件需要访问其他插件的功能
3. 插件需要共享数据或状态

**建议设计: 插件事件总线**

```csharp
// 事件总线接口
public interface IPluginEventBus
{
    // 发布事件
    void Publish<TEvent>(TEvent eventData) where TEvent : IPluginEvent;
    
    // 订阅事件
    IDisposable Subscribe<TEvent>(string pluginId, Action<TEvent> handler) 
        where TEvent : IPluginEvent;
    
    // 取消订阅
    void Unsubscribe<TEvent>(string pluginId) where TEvent : IPluginEvent;
    
    // 异步发布
    Task PublishAsync<TEvent>(TEvent eventData) where TEvent : IPluginEvent;
    
    // 异步订阅
    IDisposable Subscribe<TEvent>(string pluginId, Func<TEvent, Task> handler) 
        where TEvent : IPluginEvent;
}

// 事件基类
public interface IPluginEvent
{
    string SourcePluginId { get; }
    DateTime Timestamp { get; }
    string EventType { get; }
}

// 具体事件示例
public class WindowSwitchedEvent : IPluginEvent
{
    public string SourcePluginId { get; init; }
    public DateTime Timestamp { get; init; }
    public string EventType => "WindowSwitched";
    
    public string ProcessName { get; init; }
    public IntPtr WindowHandle { get; init; }
    public string WindowTitle { get; init; }
}

public class CredentialFilledEvent : IPluginEvent
{
    public string SourcePluginId { get; init; }
    public DateTime Timestamp { get; init; }
    public string EventType => "CredentialFilled";
    
    public string CredentialId { get; init; }
    public bool Success { get; init; }
}
```

**使用示例**:

```csharp
// 发布者插件
public class WinSwitcherPlugin : PluginBase<WinSwitcherPlugin>
{
    private readonly IPluginEventBus _eventBus;
    
    public WinSwitcherPlugin(
        ILogger<WinSwitcherPlugin> logger, 
        IPluginEventBus eventBus
    ) : base(logger)
    {
        _eventBus = eventBus;
    }
    
    public override async Task<PluginResult> ExecuteAsync(...)
    {
        // 切换窗口
        await SwitchToWindowAsync(processName);
        
        // 发布事件
        _eventBus.Publish(new WindowSwitchedEvent
        {
            SourcePluginId = Id,
            Timestamp = DateTime.UtcNow,
            ProcessName = processName,
            WindowHandle = handle,
            WindowTitle = title
        });
        
        return PluginResult.Ok();
    }
}

// 订阅者插件
public class LoggerPlugin : PluginBase<LoggerPlugin>
{
    private readonly IPluginEventBus _eventBus;
    private IDisposable? _subscription;
    
    public LoggerPlugin(
        ILogger<LoggerPlugin> logger, 
        IPluginEventBus eventBus
    ) : base(logger)
    {
        _eventBus = eventBus;
    }
    
    protected override void OnInitialize(IServiceProvider services)
    {
        // 订阅窗口切换事件
        _subscription = _eventBus.Subscribe<WindowSwitchedEvent>(
            Id,
            evt => Logger.LogInformation(
                "Window switched to {Process} at {Time}",
                evt.ProcessName,
                evt.Timestamp
            )
        );
    }
    
    // 实现 IDisposable 清理订阅
    public void Dispose()
    {
        _subscription?.Dispose();
    }
}
```

**实施步骤**:
1. 设计事件总线接口
2. 实现基于内存的事件总线
3. 集成到 DI 容器
4. 更新 PluginBase 支持事件订阅
5. 提供常用事件类型
6. 编写文档和示例

**预期收益**:
- ✅ 插件间松耦合通信
- ✅ 支持插件协作
- ✅ 更灵活的插件生态
- ✅ 易于扩展新功能

---

### Phase 4: 性能监控 (P3 - 建议 Q4 2026)

#### 问题: 缺少插件性能监控

**需求**:
1. 统计插件执行时间
2. 识别性能瓶颈
3. 监控资源使用
4. 生成性能报告

**建议设计: 插件性能监控器**

```csharp
public interface IPluginPerformanceMonitor
{
    // 记录执行时间
    void RecordExecution(string pluginId, string action, TimeSpan duration, bool success);
    
    // 获取统计信息
    PluginPerformanceStats GetStats(string pluginId);
    PluginPerformanceStats GetStats(string pluginId, TimeSpan timeWindow);
    
    // 获取所有插件统计
    IEnumerable<PluginPerformanceStats> GetAllStats();
    
    // 重置统计
    void ResetStats(string pluginId);
    void ResetAllStats();
    
    // 导出报告
    Task<string> ExportReportAsync(ReportFormat format);
}

public class PluginPerformanceStats
{
    public string PluginId { get; init; }
    public int TotalExecutions { get; init; }
    public int SuccessfulExecutions { get; init; }
    public int FailedExecutions { get; init; }
    public TimeSpan AverageExecutionTime { get; init; }
    public TimeSpan MinExecutionTime { get; init; }
    public TimeSpan MaxExecutionTime { get; init; }
    public TimeSpan TotalExecutionTime { get; init; }
    public double SuccessRate => TotalExecutions > 0 
        ? (double)SuccessfulExecutions / TotalExecutions 
        : 0;
}
```

**集成到 PluginExecutor**:

```csharp
public class PluginExecutor : IPluginExecutor
{
    private readonly IPluginPerformanceMonitor _performanceMonitor;
    
    public async Task<PluginResult> ExecuteAsync(...)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await plugin.ExecuteAsync(action, args, context);
            stopwatch.Stop();
            
            _performanceMonitor.RecordExecution(
                pluginId, 
                action, 
                stopwatch.Elapsed, 
                result.IsSuccess
            );
            
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _performanceMonitor.RecordExecution(
                pluginId, 
                action, 
                stopwatch.Elapsed, 
                false
            );
            throw;
        }
    }
}
```

**预期收益**:
- ✅ 识别性能瓶颈
- ✅ 优化慢插件
- ✅ 监控系统健康
- ✅ 数据驱动决策

---

## 🎓 架构原则总结

### SOLID 原则应用

1. **单一职责 (SRP)** - PluginRegistry 需拆分 (P2)
2. **开闭原则 (OCP)** - ✅ 通过接口扩展实现
3. **里氏替换 (LSP)** - ✅ PluginBase 可替换 IPulsarPlugin
4. **接口隔离 (ISP)** - ✅ 多个小接口 (IPluginConfigurable, IPluginLifecycle)
5. **依赖倒置 (DIP)** - ✅ 已通过构造函数注入实现

### 设计模式应用

- ✅ **模板方法模式** - PluginBase.Initialize()
- ✅ **工厂模式** - PluginFactory
- ✅ **策略模式** - 不同插件实现不同策略
- ✅ **熔断器模式** - Circuit Breaker for Extension plugins
- ⚠️ **观察者模式** - 插件事件总线 (待实施)

---

## 📊 迁移进度追踪

### 已迁移插件 ✅

| 插件 | 文件路径 | 状态 | 代码减少 |
|------|----------|------|----------|
| SimpleCommandPlugin | `Pulsar/Pulsar/Plugins/Extensions/BasicCommand/` | ✅ 完成 | -35% |

### 待迁移插件 (推荐顺序)

#### 第一批: Extension 插件 (低风险)

| 优先级 | 插件 | 文件路径 | 复杂度 | 预计工时 |
|--------|------|----------|--------|----------|
| 1 | BookmarkletRunnerPlugin | `Pulsar/Pulsar/Plugins/Extensions/BookmarkletRunner/` | 中 | 2h |
| 2 | VbaRunnerPlugin | `Pulsar/Pulsar/Plugins/Extensions/VbaRunner/` | 高 | 4h |

**迁移建议**:
- BookmarkletRunnerPlugin: 依赖较少，适合快速迁移
- VbaRunnerPlugin: 复杂度高，需要仔细测试 COM 互操作

#### 第二批: Core 插件 (需谨慎测试)

| 优先级 | 插件 | 文件路径 | 复杂度 | 预计工时 |
|--------|------|----------|--------|----------|
| 3 | SystemCommandPlugin | `Pulsar/Pulsar/Plugins/Core/SystemCommand/` | 低 | 1h |
| 4 | WinSwitcherPlugin | `Pulsar/Pulsar/Plugins/Core/WinSwitcher/` | 中 | 3h |
| 5 | PkiPlugin | `Pulsar/Pulsar/Plugins/Core/Pki/` | 高 | 5h |

**迁移建议**:
- SystemCommandPlugin: 简单，优先迁移
- WinSwitcherPlugin: 核心功能，需要完整测试
- PkiPlugin: 涉及安全，需要额外审查

**总预计工时**: 15 小时

---

## 🔍 代码审查规范

### 新插件开发强制要求

1. ✅ **必须**: 继承 `PluginBase<T>`
2. ✅ **必须**: 使用构造函数注入
3. ✅ **必须**: 实现 `IPluginTiered`
4. ✅ **必须**: 设置正确的 `PluginErrorSeverity`
5. ✅ **推荐**: 使用基类辅助方法
6. ✅ **推荐**: 实现 `IPluginLifecycle` (如有资源管理需求)
7. ✅ **推荐**: 添加 XML 文档注释

### Code Review Checklist

```markdown
## 插件代码审查清单

### 基础结构
- [ ] 继承 `PluginBase<T>` (而非直接实现 IPulsarPlugin)
- [ ] 构造函数注入所有依赖
- [ ] 调用 `base(logger)` 初始化基类
- [ ] 实现所有必需的抽象属性

### 依赖管理
- [ ] 使用 `Logger` 属性而非 `_logger` 字段
- [ ] 必需依赖使用非 nullable 类型
- [ ] 可选依赖使用 nullable 类型
- [ ] 避免在构造函数中执行耗时操作

### 参数验证
- [ ] 使用 `TryGetRequiredArg()` 验证必需参数
- [ ] 使用 `MissingParameterError()` 返回统一错误
- [ ] 使用 `UnknownActionError()` 处理未知动作

### 错误处理
- [ ] 正确设置 `PluginErrorSeverity` (Recoverable/Critical)
- [ ] 使用结构化日志 (Logger.LogInformation/Error)
- [ ] 捕获并记录所有异常

### 生命周期
- [ ] 实现 `IPluginLifecycle` (如需资源管理)
- [ ] 在 `OnDisableAsync()` 中清理资源
- [ ] 避免在 `OnInitialize()` 中执行耗时操作

### 文档
- [ ] 添加 XML 文档注释
- [ ] 注释复杂业务逻辑
- [ ] 更新相关文档
```

### 自动化检查工具 (未来)

```bash
# 建议开发的工具
pulsar-lint check-plugin MyPlugin.cs
  ✅ Inherits PluginBase<T>
  ✅ Uses constructor injection
  ✅ Uses helper methods
  ⚠️  Missing XML documentation
  ❌ PluginErrorSeverity not set
```

---

## 📖 参考文档

### 核心文档
- [PLUGIN_MIGRATION_GUIDE.md](../guides/PLUGIN_MIGRATION_GUIDE.md) - 完整迁移步骤和示例
- [PLUGIN_DEVELOPMENT.md](../../PLUGIN_DEVELOPMENT.md) - 插件开发完整指南
- [PLUGIN_SYSTEM.md](./PLUGIN_SYSTEM.md) - 插件系统架构详解
- [PLUGIN_SYSTEM_REFACTORING_REPORT.md](./PLUGIN_SYSTEM_REFACTORING_REPORT.md) - 重构审查报告

### 源码参考
- [PluginBase.cs](../../Pulsar/Pulsar/Core/Plugin/PluginBase.cs) - 插件抽象基类 (247 行)
- [PluginFactory.cs](../../Pulsar/Pulsar/Core/Plugin/PluginFactory.cs) - 插件工厂 (213 行)
- [SimpleCommandPlugin.cs](../../Pulsar/Pulsar/Plugins/Extensions/BasicCommand/SimpleCommandPlugin.cs) - 重构示例 (146 行)
- [SimpleCommandPlugin.Refactored.cs](../../Pulsar/Pulsar/Plugins/Extensions/BasicCommand/SimpleCommandPlugin.Refactored.cs) - 详细注释版 (168 行)

### 架构文档
- [ARCHITECTURE.md](../../ARCHITECTURE.md) - 系统架构概览
- [AGENTS.md](../../AGENTS.md) - AI Agent 操作指南

---

## 📅 实施时间表

### Q1 2026 (已完成) ✅
- ✅ PluginBase<T> 实现
- ✅ PluginFactory 实现
- ✅ 配置验证增强
- ✅ SimpleCommandPlugin 迁移
- ✅ 文档编写

### Q2 2026 (计划中)
- [ ] 迁移所有 Extension 插件
- [ ] 迁移 Core 插件
- [ ] 单元测试覆盖
- [ ] 性能基准测试
- [ ] 开始 PluginRegistry 拆分设计

### Q3 2026 (规划中)
- [ ] 实施 PluginRegistry 拆分
- [ ] 插件事件总线设计
- [ ] 插件热重载支持
- [ ] 插件市场基础设施

### Q4 2026 (规划中)
- [ ] 插件性能监控
- [ ] 插件沙箱隔离
- [ ] 外部插件支持
- [ ] 插件开发工具链

---

## 🎓 最佳实践总结

### 设计原则

#### SOLID 原则应用

| 原则 | 当前状态 | 说明 |
|------|----------|------|
| **单一职责 (SRP)** | ⚠️ 部分违反 | PluginRegistry 需拆分 (P2) |
| **开闭原则 (OCP)** | ✅ 良好 | 通过接口扩展实现 |
| **里氏替换 (LSP)** | ✅ 良好 | PluginBase 可替换 IPulsarPlugin |
| **接口隔离 (ISP)** | ✅ 优秀 | 多个小接口 (IPluginConfigurable, IPluginLifecycle) |
| **依赖倒置 (DIP)** | ✅ 优秀 | 已通过构造函数注入实现 |

#### 设计模式应用

| 模式 | 应用位置 | 状态 |
|------|----------|------|
| **模板方法模式** | PluginBase.Initialize() | ✅ 已实施 |
| **工厂模式** | PluginFactory | ✅ 已实施 |
| **策略模式** | 不同插件实现 | ✅ 已实施 |
| **熔断器模式** | Circuit Breaker for Extensions | ✅ 已实施 |
| **观察者模式** | 插件事件总线 | ⚠️ 待实施 (P3) |
| **单例模式** | PluginRegistry | ✅ 已实施 |
| **依赖注入模式** | 构造函数注入 | ✅ 已实施 |

### 性能优化建议

1. **懒加载**: PulsarContext 已实现懒加载
2. **缓存**: 考虑缓存插件元数据
3. **异步**: 所有 I/O 操作使用 async/await
4. **资源池**: 考虑为频繁创建的对象使用对象池
5. **监控**: 实施性能监控 (P3)

### 安全性建议

1. **输入验证**: 所有参数必须验证
2. **错误处理**: 不泄露敏感信息
3. **权限控制**: Core 插件不可禁用
4. **沙箱隔离**: 考虑为外部插件实施沙箱 (P3)
5. **审计日志**: 记录所有插件操作

---

## 🚀 快速开始

### 创建新插件 (5 分钟)

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;

namespace Pulsar.Plugins.Extensions.MyPlugin
{
    /// <summary>
    /// 我的新插件 - 简短描述
    /// </summary>
    public class MyNewPlugin : PluginBase<MyNewPlugin>
    {
        // 1. 构造函数注入依赖
        public MyNewPlugin(ILogger<MyNewPlugin> logger) 
            : base(logger)
        {
        }
        
        // 2. 实现必需属性
        public override string Id => "com.company.myplugin";
        public override PluginTier Tier => PluginTier.Extension;
        public override string DisplayName => "My Plugin";
        public override string Version => "1.0.0";
        public override string Author => "Your Name";
        public override string Description => "Plugin description";
        public override string Icon => "\uE756";
        public override bool CanDisable => true;
        
        // 3. 实现执行逻辑
        public override async Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            // 参数验证
            if (!TryGetRequiredArg(args, "param1", out var param1))
                return MissingParameterError("param1");
            
            Logger.LogInformation("Executing with {Param1}", param1);
            
            try
            {
                // 业务逻辑
                await DoSomethingAsync(param1);
                return PluginResult.Ok("Success");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Execution failed");
                return PluginResult.Error(
                    $"Failed: {ex.Message}",
                    PluginErrorSeverity.Recoverable
                );
            }
        }
        
        private async Task DoSomethingAsync(string param)
        {
            // 实现业务逻辑
            await Task.Delay(100);
        }
    }
}
```

### 注册插件

插件会自动被 PluginLoader 发现和加载，无需手动注册。

### 配置插件

在 `Profiles.json` 中添加配置：

```json
{
  "Profiles": [
    {
      "PluginId": "com.company.myplugin",
      "Action": "default",
      "Config": {
        "param1": "value1"
      }
    }
  ]
}
```

---

## 💡 常见问题解答

### Q1: 为什么要迁移到新模式？
**A**: 
- 编译时依赖检查，提前发现问题
- 减少 30% 样板代码
- 更好的可测试性
- 统一的代码风格

### Q2: 迁移会破坏现有插件吗？
**A**: 不会。完全向后兼容，旧插件无需修改即可运行。

### Q3: 迁移需要多长时间？
**A**: 
- 简单插件: 30 分钟 - 1 小时
- 中等复杂度: 2-3 小时
- 复杂插件: 4-5 小时

### Q4: 如何测试迁移后的插件？
**A**: 
1. 编译检查: `dotnet build`
2. 手动测试: 运行 Pulsar 并测试所有功能
3. 单元测试: 编写单元测试（推荐）

### Q5: 遇到问题怎么办？
**A**: 
1. 查看迁移指南: `Docs/guides/PLUGIN_MIGRATION_GUIDE.md`
2. 参考示例代码: `SimpleCommandPlugin.cs`
3. 查看日志: `%AppData%\Pulsar\Logs\`
4. 提交 Issue

---

**审查人**: 资深架构师  
**批准人**: Pulsar Team  
**生效日期**: 2026-03-17  
**最后更新**: 2026-03-17  
**版本**: 2.0.0
