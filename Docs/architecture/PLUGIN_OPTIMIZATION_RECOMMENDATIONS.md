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

## 🔧 已实施重构 (P0/P1)

### 1. PluginBase<T> 抽象基类

**文件**: `Core/Plugin/PluginBase.cs`

**收益**:
- 减少 30% 样板代码
- 统一日志格式
- 提供辅助方法 (TryGetRequiredArg, MissingParameterError, UnknownActionError)

**迁移示例**: 见 `SimpleCommandPlugin.cs`

---

### 2. PluginFactory 依赖注入

**文件**: `Core/Plugin/PluginFactory.cs`

**收益**:
- 支持构造函数注入
- 编译时依赖检查
- 自动解析可选依赖
- 向后兼容旧插件

**集成**: PluginLoader 已更新使用

---

### 3. 配置验证增强

**文件**: `Services/PluginRegistry.cs:89-120`

**修复**:
- 验证失败时应用默认配置
- 防止插件进入不一致状态
- 记录详细错误日志

---

## 📋 未来优化路线图

### Phase 2: 架构重构 (P2 - 建议 Q2 2026)

#### 拆分 PluginRegistry

**当前问题**: 单个类承担 7 个职责，违反 SRP

**建议设计**:

```csharp
// 1. 纯注册表
public interface IPluginRegistry
{
    void Register(IPulsarPlugin plugin);
    IPulsarPlugin? GetPlugin(string pluginId);
    IEnumerable<IPulsarPlugin> GetAllPlugins();
}

// 2. 执行调度器
public interface IPluginExecutor
{
    Task<PluginResult> ExecuteAsync(string pluginId, string action, 
        IReadOnlyDictionary<string, string> args, PulsarContext context);
}

// 3. 熔断管理器
public interface ICircuitBreakerManager
{
    bool IsCircuitOpen(string pluginId);
    void RecordSuccess(string pluginId);
    void RecordFailure(string pluginId, Exception ex);
    CircuitBreakerState GetState(string pluginId);
}

// 4. 生命周期管理器
public interface IPluginLifecycleManager
{
    Task EnablePluginAsync(string pluginId);
    Task DisablePluginAsync(string pluginId);
    Task UnloadAllAsync();
}
```

**收益**:
- 单一职责，易于测试
- 独立演进
- 更好的关注点分离

---

### Phase 3: 插件通信 (P3 - 建议 Q3 2026)

#### 插件事件总线

**需求场景**:
- 插件 A 需要通知插件 B 某个事件
- 插件需要访问其他插件的功能

**建议设计**:

```csharp
public interface IPluginEventBus
{
    void Publish<TEvent>(TEvent eventData) where TEvent : IPluginEvent;
    void Subscribe<TEvent>(string pluginId, Action<TEvent> handler) where TEvent : IPluginEvent;
    void Unsubscribe<TEvent>(string pluginId) where TEvent : IPluginEvent;
}

// 使用示例
public class PluginA : PluginBase<PluginA>
{
    private readonly IPluginEventBus _eventBus;
    
    public PluginA(ILogger<PluginA> logger, IPluginEventBus eventBus) 
        : base(logger)
    {
        _eventBus = eventBus;
    }
    
    public override async Task<PluginResult> ExecuteAsync(...)
    {
        // 发布事件
        _eventBus.Publish(new WindowSwitchedEvent { ProcessName = "EXCEL" });
        return PluginResult.Ok();
    }
}
```

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

## 📊 迁移进度

### 已迁移插件

- ✅ SimpleCommandPlugin (示例)

### 待迁移插件 (推荐顺序)

1. **Extension 插件** (低风险)
   - BookmarkletRunnerPlugin
   - VbaRunnerPlugin

2. **Core 插件** (需谨慎测试)
   - WinSwitcherPlugin
   - PkiPlugin
   - SystemCommandPlugin

---

## 🔍 代码审查建议

### 新插件开发规范

1. **必须**: 继承 `PluginBase<T>`
2. **必须**: 使用构造函数注入
3. **必须**: 实现 `IPluginTiered`
4. **推荐**: 使用基类辅助方法
5. **推荐**: 实现 `IPluginLifecycle` (如有资源管理需求)

### Code Review Checklist

```markdown
- [ ] 继承 PluginBase<T>
- [ ] 构造函数注入所有依赖
- [ ] 使用 Logger 属性而非 _logger 字段
- [ ] 使用 TryGetRequiredArg() 验证参数
- [ ] 正确设置 PluginErrorSeverity
- [ ] 实现 IPluginLifecycle (如需资源管理)
- [ ] 添加 XML 文档注释
```

---

## 📖 参考文档

- [PLUGIN_MIGRATION_GUIDE.md](../guides/PLUGIN_MIGRATION_GUIDE.md) - 迁移步骤
- [PLUGIN_DEVELOPMENT.md](../../PLUGIN_DEVELOPMENT.md) - 开发指南
- [PLUGIN_SYSTEM.md](./PLUGIN_SYSTEM.md) - 系统架构
- [PluginBase.cs](../../Pulsar/Pulsar/Core/Plugin/PluginBase.cs) - 基类源码

---

**审查人**: 资深架构师  
**批准人**: Pulsar Team  
**生效日期**: 2026-03-17
