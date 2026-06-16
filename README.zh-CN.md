<picture>
  <source media="(prefers-color-scheme: dark)" srcset="Pulsar/Pulsar/Assets/Brand/wordmark.png">
  <img alt="Pulsar" src="Pulsar/Pulsar/Assets/Brand/wordmark.png">
</picture>

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-0078D6)](https://www.microsoft.com/windows)

> [English](README.md) | **简体中文**

---

**Pulsar** 是一款高性能 Windows 生产力启动器，采用热键唤起的径向菜单界面。专为肌肉记忆打造——告别传统的 Alt-Tab 线性切换，用空间定位来导航。

<p align="center">
  <img src="Pulsar/Pulsar/Assets/Brand/demo.gif" alt="Pulsar 演示" width="500">
</p>

## 功能特性

- **径向菜单** — 热键唤起的圆形启动器，空间定位，支持盲操
- **可扩展插件系统** — 核心插件（基础设施，必须）和扩展插件（可选，断路器保护）
- **PKI / 秘密管理** — 使用 DPAPI 加密和 UI 自动化安全存储和注入凭据
- **全局热键** — 系统级热键绑定，即时访问
- **插件模拟器** — 无头测试，无需 WPF 界面

## 环境要求

- Windows 10 或更高版本（x64）
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（如需编译需安装 SDK）

## 快速开始

```bash
# 编译
dotnet build Pulsar/Pulsar/Pulsar.csproj

# 运行（默认热键：Ctrl+Alt+P）
dotnet run --project Pulsar/Pulsar/Pulsar.csproj

# 运行测试
dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj

# 插件模拟（无头模式）
dotnet run --project Pulsar/Pulsar.Simulator -- --plugin "com.your.plugin" --args "{...}"
```

## 架构

| 层 | 描述 |
|-------|-------------|
| `Core/` | 接口、基础类型、插件系统核心 |
| `Plugins/Core/` | 基础设施插件（PKI、热键） |
| `Plugins/` | 扩展插件（WinSwitcher、VbaRunner 等） |
| `Services/` | 业务逻辑（PluginRegistry、ConfigService 等） |
| `ViewModels/` | MVVM ViewModels（CommunityToolkit.Mvvm） |
| `Views/` | XAML 窗口、控件、UserControl |
| `Helpers/` | 静态工具类和扩展方法 |
| `Models/` | DTO 和配置模型 |

## 插件系统

Pulsar 采用双层插件架构：

- **核心插件** — 必需，不可禁用，崩溃即致命
- **扩展插件** — 可选，断路器保护（1 分钟内崩溃 3 次 = 禁用 60 秒）

> 详见 [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md) 开发者指南和 [Docs/architecture/PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md) 架构文档。

## 文档

| 资源 | 描述 |
|----------|-------------|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | 系统架构概览 |
| [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md) | 插件开发指南 |
| [Docs/](./Docs/) | 完整文档索引 |

## 许可证

MIT — 详见 [LICENSE](./LICENSE)。
