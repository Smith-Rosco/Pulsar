<picture>
  <source media="(prefers-color-scheme: dark)" srcset="Pulsar/Pulsar/Assets/Brand/wordmark.png">
  <img alt="Pulsar" src="Pulsar/Pulsar/Assets/Brand/wordmark.png">
</picture>

**Pulsar** is a high-performance productivity launcher for Windows featuring a hotkey-invoked radial menu interface. Built for muscle memory — abandon traditional Alt-Tab linear traversal and navigate with spatial positioning.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-0078D6)](https://www.microsoft.com/windows)

---

## Features

- **Radial Menu** — Hotkey-invoked circular launcher with spatial positioning for blind operation
- **Extensible Plugin System** — Core plugins (essential infrastructure) and extension plugins (optional, circuit-breaker protected)
- **PKI / Secret Management** — Securely store and inject credentials with DPAPI encryption and UI Automation
- **Global Hotkeys** — System-wide hotkey bindings for instant access
- **Plugin Simulator** — Headless testing without the WPF shell

## Quick Start

```bash
# Build
dotnet build Pulsar/Pulsar/Pulsar.csproj

# Run
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
