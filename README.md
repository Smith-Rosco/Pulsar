<picture>
  <source media="(prefers-color-scheme: dark)" srcset="Pulsar/Pulsar/Assets/Brand/wordmark.png">
  <img alt="Pulsar" src="Pulsar/Pulsar/Assets/Brand/wordmark.png">
</picture>

> **English** | [简体中文](README.zh-CN.md)

**Pulsar** — a high-performance productivity launcher for Windows featuring a hotkey-invoked radial menu interface. Built for muscle memory: abandon traditional Alt-Tab linear traversal and navigate with spatial positioning.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows-0078D6)](https://www.microsoft.com/windows)
[![CI](https://github.com/anomalyco/Pulsar/actions/workflows/ci.yml/badge.svg)](.github/workflows/ci.yml)

---

<p align="center">
  <img src="Pulsar/Pulsar/Assets/Brand/demo.gif" alt="Pulsar Demo" width="600">
</p>

## Key Features

| Feature | Description |
|---------|-------------|
| **Radial Menu** | Hotkey-invoked circular launcher. Two modes: Command mode (`Ctrl+Q`) for contextual actions, Switch mode (`Ctrl+Shift+Q`) with MRU center window for app switching |
| **Extensible Plugin System** | Two-tier architecture: Core plugins (essential infrastructure) and Extension plugins (optional, Circuit Breaker protected) |
| **PKI / Secret Management** | Securely store credentials with DPAPI encryption. Inject via UI Automation with auto-submit and configurable delay |
| **Global Hotkeys** | System-wide bindings for instant access — default `Ctrl+Alt+P` |
| **App & Window Switching** | Smart window switching with discovery blacklist, launch apps if not running |
| **Plugin Simulator** | Headless plugin execution + structured JSON output for AI-driven testing without the WPF shell |
| **Plugin System Extensions** | Command Runner (apps/files/folders/URLs/keystrokes), VBA Script Runner (Excel/WPS), Bookmarklet Runner (browser JS) |
| **Localization** | English + Simplified Chinese, convention-based lookup for plugin metadata |

## Prerequisites

- Windows 10 or later (x64)
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (SDK required for building)

## Quick Start

```bash
# Restore & Build
dotnet restore Pulsar/Pulsar/Pulsar.csproj
dotnet build Pulsar/Pulsar/Pulsar.csproj

# Run (default hotkey: Ctrl+Alt+P)
dotnet run --project Pulsar/Pulsar/Pulsar.csproj

# Run tests
dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj

# Headless plugin simulation
dotnet run --project Pulsar/Pulsar.Simulator -- --plugin "com.pulsar.winswitcher" --action "activate" --args "{\"app\":\"chrome\"}"

# Publish self-contained release
dotnet publish Pulsar/Pulsar/Pulsar.csproj -c Release -o publish -p:RuntimeIdentifier=win-x64 -p:SelfContained=true -p:PublishSingleFile=true -p:PublishReadyToRun=true
```

## Built-in Plugins

### Core Plugins (always loaded, crashes are fatal)

| Plugin | ID | Description |
|--------|----|-------------|
| **Secret Fill (PKI)** | `com.pulsar.pki` | DPAPI-encrypted credential vault. Inject username/password into any window via UI Automation with configurable delay and auto-submit |
| **App Switcher** | `com.pulsar.winswitcher` | Smart window switching with fuzzy search. Launch app if not running. Supports discovery blacklist |
| **Pulsar Control** | `com.pulsar.system` | Open settings, quick-add context apps, system commands |

### Extension Plugins (Circuit Breaker protected — 3 crashes/min = 60s disable)

| Plugin | ID | Description |
|--------|----|-------------|
| **Command Runner** | `com.pulsar.command` | Launch apps, files, folders, URLs; send keystroke sequences to foreground window |
| **VBA Script Runner** | `com.pulsar.vbarunner` | Execute VBA macros in Excel/WPS with smart directives |
| **Bookmarklet Runner** | `com.pulsar.bookmarklet` | Run JavaScript bookmarklets in the active browser via UI Automation |

## Architecture

```
Pulsar/
├── Core/                      # Interfaces, base types, plugin system core
│   ├── Plugin/                #   IPulsarPlugin, PluginBase<T>, PulsarContext, PluginResult
│   │   └── Metadata/          #   IPluginMetadataProvider, PluginMetadata, ConfigSchema
│   ├── Localization/          #   ILocalizationService (resx: EN + zh-CN)
│   ├── Focus/                 #   Focus management abstractions
│   ├── Converters/            #   WPF value converters
│   └── Messages/              #   CommunityToolkit.Mvvm weak-reference messages
│
├── Plugins/
│   ├── Core/                  #   Core plugins (always loaded, no circuit breaker)
│   └── Extensions/            #   Extension plugins (circuit breaker protected)
│
├── Services/                  # Business logic layer
│   ├── PluginRegistry.cs      #   Plugin lifecycle + circuit breaker (Facade pattern)
│   ├── ConfigService.cs       #   Configuration management (Profiles.json)
│   ├── HotkeyService.cs       #   Global hotkey bindings
│   ├── ThemeService.cs        #   Light/Dark theme injection
│   ├── DialogService.cs       #   Unified dialog system
│   ├── SlotLayoutEngine.cs    #   Radial menu layout computation
│   └── ... (40+ services)
│
├── ViewModels/                # MVVM ViewModels
│   ├── RadialMenuViewModel.cs #   Main radial menu state
│   ├── SettingsViewModel.cs   #   Settings editor (transient)
│   └── Dialogs/               #   Dialog ViewModels
│
├── Views/                     # XAML views
│   ├── RadialMenuWindow.xaml  #   Main radial menu window
│   ├── SettingsWindow.xaml    #   Settings window
│   └── Dialogs/ Controls/    #   Dialog contents, reusable controls
│
├── Models/                    # DTOs and configuration models
├── Helpers/                   # Static utilities (IconHelper, RadialLayoutHelper, etc.)
├── Features/                  # Feature modules
│   └── Tutorial/              #   Interactive onboarding system
├── Styles/                    # Custom WPF styles (Pulsar buttons, slots, scrollbars)
├── Themes/                    # Theme.XAML (Dark + Light)
└── Resources/                 # Localization (.resx files)
    ├── Strings.resx           # English (base)
    └── Strings.zh-CN.resx     # Simplified Chinese
```

## Key Design Concepts

### PulsarContext — Immutable Context Snapshot
When the radial menu is invoked, Pulsar freezes the system state into an immutable `PulsarContext` — eliminating race conditions. Heavy properties (clipboard, window list) are lazy-loaded. Per-execution mutable data lives in `PluginExecutionContext` (AsyncLocal scope), not on the context itself.

### Focus Boomerang
Plugins that inject input (e.g., PKI) operate on a capture → execute → hide → restore → delay → inject cycle, reliably returning focus to the original window.

### Circuit Breaker for Extensions
Extension plugins are wrapped in a Circuit Breaker: 3 crashes within 1 minute triggers a 60-second disable period, after which the plugin enters half-open state for a single retry. Users are notified via Windows toast notifications.

### AI-First Development
The entire project is optimized for AI-agent collaboration:
- **Headless Simulator**: Test plugins without the WPF shell, parse structured JSON output
- **Isolated Side-Effects**: All OS coupling behind interfaces (`IInputSimulator`, `IProcessLauncher`, etc.) — mockable with Moq
- **Comprehensive test suite**: 330+ xUnit tests covering ViewModels, services, and plugin logic
- **Self-Correction loop**: Simulator → parse errors → fix code → re-run until green

## Documentation

| Resource | Description |
|----------|-------------|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | System architecture deep-dive |
| [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md) | Plugin development guide |
| [AGENTS.md](./AGENTS.md) | AI-assisted development conventions |
| [Docs/](./Docs/) | Full documentation index |
| [Docs/lessons/](./Docs/lessons/) | WPF pitfalls & known issues archive |
| [Docs/architecture/](./Docs/architecture/) | Architecture details (Plugin System, Dialog System, etc.) |
| [Docs/ops/BUILD_AND_RUN.md](./Docs/ops/BUILD_AND_RUN.md) | Build & run reference |

## Screenshots

<!-- TODO: Add screenshots -->
| Radial Menu | Settings | Plugin Editor |
|-------------|----------|---------------|
| `[Screenshot_RadialMenu]` | `[Screenshot_Settings]` | `[Screenshot_PluginEditor]` |

## Video Demo

<!-- TODO: Add video link -->
`[Video_Demo_Link]`

## Project Status

Pulsar is in active development. The architecture, plugin API, and core features are stable. Extension plugin ecosystem is growing.

## License

MIT — see [LICENSE](./LICENSE).
