<picture>
  <source media="(prefers-color-scheme: dark)" srcset="Pulsar/Pulsar/Assets/Brand/wordmark.png">
  <img alt="Pulsar" src="Pulsar/Pulsar/Assets/Brand/wordmark.png">
</picture>

<p align="center">
  <b>Pulsar</b> — A high-performance productivity launcher for Windows featuring a hotkey-invoked radial menu interface.
  <br>
  <b>Pulsar</b> — 一款高性能 Windows 效率启动器，通过快捷键呼出径向菜单，用空间定位取代传统的 Alt-Tab 线性切换。
</p>

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-0078D6)](https://www.microsoft.com/windows)

---

<p align="center">
  <img src="Pulsar/Pulsar/Assets/Brand/demo.gif" alt="Pulsar Demo" width="500">
</p>

---

## English

## Features

- **Radial Menu** — Hotkey-invoked circular launcher with spatial positioning for blind operation
- **Extensible Plugin System** — Core plugins (essential infrastructure) and extension plugins (optional, circuit-breaker protected)
- **PKI / Secret Management** — Securely store and inject credentials with DPAPI encryption and UI Automation
- **Global Hotkeys** — System-wide hotkey bindings for instant access
- **Plugin Simulator** — Headless testing without the WPF shell

## Prerequisites

- Windows 10 or later (x64)
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (or SDK for building)

## Quick Start

```bash
# Build
dotnet build Pulsar/Pulsar/Pulsar.csproj

# Run (default hotkey: Ctrl+Alt+P)
dotnet run --project Pulsar/Pulsar/Pulsar.csproj

# Run tests
dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj

# Plugin simulation (headless)
dotnet run --project Pulsar/Pulsar.Simulator -- --plugin "com.your.plugin" --args "{...}"
```

## Architecture

| Layer | Description |
|-------|-------------|
| `Core/` | Interfaces, base types, plugin system core |
| `Plugins/Core/` | Essential infrastructure plugins (PKI, Hotkey) |
| `Plugins/` | Extension plugins (WinSwitcher, VbaRunner, etc.) |
| `Services/` | Business logic (PluginRegistry, ConfigService, etc.) |
| `ViewModels/` | MVVM ViewModels (CommunityToolkit.Mvvm) |
| `Views/` | XAML Windows, Controls, UserControls |
| `Helpers/` | Static utilities and extensions |
| `Models/` | DTOs and configuration models |

## Plugin System

Pulsar has a two-tier plugin architecture:

- **Core Plugins** — Essential, cannot be disabled, crashes are fatal
- **Extension Plugins** — Optional, Circuit Breaker protected (3 crashes in 1 min = 60s disable)

> See [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md) for the developer guide and [Docs/architecture/PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md) for architecture details.

## Documentation

| Resource | Description |
|----------|-------------|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | System architecture overview |
| [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md) | Plugin development guide |
| [AGENTS.md](./AGENTS.md) | AI-assisted development conventions |
| [Docs/](./Docs/) | Full documentation index |

## License

MIT — see [LICENSE](./LICENSE).

---

## 中文

## 功能特性

- **径向菜单** — 快捷键呼出圆形启动器，空间定位支持盲操作
- **可扩展插件系统** — 核心插件（基础架构，不可禁用）和扩展插件（可选，断路器保护）
- **PKI / 密码管理** — 通过 DPAPI 加密和 UI Automation 安全存储和注入凭据
- **全局热键** — 系统级热键绑定，即时访问
- **插件模拟器** — 无需 WPF 界面的无头测试

## 环境要求

- Windows 10 或更高版本（x64）
- [.NET 8.0 运行时](https://dotnet.microsoft.com/download/dotnet/8.0)（如需编译则需要 SDK）

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

| 层 | 说明 |
|-------|------|
| `Core/` | 接口、基类、插件系统核心 |
| `Plugins/Core/` | 基础插件（PKI、热键） |
| `Plugins/` | 扩展插件（WinSwitcher、VbaRunner 等） |
| `Services/` | 业务逻辑（PluginRegistry、ConfigService 等） |
| `ViewModels/` | MVVM 视图模型（CommunityToolkit.Mvvm） |
| `Views/` | XAML 窗口、控件、用户控件 |
| `Helpers/` | 静态工具和扩展方法 |
| `Models/` | DTO 和配置模型 |

## 插件系统

Pulsar 采用双层插件架构：

- **核心插件** — 基础功能，不可禁用，崩溃为致命错误
- **扩展插件** — 可选功能，断路器保护（1 分钟内崩溃 3 次 = 禁用 60 秒）

> 开发者指南见 [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md)，架构详情见 [Docs/architecture/PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md)。

## 文档

| 资源 | 说明 |
|------|------|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | 系统架构概览 |
| [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md) | 插件开发指南 |
| [AGENTS.md](./AGENTS.md) | AI 辅助开发规范 |
| [Docs/](./Docs/) | 完整文档索引 |

## 许可证

MIT — 详见 [LICENSE](./LICENSE)。
