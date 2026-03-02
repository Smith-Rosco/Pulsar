# 📋 Pulsar 插件系统现代化 - Phase 2 任务清单

## 🎯 Phase 2 目标

将插件系统升级为完整的企业级热插拔平台，实现热重载、权限管理、依赖隔离和插件市场。

**预计时间**: 2-3 周  
**前置条件**: Phase 1 已完成 ✅

---

## 📊 任务概览

| 任务 | 优先级 | 预计时间 | 状态 | 负责人 |
|------|--------|---------|------|--------|
| 1. 热重载管理器 | P0 | 4 天 | ✅ 已完成 | OpenCode |
| 2. 权限系统 | P0 | 4 天 | 🔴 待开始 | - |
| 3. 依赖隔离增强 | P0 | 4 天 | 🔴 待开始 | - |
| 4. 插件包管理器 | P1 | 5 天 | 🔴 待开始 | - |
| 5. 单元测试 | P1 | 3 天 | 🟡 进行中 | OpenCode |
| 6. 插件市场 UI | P2 | 5 天 | 🔴 待开始 | - |

---

## 🔥 P0 任务 (必须完成)

### 任务 1: 热重载管理器 ✅

**目标**: 实现文件监听和自动热重载

**子任务**:
- [x] 1.1 创建 `HotReloadManager.cs`
  - 实现 FileSystemWatcher 监听插件目录
  - 实现文件变更事件处理
  - 实现防抖逻辑 (避免频繁触发)
  
- [x] 1.2 实现 Shadow Copy 机制
  - 创建临时目录 `%Temp%\Pulsar\PluginShadow`
  - 实现文件复制逻辑
  - 实现旧文件清理

- [x] 1.3 集成到 PluginRegistryV2
  - 添加 `EnableHotReload()` 方法
  - 添加 `DisableHotReload()` 方法
  - 添加热重载事件通知

- [x] 1.4 编写集成测试
  - 测试文件变更检测
  - 测试自动重载
  - 测试 Shadow Copy

**文件清单**:
```
Core/Plugin/HotReloadManager.cs          (已创建 ✅)
Services/PluginRegistryV2.cs             (已修改 ✅)
Pulsar.Tests/Plugin/HotReloadTests.cs    (已创建 ✅)
```

**验收标准**:
- ✅ 插件文件变更后 500ms 内自动重载
- ✅ Shadow Copy 正常工作
- ✅ 旧插件内存正确释放
- ✅ 集成测试通过 (12/12 tests passed)

---

### 任务 2: 权限系统

**目标**: 实现声明式权限和运行时检查

**子任务**:
- [ ] 2.1 创建权限定义
  - 创建 `PluginPermission.cs` (枚举)
  - 定义所有权限类型
  - 创建权限组合 (Basic/Standard/Full)

- [ ] 2.2 实现权限拦截器
  - 创建 `PermissionInterceptor.cs`
  - 实现 `CheckPermission()` 方法
  - 集成到 PulsarContext

- [ ] 2.3 修改 PulsarContext
  - 添加权限检查到所有敏感操作
  - 添加当前插件 ID 追踪
  - 添加权限拒绝异常

- [ ] 2.4 创建权限授权 UI
  - 创建 `PermissionRequestDialog.xaml`
  - 实现权限请求逻辑
  - 实现权限记忆功能

**文件清单**:
```
Core/Plugin/Security/PluginPermission.cs       (新建)
Core/Plugin/Security/PermissionInterceptor.cs  (新建)
Core/Plugin/PulsarContext.cs                   (修改 - 添加权限检查)
Views/Dialogs/PermissionRequestDialog.xaml     (新建)
ViewModels/Dialogs/PermissionRequestViewModel.cs (新建)
```

**验收标准**:
- ✅ 所有敏感操作都有权限检查
- ✅ 未授权操作抛出 UnauthorizedAccessException
- ✅ 权限请求 UI 正常显示
- ✅ 权限记忆功能正常

---

### 任务 3: 依赖隔离增强

**目标**: 解决依赖版本冲突问题

**子任务**:
- [ ] 3.1 实现 Shim Assembly 生成
  - 创建 `DependencyShim.cs`
  - 实现 deps.json 生成
  - 实现 binding redirect

- [ ] 3.2 实现 NuGet 包解析
  - 创建 `NuGetPackageResolver.cs`
  - 实现包下载逻辑
  - 实现包缓存管理

- [ ] 3.3 实现依赖冲突检测
  - 创建 `DependencyConflictDetector.cs`
  - 实现版本冲突检测
  - 实现自动解决策略

- [ ] 3.4 集成到 PluginLoader
  - 修改 `PluginLoader.LoadAll()`
  - 添加依赖预处理步骤
  - 添加依赖验证

**文件清单**:
```
Core/Plugin/DependencyShim.cs                (新建)
Core/Plugin/NuGetPackageResolver.cs         (新建)
Core/Plugin/DependencyConflictDetector.cs   (新建)
Core/Plugin/PluginLoader.cs                 (修改)
```

**验收标准**:
- ✅ 不同插件可以使用不同版本的依赖
- ✅ NuGet 包自动下载
- ✅ 依赖冲突自动解决
- ✅ 依赖验证测试通过

---

## 🟡 P1 任务 (重要)

### 任务 4: 插件包管理器

**目标**: 实现插件的安装、更新、卸载

**子任务**:
- [ ] 4.1 创建本地仓库
  - 创建 `PluginRepository.cs`
  - 实现 index.json 管理
  - 实现版本存储结构

- [ ] 4.2 实现包管理器
  - 创建 `PluginPackageManager.cs`
  - 实现 `InstallAsync()` 方法
  - 实现 `UpdateAsync()` 方法
  - 实现 `UninstallAsync()` 方法

- [ ] 4.3 实现依赖解析
  - 集成 PluginVersionResolver
  - 实现递归依赖安装
  - 实现依赖树验证

- [ ] 4.4 创建包管理 UI
  - 创建 `PluginMarketPage.xaml`
  - 实现插件列表显示
  - 实现安装/更新按钮

**文件清单**:
```
Services/PluginRepository.cs              (新建)
Services/PluginPackageManager.cs          (新建)
Views/Pages/PluginMarketPage.xaml         (新建)
ViewModels/Settings/PluginMarketViewModel.cs (新建)
```

**验收标准**:
- ✅ 可以安装插件
- ✅ 可以更新插件
- ✅ 可以卸载插件
- ✅ 依赖自动安装
- ✅ UI 正常工作

---

### 任务 5: 单元测试

**目标**: 建立完整的测试覆盖

**子任务**:
- [ ] 5.1 创建测试项目
  ```bash
  dotnet new xunit -o Pulsar/Pulsar.Tests
  dotnet add Pulsar/Pulsar.Tests reference Pulsar/Pulsar/Pulsar.csproj
  dotnet add Pulsar/Pulsar.Tests package Moq
  dotnet add Pulsar/Pulsar.Tests package FluentAssertions
  ```

- [ ] 5.2 编写核心测试
  - UnloadablePluginContextTests.cs
  - PluginHostTests.cs
  - PluginVersionResolverTests.cs
  - PluginManifestLoaderTests.cs

- [ ] 5.3 编写集成测试
  - PluginRegistryV2Tests.cs
  - HotReloadTests.cs
  - PermissionTests.cs

- [ ] 5.4 编写性能测试
  - 使用 BenchmarkDotNet
  - 测试加载/卸载性能
  - 测试内存泄漏

**文件清单**:
```
Pulsar.Tests/
  ├── Plugin/
  │   ├── UnloadablePluginContextTests.cs
  │   ├── PluginHostTests.cs
  │   ├── VersionResolverTests.cs
  │   └── HotReloadTests.cs
  ├── Services/
  │   └── PluginRegistryV2Tests.cs
  └── Benchmarks/
      └── PluginPerformanceTests.cs
```

**验收标准**:
- ✅ 测试覆盖率 > 80%
- ✅ 所有测试通过
- ✅ 内存泄漏测试通过
- ✅ 性能基准建立

---

## 🟢 P2 任务 (可选)

### 任务 6: 插件市场 UI

**目标**: 创建可视化的插件浏览和管理界面

**子任务**:
- [ ] 6.1 设计 UI 原型
- [ ] 6.2 实现插件列表
- [ ] 6.3 实现插件详情页
- [ ] 6.4 实现搜索和筛选
- [ ] 6.5 实现评分和评论

**预计时间**: 5 天

---

## 📝 开发规范

### 代码规范
- 遵循 `AGENTS.md` 中的代码规范
- 所有公共 API 必须有 XML 注释
- 使用 `async/await` 处理异步操作
- 使用 `ILogger` 记录日志

### 提交规范
```
feat: 添加热重载管理器
fix: 修复权限检查逻辑
docs: 更新 Phase 2 文档
test: 添加插件宿主测试
```

### 测试规范
- 每个新功能必须有单元测试
- 集成测试覆盖关键流程
- 性能测试验证性能指标

---

## 🧪 测试清单

### 功能测试
- [ ] 热重载功能测试
- [ ] 权限系统测试
- [ ] 依赖隔离测试
- [ ] 包管理器测试

### 性能测试
- [ ] 插件加载时间 < 100ms
- [ ] 插件卸载时间 < 50ms
- [ ] 内存释放率 > 95%
- [ ] 热重载时间 < 500ms

### 兼容性测试
- [ ] 旧插件兼容性
- [ ] 多版本共存
- [ ] 依赖冲突处理

---

## 📚 参考资料

### 技术文档
- [AssemblyLoadContext](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)
- [FileSystemWatcher](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher)
- [NuGet Client SDK](https://learn.microsoft.com/en-us/nuget/reference/nuget-client-sdk)

### 项目文档
- `Docs/PLUGIN_SYSTEM_MODERNIZATION_PHASE1.md`
- `Docs/PLUGIN_QUICKSTART.md`
- `AGENTS.md`

---

## 🎯 里程碑

### Milestone 1: 核心功能 (Week 1)
- ✅ 热重载管理器完成
- ✅ 权限系统完成
- ✅ 依赖隔离完成

### Milestone 2: 高级功能 (Week 2)
- ✅ 插件包管理器完成
- ✅ 单元测试完成
- ✅ 文档更新完成

### Milestone 3: 优化和发布 (Week 3)
- ✅ 性能优化完成
- ✅ 插件市场 UI 完成
- ✅ Phase 2 发布

---

## ✅ 完成标准

### Phase 2 完成条件
- [ ] 所有 P0 任务完成
- [ ] 所有 P1 任务完成
- [ ] 测试覆盖率 > 80%
- [ ] 性能指标达标
- [ ] 文档更新完成
- [ ] 代码审查通过

### 发布清单
- [ ] 构建成功 (0 警告, 0 错误)
- [ ] 所有测试通过
- [ ] 文档完整
- [ ] Git 提交完成
- [ ] Release Notes 编写完成

---

## 📞 支持

如有问题，请参考:
- `HANDOVER.md` - 交接文档
- `Docs/PLUGIN_QUICKSTART.md` - 快速开始
- Git Commit `71b47dd` - Phase 1 实现

---

*任务清单创建时间: 2026-03-02*
*当前状态: Phase 1 完成, Phase 2 待开始*
