<picture>
  <source media="(prefers-color-scheme: dark)" srcset="Pulsar/Pulsar/Assets/Brand/wordmark.png">
  <img alt="Pulsar" src="Pulsar/Pulsar/Assets/Brand/wordmark.png">
</picture>

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-0078D6)](https://www.microsoft.com/windows)

> [English](README.md) | **简体中文**

**Pulsar** — 一款高性能 Windows 生产力启动器，采用热键唤起的径向菜单界面。专为肌肉记忆打造——告别传统的 Alt-Tab 线性切换，用空间定位来导航。

---

<p align="center">
  <img src="Pulsar/Pulsar/Assets/Brand/demo.gif" alt="Pulsar 演示" width="600">
</p>

## 主要功能

| 功能 | 描述 |
|------|------|
| **径向菜单** | 热键唤起的圆形启动器。两种模式：命令模式 (`Ctrl+Q`) 显示上下文操作，切换模式 (`Ctrl+Shift+Q`) 带 MRU 中心窗口用于应用切换 |
| **可扩展插件系统** | 双层架构：核心插件（基础设施，必须）和扩展插件（可选，断路器保护） |
| **PKI / 秘密管理** | 使用 DPAPI 加密安全存储凭据，通过 UI 自动化注入，支持自动提交和可配置延迟 |
| **全局热键** | 系统级热键绑定，默认 `Ctrl+Alt+P` |
| **应用与窗口切换** | 智能窗口切换（含发现黑名单），未运行时自动启动应用 |
| **插件模拟器** | 无头模式执行插件，输出结构化 JSON，无需 WPF 界面即可进行 AI 驱动的测试 |
| **插件扩展** | 命令启动器（应用/文件/文件夹/URL/按键序列）、VBA 脚本执行器（Excel/WPS）、书签执行器（浏览器 JS） |
| **本地化** | 简体中文 + 英文，插件元数据基于约定的自动键查找 |

## 环境要求

- Windows 10 或更高版本（x64）
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（如需编译需安装 SDK）

## 快速开始

```bash
# 还原依赖 & 编译
dotnet restore Pulsar/Pulsar/Pulsar.csproj
dotnet build Pulsar/Pulsar/Pulsar.csproj

# 运行（默认热键：Ctrl+Alt+P）
dotnet run --project Pulsar/Pulsar/Pulsar.csproj

# 运行测试
dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj

# 无头插件模拟
dotnet run --project Pulsar/Pulsar.Simulator -- --plugin "com.pulsar.winswitcher" --action "activate" --args "{\"app\":\"chrome\"}"

# 发布自包含版本
dotnet publish Pulsar/Pulsar/Pulsar.csproj -c Release -o publish -p:RuntimeIdentifier=win-x64 -p:SelfContained=true -p:PublishSingleFile=true -p:PublishReadyToRun=true
```

## 内置插件

### 核心插件（始终加载，崩溃即致命）

| 插件 | ID | 描述 |
|------|----|------|
| **秘密填充 (PKI)** | `com.pulsar.pki` | DPAPI 加密凭据库。通过 UI 自动化向任意窗口注入用户名/密码，支持延迟和自动提交 |
| **应用切换器** | `com.pulsar.winswitcher` | 智能窗口切换（模糊搜索），未运行时自动启动，支持发现黑名单 |
| **Pulsar 控制** | `com.pulsar.system` | 打开设置、快速添加上下文应用、系统命令 |

### 扩展插件（断路器保护 — 3 次崩溃/分钟 = 禁用 60 秒）

| 插件 | ID | 描述 |
|------|----|------|
| **命令启动器** | `com.pulsar.command` | 启动应用/文件/文件夹/URL，向前台窗口发送按键序列 |
| **VBA 脚本执行器** | `com.pulsar.vbarunner` | 在 Excel/WPS 中执行 VBA 宏，支持智能指令 |
| **书签执行器** | `com.pulsar.bookmarklet` | 通过 UI 自动化在当前浏览器中执行 JavaScript 书签脚本 |

## 架构

```
Pulsar/
├── Core/                      # 接口、基础类型、插件系统核心
│   ├── Plugin/                #   IPulsarPlugin, PluginBase<T>, PulsarContext, PluginResult
│   │   └── Metadata/          #   IPluginMetadataProvider, PluginMetadata, ConfigSchema
│   ├── Localization/          #   ILocalizationService (resx: EN + zh-CN)
│   ├── Focus/                 #   焦点管理抽象
│   ├── Converters/            #   WPF 值转换器
│   └── Messages/              #   CommunityToolkit.Mvvm 弱引用消息
│
├── Plugins/
│   ├── Core/                  #   核心插件（始终加载，无断路器）
│   └── Extensions/            #   扩展插件（断路器保护）
│
├── Services/                  # 业务逻辑层
│   ├── PluginRegistry.cs      #   插件生命周期 + 断路器（外观模式）
│   ├── ConfigService.cs       #   配置管理（Profiles.json）
│   ├── HotkeyService.cs       #   全局热键绑定
│   ├── ThemeService.cs        #   亮/暗主题注入
│   ├── DialogService.cs       #   统一对话框系统
│   ├── SlotLayoutEngine.cs    #   径向菜单布局计算
│   └── ... (40+ 服务)
│
├── ViewModels/                # MVVM ViewModel 层
│   ├── RadialMenuViewModel.cs #   主径向菜单状态
│   ├── SettingsViewModel.cs   #   设置编辑器（瞬态）
│   └── Dialogs/               #   对话框 ViewModel
│
├── Views/                     # XAML 视图
│   ├── RadialMenuWindow.xaml  #   主径向菜单窗口
│   ├── SettingsWindow.xaml    #   设置窗口
│   └── Dialogs/ Controls/    #   对话框内容、可复用控件
│
├── Models/                    # DTO 和配置模型
├── Helpers/                   # 静态工具类（IconHelper, RadialLayoutHelper 等）
├── Features/                  # 功能模块
│   └── Tutorial/              #   交互式入门引导系统
├── Styles/                    # 自定义 WPF 样式（Pulsar 按钮、插槽、滚动条）
├── Themes/                    # 主题 XAML（深色 + 浅色）
└── Resources/                 # 本地化资源 (.resx)
    ├── Strings.resx           # 英文（基础语言）
    └── Strings.zh-CN.resx     # 简体中文
```

## 核心设计理念

### PulsarContext — 不可变上下文快照
径向菜单唤起时，Pulsar 将系统状态冻结为不可变的 `PulsarContext`。重型属性（剪贴板、窗口列表）采用懒加载。每次执行的可变数据存储在 `PluginExecutionContext`（AsyncLocal 范围）中，而非上下文本体。

### 焦点回旋镖（Focus Boomerang）
执行输入注入的插件（如 PKI）遵循捕获 → 执行 → 隐藏 → 恢复焦点 → 延迟 → 注入的循环，可靠地将焦点返回到原始窗口。

### 扩展插件断路器
扩展插件由断路器保护：1 分钟内崩溃 3 次触发 60 秒禁用期，之后进入半开状态允许单次重试。用户通过 Windows 通知接收提醒。

### AI 优先开发
整个项目为 AI 智能体协作进行了优化：
- **无头模拟器**：无需 WPF 界面即可测试插件，解析结构化 JSON 输出
- **隔离副作用**：所有 OS 操作通过接口抽象（`IInputSimulator`、`IProcessLauncher` 等），可用 Moq 进行单元测试
- **全面测试套件**：330+ 个 xUnit 测试覆盖 ViewModel、服务和插件逻辑
- **自纠错循环**：模拟器 → 解析错误 → 修复代码 → 重新运行直到通过

## 文档

| 资源 | 描述 |
|------|------|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | 系统架构深入解析 |
| [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md) | 插件开发指南 |
| [Docs/](./Docs/) | 完整文档索引 |
| [Docs/lessons/](./Docs/lessons/) | WPF 坑点与已知问题归档 |
| [Docs/architecture/](./Docs/architecture/) | 架构细节（插件系统、对话框系统等） |
| [Docs/ops/BUILD_AND_RUN.md](./Docs/ops/BUILD_AND_RUN.md) | 构建与运行参考 |

## 截图

<!-- TODO: 添加截图 -->
| 径向菜单 | 设置界面 | 插件编辑 |
|---------|---------|---------|
| `[截图_径向菜单]` | `[截图_设置界面]` | `[截图_插件编辑]` |

## 视频演示

<!-- TODO: 添加视频链接 -->
`[视频演示链接]`

## 项目状态

Pulsar 正在活跃开发中。架构、插件 API 和核心功能已趋于稳定。扩展插件生态正在持续增长。

## 许可证

MIT — 详见 [LICENSE](./LICENSE)。
