<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="Pulsar/Pulsar/Assets/Brand/wordmark.png">
  <img alt="Pulsar" src="Pulsar/Pulsar/Assets/Brand/wordmark.png">
</picture>

# Pulsar

### An office automation workbench for Windows — one-click macros, secure fill & sign-in, and custom scripts for legacy intranet web pages
**重度办公效率工作台 · 驯服老旧办公系统**

[![Release Version](https://img.shields.io/badge/Release-v1.10.0-2563EB.svg?style=flat-square&logo=github)](https://github.com/Smith-Rosco/Pulsar/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D4.svg?style=flat-square&logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-10B981.svg?style=flat-square)](LICENSE)
[![Language](https://img.shields.io/badge/Language-zh--CN%20%7C%20en-8B5CF6.svg?style=flat-square)](#-internationalization)

<br/>

**[简体中文](README.md)** • **[English](README_EN.md)**

<br/>

[🚀 Quick Start](#-quick-start) · [✨ Features](#-features) · [🎬 Demo](#-demo) · [📸 Screenshots](#-screenshots) · [🧑‍💻 For Developers](#-for-developers) · [🤝 Community](#-community--contributing)

</div>

---

## 💡 What is it?

**Pulsar** is an office automation workbench for Windows, built for people who spend their days stuck repeating the same operations.

Tired of redoing spreadsheets in Excel/WPS? Logging into one intranet system after another? Filling in the same forms over and over? — Press a hotkey and a circular menu appears right next to your cursor. Every high-frequency action becomes a one-shot gesture you can trigger by sliding toward it.

**In one sentence**: it brings automation to the legacy office systems that "modern tools can't handle" — turning them into actions on a hotkey.

### The three things it does best

| | Scenario | Result |
| :--- | :--- | :--- |
| 📊 **One-click macros** | Repetitive spreadsheets & data work in Excel/WPS | Saved macros become one action on the wheel — fire and forget |
| 🌐 **Legacy web automation** | Intranet systems without browser-extension/userscript support | Custom one-click entries for old web pages — repetitive entry becomes automated |
| 🔐 **Secure fill & sign-in** | Logging into many systems, filling forms | Credentials encrypted & stored locally; inject once, auto-submit |

### Nice-to-haves

- **Radial menu**: actions live at fixed positions — build muscle memory and operate "blind", no more digging through menus;
- **Smart window switching**: jump to the window you want; not running? Pulsar launches it for you;
- **Global hotkeys**: summon Pulsar from any app, no need to switch back first;
- **Beginner friendly**: a first-run tutorial gets you started in minutes, plus ready-made office action presets and a script example library;
- **Bilingual**: switch the UI between Simplified Chinese and English anytime.

---

## 🚀 Quick Start

1. Grab the latest build from [Releases](https://github.com/Smith-Rosco/Pulsar/releases) and run it — Pulsar waits in your system tray;
2. Press **`Ctrl+Shift+Q`** to summon the command menu (or **`Ctrl+Q`** for switch mode);
3. Slide toward the action you want and release — it executes instantly;
4. Want to tweak things? Open Settings from Pulsar.

> 💡 First time? Follow the built-in **onboarding tutorial** — you'll be up to speed in minutes.

---

## ✨ Features

### 1. 📊 One-click macros (Excel/WPS)

Save your frequently used macros as "one action on the wheel". No more opening the VBA editor every time you need a report or a cleanup — slide once, the macro runs.

### 2. 🌐 Legacy web automation

Many corporate intranet systems are too old for modern browser extensions or userscripts. Pulsar lets you build one-click entries for those pages, turning daily repetitive web work into automation.

### 3. 🔐 Secure fill & sign-in

Store frequently used usernames and passwords locally with system-level encryption. At login, inject them with one action — auto-fill and auto-submit, with no plaintext credentials ever touching disk.

### 4. 🎯 Radial menu: operate by muscle memory

- **Command mode** (`Ctrl+Shift+Q`): shows the actions available right now;
- **Switch mode** (`Ctrl+Q`): fast window switching; auto-launches apps that aren't running;
- Frequent actions sit at fixed positions — practice a few times and you can go "blind", no more hunting through menus.

<div align="center">
  <img src="Pulsar/Pulsar/Assets/Brand/demo.gif" width="640" alt="Pulsar Radial Menu Demo" />
</div>

### 5. 🪟 Smart window switching

Jump straight to the window you want; if the app isn't running, Pulsar starts it for you.

### 6. 🧩 Built-in tools

| Tool | What it does |
| :--- | :--- |
| **Secret Fill** | Encrypted credential storage; inject into any window with one action |
| **App Switcher** | Smart window switching; auto-launches apps that aren't running |
| **Pulsar Control** | Open settings, quick-add context apps |
| **Command Runner** | Launch apps/files/folders/URLs; send keystrokes to the foreground window |
| **VBA Script Runner** | Run saved Excel/WPS macros with one click |
| **Web Scripts** | Run custom scripts on legacy intranet web pages |

### 7. 🎓 Beginner friendly

- First-run onboarding tutorial — learn the core operations step by step;
- Built-in **script editor** and **example library** — start from examples even if you've never written a script;
- **Office action preset packs** — install once, use immediately;
- UI in Simplified Chinese / English.

---

## 🎬 Demo

<!-- TODO: Replace with a real video cover and link -->
<div align="center">
  <a href="https://github.com/Smith-Rosco/Pulsar" target="_blank">
    <img src="Pulsar/Pulsar/Assets/Brand/demo.gif" width="640" alt="Pulsar Demo Video Cover (placeholder)" />
  </a>
  <p>
    <a href="https://github.com/Smith-Rosco/Pulsar"><b>📺 Click to watch the demo video (placeholder link)</b></a>
  </p>
</div>

---

## 📸 Screenshots

<!-- TODO: Add real screenshots, replace these placeholders -->
| Radial Menu | Settings | Plugin Editor |
|-------------|----------|---------------|
| `[Screenshot_RadialMenu]` | `[Screenshot_Settings]` | `[Screenshot_PluginEditor]` |

---

## 🌐 Internationalization

Switch the UI language anytime from the settings page:

| Language | Status |
| :--- | :---: |
| 🇨🇳 简体中文 | 🟢 Full support |
| 🇺🇸 English | 🟢 Full support |

---

## 🧑‍💻 For Developers

Pulsar is MIT-licensed and open to contributions — code, plugins, and ideas are all welcome.

- **[Developer Guide (DEVELOPER.md)](./DEVELOPER.md)**: tech stack, project structure, build/test commands, plugin development & architecture;
- **[ARCHITECTURE.md](./ARCHITECTURE.md)** · **[PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md)** · **[Docs index](./Docs/README.md)**;
- Please read [CONTRIBUTING.md](./Docs/CONTRIBUTING.md) before contributing.

---

## 🤝 Community & Contributing

- **Changelog**: [CHANGELOG.md](./CHANGELOG.md) — version history
- **Contributing guide**: [CONTRIBUTING.md](./Docs/CONTRIBUTING.md) — how to contribute
- **Security**: report security issues via GitHub Issue or email

---

## 📌 Project Status

Pulsar is in active development. The architecture, plugin API, and core features are stable, and the built-in tool & plugin ecosystem keeps growing.

---

## 📄 License

MIT — see [LICENSE](./LICENSE).
