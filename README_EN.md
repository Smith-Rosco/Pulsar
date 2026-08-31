<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="Pulsar/Pulsar/Assets/Brand/wordmark.png">
  <img alt="Pulsar" src="Pulsar/Pulsar/Assets/Brand/wordmark.png">
</picture>

# Pulsar

### A high-performance productivity launcher for Windows featuring a hotkey-invoked radial menu
**高性能 Windows 生产力启动器 · 热键唤起的径向菜单**

[![Release Version](https://img.shields.io/badge/Release-v1.8.0-2563EB.svg?style=flat-square&logo=github)](https://github.com/anomalyco/Pulsar/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D4.svg?style=flat-square&logo=windows)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4.svg?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-10B981.svg?style=flat-square)](LICENSE)
[![CI](https://github.com/anomalyco/Pulsar/actions/workflows/ci.yml/badge.svg)](.github/workflows/ci.yml)
[![Tests](https://img.shields.io/badge/Tests-330%2B%20xUnit-success.svg?style=flat-square&logo=xunit)](Pulsar/Pulsar.Tests)
[![Language](https://img.shields.io/badge/Language-zh--CN%20%7C%20en-8B5CF6.svg?style=flat-square)](#-internationalization)

<br/>

**[简体中文](README.md)** • **[English](README_EN.md)**

<br/>

[🚀 Quick Start](#-quick-start) · [✨ Features](#-features) · [🎬 Video Demo](#-video-demo) · [📸 Screenshots](#-screenshots) · [🛠️ Local Build](#-local-build--development) · [📋 Changelog](CHANGELOG.md)

</div>

---

## 📖 Introduction

**Pulsar** is a high-performance productivity launcher for Windows featuring a hotkey-invoked radial menu interface.

Built for muscle memory: abandon traditional Alt-Tab linear traversal and navigate with spatial positioning. It supports plugin-based extensibility, PKI secret injection, global hotkeys, and smart app/window switching.

> 💡 **Design Highlights**:
> - **Spatial-first navigation**: radial menu + spatial memory for one-shot access to frequent actions, replacing linear Alt-Tab traversal;
> - **Layered plugin architecture**: Core plugins (infrastructure) vs Extension plugins (optional) — extensions protected by a Circuit Breaker;
> - **AI-friendly development**: headless plugin simulator + structured JSON output, 330+ xUnit tests, optimized for AI-agent collaboration.

---

## 🚀 Quick Start

### Download & Install

| Package | Use case | Description | Download |
| :--- | :--- | :--- | :--- |
| **Latest Release** | All users | Latest stable build | [⬇️ Go to Releases](https://github.com/anomalyco/Pulsar/releases) |
| **Build from source** | Developers | Compile and run from source | [🛠️ Build guide below](#-local-build--development) |

### Basic Usage

1. Download and run `Pulsar` — it runs in the system tray in the background;
2. Press the default hotkey **`Ctrl+Shift+Q`** (Command mode) or **`Ctrl+Q`** (Switch mode) to invoke the radial menu;
3. Slide toward the target sector using spatial positioning, release to trigger the action;
4. For detailed configuration, open Settings from Pulsar Control (`com.pulsar.system`).

---

## ✨ Features

### 1. 🌟 Radial Menu

A hotkey-invoked circular launcher with two modes:
- **Command mode** (`Ctrl+Shift+Q`): contextual actions;
- **Switch mode** (`Ctrl+Q`): app switching with an MRU center window, smart discovery, blacklist filtering, and auto-launch of missing apps.

<div align="center">
  <img src="Pulsar/Pulsar/Assets/Brand/demo.gif" width="640" alt="Pulsar Radial Menu Demo" />
</div>

---

### 2. 🧩 Extensible Plugin System

Two-tier architecture:
- **Core plugins**: essential infrastructure, always loaded, crashes are fatal (fail-fast);
- **Extension plugins**: optional, protected by a Circuit Breaker — 3 crashes within 1 minute triggers a 60-second disable, then half-open with a single retry, with Windows Toast notifications.

**Built-in plugins**:

| Plugin | ID | Description | Tier |
|--------|----|-------------|------|
| **Secret Fill (PKI)** | `com.pulsar.pki` | DPAPI-encrypted credential vault; inject username/password via UI Automation with delay and auto-submit | Core |
| **App Switcher** | `com.pulsar.winswitcher` | Smart window switching (fuzzy search), auto-launch if not running, discovery blacklist | Core |
| **Pulsar Control** | `com.pulsar.system` | Open settings, quick-add context apps, system commands | Core |
| **Command Runner** | `com.pulsar.command` | Launch apps/files/folders/URLs; send keystroke sequences to the foreground window | Extension |
| **VBA Script Runner** | `com.pulsar.vbarunner` | Execute VBA macros in Excel/WPS with smart directives | Extension |
| **Bookmarklet Runner** | `com.pulsar.bookmarklet` | Run JavaScript bookmarklets in the active browser via UI Automation | Extension |

---

### 3. 🔐 PKI / Secret Management

DPAPI-encrypted credential vault with UI Automation injection, auto-submit, and configurable delay. Follows the **Focus Boomerang** cycle: capture → execute → hide → restore focus → delay → inject, reliably returning focus to the original window.

---

### 4. 🔑 Global Hotkeys

System-wide bindings for instant access — default `Ctrl+Shift+Q` (Command mode) and `Ctrl+Q` (Switch mode).

---

### 5. 🖥️ App & Window Switching

Smart window switching with discovery blacklist; launches apps if not running.

---

### 6. 🤖 Plugin Simulator

Headless plugin execution with structured JSON output for AI-driven testing and a self-correction loop (simulator → parse errors → fix code → re-run until green).

---

### 7. 🌐 Localization

Simplified Chinese + English, with convention-based automatic key lookup for plugin metadata.

---

## 🎬 Video Demo

<!-- TODO: Replace with a real video cover and link -->
<div align="center">
  <a href="https://github.com/anomalyco/Pulsar" target="_blank">
    <img src="Pulsar/Pulsar/Assets/Brand/demo.gif" width="640" alt="Pulsar Demo Video Cover (placeholder)" />
  </a>
  <p>
    <a href="https://github.com/anomalyco/Pulsar"><b>📺 Click to watch the demo video (placeholder link)</b></a>
  </p>
</div>

---

## 📸 Screenshots

<!-- TODO: Add real screenshots, replace these placeholders -->
| Radial Menu | Settings | Plugin Editor |
|-------------|----------|---------------|
| `[Screenshot_RadialMenu]` | `[Screenshot_Settings]` | `[Screenshot_PluginEditor]` |

---

## 🛠️ Local Build & Development

### Prerequisites

- Windows 10 or later (x64)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Runtime needed to run; SDK to build)

### Build, Run & Test

```bash
# Restore & Build
dotnet restore Pulsar/Pulsar/Pulsar.csproj
dotnet build Pulsar/Pulsar/Pulsar.csproj

# Run (default hotkeys: Ctrl+Shift+Q = Command mode, Ctrl+Q = Switch mode)
dotnet run --project Pulsar/Pulsar/Pulsar.csproj

# Run tests (330+ xUnit tests)
dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj

# Headless plugin simulation (AI-driven plugin testing)
dotnet run --project Pulsar/Pulsar.Simulator -- --plugin "com.pulsar.winswitcher" --action "activate" --args "{\"app\":\"chrome\"}"

# Publish self-contained release (see Docs/ops/BUILD_AND_RUN.md for the full Artifacts workflow)
dotnet publish Pulsar/Pulsar/Pulsar.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:PublishDir="Artifacts\publish\v<Version>"
```

---

## 📂 Project Structure

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

---

## 🧠 Key Design Concepts

### PulsarContext — Immutable Context Snapshot

When the radial menu is invoked, Pulsar freezes the system state into an immutable `PulsarContext`, eliminating race conditions. Heavy properties (clipboard, window list) are lazy-loaded. Per-execution mutable data lives in `PluginExecutionContext` (AsyncLocal scope), not on the context itself.

### Focus Boomerang

Plugins that inject input (e.g., PKI) operate on a capture → execute → hide → restore → delay → inject cycle, reliably returning focus to the original window.

### Circuit Breaker for Extensions

Extension plugins are wrapped in a Circuit Breaker: 3 crashes within 1 minute triggers a 60-second disable period, after which the plugin enters half-open state for a single retry. Users are notified via Windows toast notifications.

### AI-First Development

The entire project is optimized for AI-agent collaboration:
- **Headless Simulator**: test plugins without the WPF shell, parse structured JSON output;
- **Isolated Side-Effects**: all OS coupling behind interfaces (`IInputSimulator`, `IProcessLauncher`, etc.) — mockable with Moq;
- **Comprehensive test suite**: 330+ xUnit tests covering ViewModels, services, and plugin logic;
- **Self-Correction loop**: simulator → parse errors → fix code → re-run until green.

---

## 🌐 Internationalization

Switch the UI language anytime from the settings page:

| Language Code | Display Name | Status |
| :--- | :--- | :---: |
| `zh-CN` | 🇨🇳 简体中文 | 🟢 Full support |
| `en` | 🇺🇸 English | 🟢 Full support |

---

## 📚 Documentation

| Resource | Description |
|----------|-------------|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | System architecture deep-dive |
| [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md) | Plugin development guide |
| [AGENTS.md](./AGENTS.md) | AI-assisted development conventions |
| [Docs/](./Docs/) | Full documentation index |
| [Docs/lessons/](./Docs/lessons/) | WPF pitfalls & known issues archive |
| [Docs/architecture/](./Docs/architecture/) | Architecture details (Plugin System, Dialog System, etc.) |
| [Docs/ops/BUILD_AND_RUN.md](./Docs/ops/BUILD_AND_RUN.md) | Build & run reference |

---

## 🤝 Community & Contributing

- **Changelog**: [CHANGELOG.md](./CHANGELOG.md) — version history
- **Contributing guide**: [CONTRIBUTING.md](./Docs/CONTRIBUTING.md) — how to contribute
- **Security**: report security issues via GitHub Issue or email

---

## 📌 Project Status

Pulsar is in active development. The architecture, plugin API, and core features are stable. The extension plugin ecosystem is growing.

---

## 📄 License

MIT — see [LICENSE](./LICENSE).
