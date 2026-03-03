# Pulsar 项目交接文档

**更新日期**: 2026-03-02  
**当前阶段**: Phase 2 - 插件系统现代化  
**最新完成**: Task 2 - 权限系统

---

## 📋 项目概述

Pulsar 是一个高性能的 Windows 生产力启动器，采用径向菜单界面。

- **框架**: .NET 8.0 (WPF + WinForms)
- **架构**: 模块化单体 + MVVM + 依赖注入
- **核心功能**: 径向菜单、全局热键、PKI 密钥管理、可扩展插件系统

---

## 🎯 当前进度

### Phase 2: 插件系统现代化

| 任务 | 状态 | 完成日期 | 文档 |
|------|------|---------|------|
| 1. 热重载管理器 | ✅ 已完成 | 2026-03-02 | [归档](archive/2026-03/phase2-task1/) |
| 2. 权限系统 | ✅ 已完成 | 2026-03-02 | [报告](PHASE2_TASK2_COMPLETION_REPORT.md) |
| 3. 依赖隔离增强 | 🔴 待开始 | - | [任务清单](PHASE2_TASKS.md#任务-3-依赖隔离增强) |
| 4. 插件包管理器 | 🔴 待开始 | - | [任务清单](PHASE2_TASKS.md#任务-4-插件包管理器) |
| 5. 单元测试 | 🟡 进行中 | - | [任务清单](PHASE2_TASKS.md#任务-5-单元测试) |
| 6. 插件市场 UI | 🔴 待开始 | - | [任务清单](PHASE2_TASKS.md#任务-6-插件市场-ui) |

**完成度**: 2/6 任务 (33%)

---

## 🚀 快速开始

### 构建项目

```bash
# 还原依赖
dotnet restore Pulsar/Pulsar/Pulsar.csproj

# 构建 (Debug)
dotnet build Pulsar/Pulsar/Pulsar.csproj

# 构建 (Release)
dotnet build Pulsar/Pulsar/Pulsar.csproj -c Release

# 运行应用
dotnet run --project Pulsar/Pulsar/Pulsar.csproj
```

### 运行测试

```bash
# 运行所有测试
dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj

# 运行特定测试
dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj --filter "FullyQualifiedName~Security"
```

---

## 📚 核心文档

### 开发指南
- [AGENTS.md](../AGENTS.md) - AI Agent 操作指南（必读）
- [CONTRIBUTING.md](CONTRIBUTING.md) - 贡献指南
- [PLUGIN_QUICKSTART.md](PLUGIN_QUICKSTART.md) - 插件开发快速入门

### 架构文档
- [PLUGIN_SYSTEM_MODERNIZATION_PHASE1.md](PLUGIN_SYSTEM_MODERNIZATION_PHASE1.md) - Phase 1 架构设计
- [PHASE2_TASKS.md](PHASE2_TASKS.md) - Phase 2 任务清单

### 插件文档
- [BasicCommand.md](Plugins/BasicCommand.md) - 基础命令插件
- [WinSwitcher.md](Plugins/WinSwitcher.md) - 窗口切换插件
- [PkiPlugin.md](Plugins/PkiPlugin.md) - PKI 凭据管理插件
- [VbaRunner.md](Plugins/VbaRunner.md) - VBA 脚本运行器
- [BookmarkletRunner.md](Plugins/BookmarkletRunner.md) - 书签小程序运行器
- [SystemCommand.md](Plugins/SystemCommand.md) - 系统命令插件

### UI 指南
- [UI_BEST_PRACTICES.md](guides/UI_BEST_PRACTICES.md) - UI 最佳实践
- [COMPONENT_LIBRARY.md](guides/COMPONENT_LIBRARY.md) - 组件库

---

## 🔧 最新功能

### Phase 2 Task 2: 权限系统 (2026-03-02)

**实现内容**:
- ✅ 24 种权限类型定义
- ✅ 5 个预定义权限集（Basic/Standard/Advanced/Full/System）
- ✅ 权限拦截器（注册、授予、撤销、检查）
- ✅ PulsarContext 权限集成
- ✅ 权限请求 UI（对话框 + ViewModel）
- ✅ 35 个单元测试（100% 通过）

**核心文件**:
```
Core/Plugin/Security/
├── PluginPermission.cs           # 权限定义
└── PermissionInterceptor.cs      # 权限拦截器

ViewModels/Dialogs/
└── PermissionRequestViewModel.cs # 权限请求 ViewModel

Views/Dialogs/Contents/
└── PermissionRequestContent.xaml # 权限请求 UI
```

**使用示例**:
```csharp
// 自动权限检查
var clipboardText = await context.GetClipboardTextAsync();
// 如果没有 ReadClipboard 权限，抛出 UnauthorizedAccessException

// 手动请求权限
var granted = await permissionInterceptor.RequestPermissionAsync(
    pluginId: "my.plugin",
    permission: PluginPermission.AccessNetwork,
    reason: "需要访问网络以下载更新"
);
```

**详细报告**: [PHASE2_TASK2_COMPLETION_REPORT.md](PHASE2_TASK2_COMPLETION_REPORT.md)

---

## 🏗️ 项目结构

```
Pulsar/
├── Pulsar/                      # 主项目
│   ├── Core/                    # 核心接口和基类
│   │   └── Plugin/              # 插件系统核心
│   │       ├── Security/        # 权限系统 (NEW)
│   │       ├── Versioning/      # 版本管理
│   │       └── Metadata/        # 元数据
│   ├── Plugins/                 # 内置插件
│   │   ├── Core/                # 核心插件（不可禁用）
│   │   │   └── Pki/             # PKI 凭据管理
│   │   ├── WinSwitcher/         # 窗口切换
│   │   ├── BasicCommand/        # 基础命令
│   │   ├── VbaRunner/           # VBA 运行器
│   │   └── BookmarkletRunner/   # 书签小程序
│   ├── Services/                # 业务逻辑服务
│   │   ├── PluginRegistryV2.cs  # 插件注册中心 (v2)
│   │   └── ...
│   ├── ViewModels/              # MVVM ViewModels
│   ├── Views/                   # XAML 视图
│   └── Models/                  # 数据模型
├── Pulsar.Tests/                # 单元测试
│   └── Plugin/
│       ├── Security/            # 权限系统测试 (NEW)
│       └── ...
└── Docs/                        # 文档
    ├── archive/                 # 归档文档
    ├── guides/                  # 开发指南
    └── Plugins/                 # 插件文档
```

---

## 🧪 测试状态

### 最新测试结果 (2026-03-02)

```
✅ 权限系统测试: 35/35 passed (48ms)
✅ 热重载测试: 12/12 passed
✅ 构建状态: Success (0 errors, 2 warnings)
```

### 测试覆盖

- **权限系统**: 100% (35 tests)
- **热重载系统**: 100% (12 tests)
- **整体覆盖率**: 进行中

---

## 🔐 权限系统使用指南

### 权限层级

| 权限集 | 适用场景 | 包含权限 |
|--------|---------|---------|
| **Basic** | 简单插件 | 读取窗口信息、显示通知 |
| **Standard** | 大多数插件 | Basic + 剪贴板、键盘模拟 |
| **Advanced** | 系统集成插件 | Standard + 进程启动、文件系统 |
| **Full** | 受信任插件 | Advanced + 网络、凭据管理 |
| **System** | 核心插件 | Full + 热键注册、绕过检查 |

### 插件自动授权

- **Core 插件** (`Plugins/Core/`): 自动获得 `System` 权限
- **Extension 插件** (`Plugins/`): 自动获得 `Standard` 权限

### 权限检查

所有敏感操作都会自动检查权限：

```csharp
// PulsarContext 中的敏感操作
await context.GetClipboardTextAsync();        // 需要 ReadClipboard
await context.GetTargetProcessWindowsAsync(); // 需要 ReadProcessWindows
await context.GetSelectedTextAsync();         // 需要 ReadSelectedText
```

---

## 📝 开发规范

### 代码风格
- 遵循 `AGENTS.md` 中的代码规范
- 使用 Allman 风格大括号
- 4 空格缩进（不使用 Tab）
- 启用 Nullable Reference Types

### 命名约定
- 类/结构/枚举: `PascalCase`
- 接口: `IPascalCase`
- 方法: `PascalCase`
- 异步方法: `MethodNameAsync`
- 私有字段: `_camelCase`
- 参数/局部变量: `camelCase`

### Git 提交规范
```
feat: 添加新功能
fix: 修复 bug
docs: 更新文档
test: 添加测试
refactor: 重构代码
style: 代码格式调整
chore: 构建/工具变更
```

---

## 🐛 已知问题

### 非阻塞问题
1. **HotReloadManager.PluginReloaded 事件未使用** (Warning CS0067)
   - 影响: 仅编译警告，不影响功能
   - 计划: Phase 2 Task 3 中实现事件触发

2. **PluginVersionResolver NuGet 引用错误** (LSP Errors)
   - 影响: 仅 LSP 错误，构建成功
   - 原因: NuGet 包未完全集成
   - 计划: Phase 2 Task 3 中修复

---

## 🚧 下一步工作

### 优先级 P0 (必须完成)

**Task 3: 依赖隔离增强** (预计 4 天)
- [ ] 实现 Shim Assembly 生成
- [ ] 实现 NuGet 包解析
- [ ] 实现依赖冲突检测
- [ ] 集成到 PluginLoader

**关键文件**:
```
Core/Plugin/DependencyShim.cs
Core/Plugin/NuGetPackageResolver.cs
Core/Plugin/DependencyConflictDetector.cs
```

**验收标准**:
- 不同插件可以使用不同版本的依赖
- NuGet 包自动下载
- 依赖冲突自动解决

---

## 📞 支持与联系

### 文档资源
- **快速开始**: [PLUGIN_QUICKSTART.md](PLUGIN_QUICKSTART.md)
- **任务清单**: [PHASE2_TASKS.md](PHASE2_TASKS.md)
- **AI Agent 指南**: [AGENTS.md](../AGENTS.md)

### Git 历史
- **Phase 1 完成**: Commit `71b47dd`
- **Phase 2 Task 1 完成**: Commit `待提交`
- **Phase 2 Task 2 完成**: Commit `待提交`

### 归档文档
- **Phase 2 Task 1**: [archive/2026-03/phase2-task1/](archive/2026-03/phase2-task1/)
- **旧版重构**: [archive/2026-03/](archive/2026-03/)
- **PKI 实现**: [archive/2026-01/](archive/2026-01/)

---

## 📊 项目统计

### 代码量 (截至 2026-03-02)
- **主项目**: ~15,000 行 C#
- **测试项目**: ~1,500 行 C#
- **文档**: ~5,000 行 Markdown

### 最近更新
- **Phase 2 Task 2**: +1,415 行代码
- **测试覆盖**: +35 个测试
- **新增文件**: 8 个

---

*最后更新: 2026-03-02*  
*维护者: OpenCode*
