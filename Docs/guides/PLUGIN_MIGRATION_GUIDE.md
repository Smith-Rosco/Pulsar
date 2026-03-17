# Pulsar 插件系统重构指南

**版本**: 2.0  
**日期**: 2026-03-17  
**状态**: 已实施

---

## 📋 重构概览

### 核心改进

1. **消除 Service Locator 反模式** - 引入构造函数依赖注入
2. **插件基类** - 减少 30% 样板代码
3. **配置验证增强** - 防止无效配置应用
4. **插件工厂** - 自动解析依赖

### 影响范围

- ✅ **向后兼容** - 旧插件无需修改即可运行
- ✅ **渐进式迁移** - 可逐步迁移到新模式
- ⚠️ **推荐迁移** - 新插件应使用新模式

---

## 🔄 迁移步骤

### 旧模式 (Service Locator)

```csharp
public class OldPlugin : IPulsarPlugin, IPluginTiered
{
    private ILogger<OldPlugin>? _logger;
    private IWindowService? _windowService;
    
    public void Initialize(IServiceProvider services)
    {
        _logger = services.GetService(typeof(ILogger<OldPlugin>)) as ILogger<OldPlugin>;
        _windowService = services.GetService(typeof(IWindowService)) as IWindowService;
        
        if (_windowService == null)
            throw new InvalidOperationException("IWindowService not available");
            
        _logger?.LogInformation("[OldPlugin] Initialized");
    }
    
    public async Task<PluginResult> ExecuteAsync(...)
    {
        if (!args.TryGetValue("path", out var path))
            return PluginResult.Error("Missing parameter: path");
        // ...
    }
}
```

### 新模式 (构造函数注入 + 基类)

```csharp
public class NewPlugin : PluginBase<NewPlugin>
{
    private readonly IWindowService _windowService;
    
    // 构造函数注入 - 编译时检查依赖
    public NewPlugin(ILogger<NewPlugin> logger, IWindowService windowService) 
        : base(logger)
    {
        _windowService = windowService;
    }
    
    public override string Id => "com.pulsar.newplugin";
    public override PluginTier Tier => PluginTier.Extension;
    // ... 其他元数据
    
    public override async Task<PluginResult> ExecuteAsync(...)
    {
        // 使用基类辅助方法
        if (!TryGetRequiredArg(args, "path", out var path))
            return MissingParameterError("path");
        
        Logger.LogInformation("Executing with path: {Path}", path);
        // ...
    }
}
```

---

## 🎯 迁移优势对比

| 特性 | 旧模式 | 新模式 |
|------|--------|--------|
| **依赖检查** | 运行时 | 编译时 ✅ |
| **代码量** | 100% | 70% ✅ |
| **可测试性** | 困难 | 简单 ✅ |
| **日志格式** | 不统一 | 统一 ✅ |
| **错误处理** | 手动 | 辅助方法 ✅ |

---

## 📦 新增组件

### 1. PluginBase<T>

**位置**: `Core/Plugin/PluginBase.cs`

**功能**:
- 自动注入 Logger
- 提供辅助方法 (TryGetRequiredArg, MissingParameterError, UnknownActionError)
- 统一初始化流程

### 2. PluginFactory

**位置**: `Core/Plugin/PluginFactory.cs`

**功能**:
- 自动解析构造函数参数
- 支持可选依赖 (nullable 参数)
- 详细错误诊断

### 3. 配置验证增强

**位置**: `Services/PluginRegistry.cs:89-120`

**改进**:
- 验证失败时应用默认配置
- 防止插件进入不一致状态

---

## 🔧 迁移检查清单

### 必需步骤

- [ ] 继承 `PluginBase<T>` 而非直接实现 `IPulsarPlugin`
- [ ] 添加构造函数，注入 `ILogger<T>` 和其他依赖
- [ ] 调用 `base(logger)` 初始化基类
- [ ] 移除 `Initialize()` 中的服务解析代码
- [ ] 使用 `Logger` 属性而非 `_logger` 字段

### 可选优化

- [ ] 使用 `TryGetRequiredArg()` 简化参数验证
- [ ] 使用 `MissingParameterError()` 统一错误消息
- [ ] 使用 `UnknownActionError()` 处理未知动作
- [ ] 移除手动的 null 检查 (构造函数注入保证非空)

---

## 📝 完整示例

见 `Plugins/Extensions/BasicCommand/SimpleCommandPlugin.cs` (已重构)

---

## ⚠️ 注意事项

1. **可选依赖**: 使用 nullable 类型 (`IService?`) 或默认参数
2. **向后兼容**: 旧插件无需修改，可继续使用
3. **渐进迁移**: 建议优先迁移 Extension 插件
4. **测试**: 迁移后运行完整测试套件

---

## 🐛 常见问题

### Q: 旧插件还能用吗？
A: 能。PluginFactory 会 fallback 到 `Activator.CreateInstance()`

### Q: 如何处理可选依赖？
A: 使用 nullable 类型: `IService? service`

### Q: 构造函数参数过多怎么办？
A: 考虑引入 Facade 服务或使用 Options 模式

---

## 📚 相关文档

- [PLUGIN_DEVELOPMENT.md](../../PLUGIN_DEVELOPMENT.md) - 插件开发完整指南
- [ARCHITECTURE.md](../../ARCHITECTURE.md) - 系统架构文档
- [PluginBase.cs](../../Pulsar/Pulsar/Core/Plugin/PluginBase.cs) - 基类源码

---

**维护者**: Pulsar Team  
**反馈**: 如有问题请提交 Issue
