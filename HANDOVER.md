# 🔄 Pulsar 项目交接文档

## 📅 交接信息

- **交接日期**: 2026年3月2日
- **当前版本**: v4.1.0
- **项目状态**: External Plugins 重构完成
- **构建状态**: ✅ 成功 (4 个预期警告, 0 错误)

---

## 🎯 最新完成工作 (Phase 2 Task 6)

### External Plugins 页面重构

**问题背景**:
- 原 Marketplace 页面展示空的"可用插件"列表（在线仓库未实现）
- 用户体验不诚实，功能与实际能力不符
- 代码复杂度高（搜索、过滤、统计等无用功能）

**解决方案**:
1. ✅ 创建新的 `SettingsExternalPluginsPage` - 简化的 MVP 布局
2. ✅ 实现 `LocalPluginScanner` - 扫描已安装的外部插件
3. ✅ 实现 `ExternalPluginManagerViewModel` - 简化的插件管理器
4. ✅ 重构 `PluginPackageManager` - 移除在线仓库依赖，只保留本地文件安装
5. ✅ 修复 XAML 图标错误 (`Puzzle24` → `Apps24`)
6. ✅ 清理编译警告和错误

**架构改进**:
```
职责分离:
├── Plugins 页面 → 管理运行时插件状态（启用/禁用/配置/监控）
│   └── 数据源: PluginRegistry
└── External Plugins 页面 → 管理外部插件安装/卸载
    └── 数据源: LocalPluginScanner + PluginPackageManager
```

**用户体验流程**:
1. 用户在 "External Plugins" 页面点击 "Install from File"
2. 选择 ZIP 文件 → `PluginPackageManager.InstallFromFileAsync()`
3. 解压到 `%AppData%\Pulsar\Plugins\{PluginId}\`
4. 提示用户重启 Pulsar
5. 重启后，`PluginLoader` 加载外部插件
6. 插件出现在 "Plugins" 页面（可启用/禁用/配置）

**代码变更统计**:
- 新增文件: 4 个 (页面、ViewModel、服务)
- 修改文件: 5 个 (服务注册、路由、重构)
- 删除代码: ~400 行（移除在线仓库依赖）
- 代码精简: 40% 的无用 UI 代码

---

## 📚 核心文档索引

### 开发指南
- **[AGENTS.md](./AGENTS.md)** - AI Agent 操作指南（构建、测试、代码规范）
- **[ARCHITECTURE.md](./ARCHITECTURE.md)** - 系统架构文档
- **[PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md)** - 插件开发指南

### 用户指南
- **[Docs/README.md](./Docs/README.md)** - 文档索引
- **[Docs/PLUGIN_QUICKSTART.md](./Docs/PLUGIN_QUICKSTART.md)** - 插件快速入门

### 组件库
- **[Docs/guides/COMPONENT_LIBRARY.md](./Docs/guides/COMPONENT_LIBRARY.md)** - 可复用 UI 组件库
- **[Docs/guides/UI_BEST_PRACTICES.md](./Docs/guides/UI_BEST_PRACTICES.md)** - UI 最佳实践

### 历史归档
- **[Docs/archive/](./Docs/archive/)** - 历史交接文档和完成报告

---

## 🏗️ 项目架构概览

### 插件系统 (v4.0.0+)

**核心组件**:
- `IPulsarPlugin` - 插件接口标准
- `PulsarContext` - 延迟加载的上下文快照
- `PluginRegistry` - 插件生命周期管理 + Circuit Breaker
- `PluginLoader` - 插件加载器（内置 + 外部 DLL）
- `PluginPackageManager` - 本地文件安装器

**插件分层**:
- **Core Plugins** (`Plugins/Core/`) - 基础设施插件，始终加载，不可禁用
  - PKI - 凭证管理
  - Hotkey - 热键管理
- **Extension Plugins** (`Plugins/`) - 可选功能插件，可动态加载/卸载
  - WinSwitcher - 窗口切换
  - VbaRunner - VBA 脚本执行
  - BookmarkletRunner - 书签小程序执行

**安全机制**:
- Circuit Breaker - 插件崩溃 3 次自动禁用 60 秒
- 插件隔离 - 异常不会导致主程序崩溃
- 异步优先 - 所有插件操作都是 `async Task`

### UI 架构

**Multi-Headed UI**:
- Radial Menu - 轻量级自定义主题（透明背景）
- Settings/Dialogs - WPF-UI 主题 + Mica 背景
- 主题隔离 - 每个窗口手动注入主题

**对话框系统** (v4.1.0+):
- `DialogService` - 统一对话框管理
- `DialogHostWindow` - FluentWindow 容器
- 内置对话框: Input, ColorPicker, Message, Confirmation
- 自动功能: 主题继承、智能定位、尺寸预设

---

## 🚀 快速开始

### 构建项目
```bash
# 还原依赖
dotnet restore Pulsar/Pulsar/Pulsar.csproj

# 构建 (Debug)
dotnet build Pulsar/Pulsar/Pulsar.csproj

# 运行
dotnet run --project Pulsar/Pulsar/Pulsar.csproj
```

### 开发新插件
```bash
# 1. 复制插件模板
cp -r PluginTemplate Pulsar/Pulsar/Plugins/MyPlugin

# 2. 实现 IPulsarPlugin 接口
# 3. 在 App.xaml.cs 注册插件
# 4. 构建并测试

# 详细指南: PLUGIN_DEVELOPMENT.md
```

---

## ⚠️ 已知问题

### 预期警告
- `CS0618` - `PluginRepository` 已过时（向后兼容保留）
- `CS0067` - `HotReloadManager.PluginReloaded` 事件未使用（预留功能）

### 待实现功能
- [ ] 在线插件市场（需要后端支持）
- [ ] 插件热重载（HotReloadManager 已实现基础设施）
- [ ] 插件依赖自动解析

---

## 📞 支持与贡献

- **问题反馈**: GitHub Issues
- **贡献指南**: [Docs/CONTRIBUTING.md](./Docs/CONTRIBUTING.md)
- **开发规范**: [AGENTS.md](./AGENTS.md)

---

## 📝 变更日志

### v4.1.0 (2026-03-02)
- ✅ External Plugins 页面重构
- ✅ PluginPackageManager 简化（移除在线仓库依赖）
- ✅ LocalPluginScanner 实现
- ✅ 修复 XAML 图标错误

### v4.0.0 (2026-03-02)
- ✅ 插件系统现代化 Phase 1
- ✅ Circuit Breaker 保护机制
- ✅ PulsarContext 延迟加载
- ✅ 统一对话框系统

### v2.4.0 (2026-03-02)
- ✅ PKI 插件实现
- ✅ 配置系统迁移到 Profiles.json

---

**最后更新**: 2026年3月2日  
**维护者**: Pulsar Development Team
