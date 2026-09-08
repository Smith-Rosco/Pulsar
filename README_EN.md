<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="Pulsar/Pulsar/Assets/Icons/pulsar-dark-256.png">
  <img alt="Pulsar" src="Pulsar/Pulsar/Assets/Icons/pulsar-light-256.png" width="128" height="128">
</picture>

# Pulsar

### An office automation workbench for Windows — one-click macros, secure fill & sign-in, and custom scripts for legacy intranet web pages
**重度办公效率工作台 · 驯服老旧办公系统**

[![Release Version](https://img.shields.io/badge/Release-v1.11.0-2563EB.svg?style=flat-square&logo=github)](https://github.com/Smith-Rosco/Pulsar/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D4.svg?style=flat-square&logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-10B981.svg?style=flat-square)](LICENSE)
[![Language](https://img.shields.io/badge/Language-zh--CN%20%7C%20en-8B5CF6.svg?style=flat-square)](README.md)

<br/>

**[简体中文](README.md)** • **[English](README_EN.md)**

<br/>

[Quick Start](#quick-start) · [What it does](#what-it-does) · [vs. Similar Tools](#vs-similar-tools) · [Demo](#demo) · [For Developers](#for-developers) · [Community](#community--contributing)

</div>

---

## What is it?

Pulsar is an office automation workbench for Windows. Press a hotkey and a circular menu opens next to your cursor; high-frequency actions become a single slide-and-release gesture.

It sits apart from search launchers, plain radial menus and RPA tools. Pulsar covers what they leave out: legacy intranet systems that can't take browser extensions, login forms you fill by hand every time, and macros you keep re-running in Excel/WPS.

If you rebuild the same spreadsheet every day, sign into several intranet systems in a row, or fill in the same form over and over, this is for you.

---

## Quick Start

### Download

The two packages differ only in whether the runtime is bundled:

| Package | What it is | Size |
| :--- | :--- | :--- |
| **Standalone (full)** | Self-contained, extract & run | ~80 MB |
| **Portable (lightweight)** | Needs the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) installed first | ~8.5 MB |

Most people want the standalone build. Pick portable if the download is slow or disk space is tight.

- **Standalone**: [Pulsar-1.11.0-full.zip](https://github.com/Smith-Rosco/Pulsar/releases/download/v1.11.0/Pulsar-1.11.0-full.zip)
- **Portable**: [Pulsar-1.11.0-portable.zip](https://github.com/Smith-Rosco/Pulsar/releases/download/v1.11.0/Pulsar-1.11.0-portable.zip)
- Older versions & release notes: [Releases page](https://github.com/Smith-Rosco/Pulsar/releases)

### Usage

1. Extract into a stable folder (e.g. `C:\Pulsar`) and double-click `Pulsar.exe` — Pulsar waits in your system tray;
2. Press `Ctrl+Shift+Q` to summon the command menu, `Ctrl+Q` for switch mode;
3. Slide toward the action you want and release — it executes instantly;
4. Settings live in the settings window.

> First run includes an onboarding tutorial. It takes a few minutes.

---

## What it does

### One-click macros (Excel/WPS)

Save a macro you use often as one action on the wheel. Reports and data cleanup no longer mean opening the VBA editor — slide once and the macro runs.

### Legacy web automation

A lot of corporate intranet systems are too old for browser extensions or userscripts. Pulsar injects scripts from the desktop layer and gives those pages one-click entries.

### Secure fill & sign-in

Credentials are encrypted with Windows DPAPI and stored on your machine, then injected and submitted in one action. No plaintext on disk. Injection happens as text injection at the keyboard layer, so it isn't limited to web pages: any desktop app with a username + password box works too.

### Radial menu

- **Command mode** (`Ctrl+Shift+Q`): the actions available right now;
- **Switch mode** (`Ctrl+Q`): window switching, auto-launching apps that aren't running;
- Actions stay at fixed positions, so once you've used it for a while you can operate by muscle memory instead of scanning menus.

<div align="center">
  <img src="Docs/media/release/demos/demo-radial-menu.webp" width="640" alt="Pulsar Radial Menu Demo" />
</div>

### Built-in tools

| Tool | What it does |
| :--- | :--- |
| **Secret Fill** | Encrypted credential storage; inject into any window in one action |
| **App Switcher** | Window switching; auto-launches apps that aren't running |
| **Pulsar Control** | Open settings, quick-add context apps |
| **Command Runner** | Launch apps / files / folders / URLs; send keystrokes to the foreground window |
| **VBA Script Runner** | Run saved Excel/WPS macros in one click |
| **Web Scripts** | Run custom scripts on legacy intranet web pages |

### Getting started

- First-run onboarding tutorial — learn the core operations step by step;
- Built-in script editor and example library — start from an example if you've never written a script;
- Office action preset packs — install and use;
- UI in Simplified Chinese / English.

---

## vs. Similar Tools

Pick a tool by the problem you need to solve, not by which one is "stronger":

| Your need | Recommended tool | Why |
| :--- | :--- | :--- |
| Taming legacy intranet systems (macros + old web pages + secure sign-in) | **Pulsar** | Built for legacy office systems: desktop-layer script injection + DPAPI-encrypted credential injection + fixed-position radial menu |
| Universal button panel with a huge library of ready-made actions | [Quicker](https://getquicker.net) | The strongest community-shared action-library ecosystem, with a handy context panel; closed-source, and the free tier caps daily triggers |
| Keyboard-first: search files, search everything | [Flow Launcher](https://www.flowlauncher.com) · [Microsoft PowerToys](https://learn.microsoft.com/windows/powertoys/) (Run / Command Palette) | The benchmark search launchers; Pulsar deliberately has no search box — complementary, not competing |
| Cross-platform / pure radial menu | [Kando](https://github.com/kando-menu/kando) · [StarPie](https://github.com/SoftBlack42/StarPie) · [RadialActions](https://github.com/danielchalmers/RadialActions) | Great open-source radial / circular menus for launching apps, files and shortcuts — no office-automation layer |
| Long-running, cross-system heavy process automation | Power Automate Desktop · Yingdao RPA (影刀) | Professional RPA tools; Pulsar focuses on high-frequency micro-actions you finish with one flick — when you outgrow it, they're the upgrade path |

---

## Demo

### One-click macros

A typical messy sheet: inconsistent formats, random colours, unreasonable column widths. Summon the wheel, slide toward the macro slot, release. The script unifies fonts, clears the stray colours, auto-fits the columns and freezes the header row.

<div align="center">
  <img src="Docs/media/release/demos/demo-1-one-key-macro.webp" width="640" alt="One-click macro demo" />
</div>

> Can't write macros? Paste the [VBA script AI prompt](./Docs/guides/AI_PROMPT_VBA_RUNNER.md) into any AI chat, describe what you need, and you'll get a macro script that runs as-is.

### Taming legacy systems

On this decade-old intranet reporting portal, sign in → query → export is three separate click-throughs. With Pulsar it's one summon, slide and release: all three run, and the CSV downloads itself.

<div align="center">
  <img src="Docs/media/release/demos/demo-2-tame-legacy.webp" width="640" alt="Taming legacy systems demo" />
</div>

> Can't write scripts? Paste the [web-script AI prompt](./Docs/guides/AI_PROMPT_BOOKMARKLET.md) into any AI chat. It also asks for a "DOM contract checklist" to re-verify after page redesigns.

### Sign in, one slide

Type the username and password by hand once. Then clear the field, slide toward the sign-in slot, and the credentials are injected and submitted.

<div align="center">
  <img src="Docs/media/release/demos/demo-3-one-slide-login.webp" width="640" alt="One-slide sign-in demo" />
</div>

---

## For Developers

Pulsar is MIT-licensed and open to contributions — code, plugins, and ideas are all welcome.

- **[User Manual](./Docs/manual/README.md)**: install, run macros, sign into legacy systems, switch windows, FAQ (bilingual);
- **[Developer Guide (DEVELOPER.md)](./DEVELOPER.md)**: tech stack, project structure, build/test commands, plugin development & architecture;
- **[ARCHITECTURE.md](./ARCHITECTURE.md)** · **[PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md)** · **[Docs index](./Docs/README.md)**;
- Please read [CONTRIBUTING.md](./Docs/CONTRIBUTING.md) before contributing.

Plugins come in two tiers: Core plugins can't be disabled, while Extension plugins sit behind a circuit breaker — three crashes within a minute disables one for 60 seconds, so a bad plugin can't take the host down.

---

## Community & Contributing

- **Changelog**: [CHANGELOG.md](./CHANGELOG.md) — version history
- **Contributing guide**: [CONTRIBUTING.md](./Docs/CONTRIBUTING.md) — how to contribute
- **Security**: report security issues via GitHub Issue or email

---

## Roadmap

Pulsar is in active development; the architecture, plugin API and core features are stable. "Describe a scenario in plain language and let AI generate the wheel configuration" is being explored, with no schedule attached — nothing in the local automation stack depends on it.

---

## License

MIT — see [LICENSE](./LICENSE).
