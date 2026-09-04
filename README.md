<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="Pulsar/Pulsar/Assets/Brand/wordmark.png">
  <img alt="Pulsar" src="Pulsar/Pulsar/Assets/Brand/wordmark.png">
</picture>

# Pulsar

### 重度办公效率工作台 · 驯服老旧办公系统
**An office automation workbench for Windows — one-click macros, secure fill & sign-in, and custom scripts for legacy intranet web pages**

[![Release Version](https://img.shields.io/badge/Release-v1.10.0-2563EB.svg?style=flat-square&logo=github)](https://github.com/Smith-Rosco/Pulsar/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D4.svg?style=flat-square&logo=windows)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/License-MIT-10B981.svg?style=flat-square)](LICENSE)
[![Language](https://img.shields.io/badge/Language-zh--CN%20%7C%20en-8B5CF6.svg?style=flat-square)](#-多语言支持)

<br/>

**[简体中文](README.md)** • **[English](README_EN.md)**

<br/>

[🚀 快速开始](#-快速开始) · [✨ 功能特性](#-功能特性) · [🎬 演示](#-演示) · [📸 截图](#-截图) · [🧑‍💻 开发者](#-开发者) · [🤝 社区与贡献](#-社区与贡献)

</div>

---

## 💡 这是什么？

**Pulsar** 是一款 Windows 办公自动化工作台，专为每天被重复操作困住的办公族打造。

每天在 Excel/WPS 里重复制表？在好几个内网系统之间来回登录？一遍遍填同一份表单？——按下热键，一个圆形菜单立刻在你鼠标旁展开，这些高频动作全部变成"滑一下就能触发"的一键操作。

**一句话**：把"现代工具管不了"的老旧办公系统，也变成热键上的一个动作。

### 它最擅长这三件事

| | 场景 | 效果 |
| :--- | :--- | :--- |
| 📊 **一键跑宏** | Excel/WPS 里重复的制表、数据处理 | 已保存的宏变成轮盘上的一个动作，一点即跑 |
| 🌐 **老旧网页自动化** | 不支持浏览器插件/油猴的内网系统 | 为网页定制一键操作入口，重复录入变自动化 |
| 🔐 **安全填表登录** | 多个系统反复登录、填表 | 账号密码加密保存在本地，一键注入、自动提交 |

### 还有这些贴心功能

- **径向菜单**：动作固定在固定方位，练几次就能凭肌肉记忆"闭眼操作"，不再翻菜单；
- **智能窗口切换**：快速切到目标窗口，应用没打开还会自动帮你启动；
- **全局热键**：在任何程序里都能随时唤出，不用先切回 Pulsar；
- **新手友好**：首次启动的引导教程带你几分钟上手，还有现成的办公动作预设包和脚本示例库可以直接用；
- **中英双语**：界面语言随时切换。

---

## 🚀 快速开始

1. 前往 [Releases](https://github.com/Smith-Rosco/Pulsar/releases) 下载最新版本并运行，Pulsar 会在系统托盘后台待命；
2. 按 **`Ctrl+Shift+Q`** 唤出命令菜单（或 **`Ctrl+Q`** 进入切换模式）；
3. 朝着目标动作的方向滑过去、松开，动作立即执行；
4. 想调整配置？在 Pulsar 设置里打开即可。

> 💡 首次使用建议跟着内置的**新手引导**走一遍，几分钟就能上手。

---

## ✨ 功能特性

### 1. 📊 一键跑宏（Excel/WPS）

把你常用的宏保存成"轮盘上的一个动作"。以后做报表、整理数据，不用再打开 VBA 编辑器——滑一下，宏自动跑完。

### 2. 🌐 老旧网页自动化

很多公司内网系统年代久远，不支持现代浏览器扩展或油猴脚本。Pulsar 可以为这类网页定制一键操作入口，把每天重复的网页操作变成自动执行。

### 3. 🔐 安全填表与登录

常用账号密码用系统级加密保存在你自己电脑上。需要登录时一键注入，自动填表、自动提交——密码不落地明文，也不用担心输错。

### 4. 🎯 径向菜单：凭肌肉记忆操作

- **命令模式**（`Ctrl+Shift+Q`）：展示当前可用的快捷动作；
- **切换模式**（`Ctrl+Q`）：快速切换窗口，应用未运行时自动补位启动；
- 高频动作固定在固定方位，练几次就能盲操作，告别在菜单里来回翻找。

<div align="center">
  <img src="Pulsar/Pulsar/Assets/Brand/demo.gif" width="640" alt="Pulsar 径向菜单演示" />
</div>

### 5. 🪟 智能窗口切换

快速定位并切换到目标窗口；应用没在运行？自动帮你启动。

### 6. 🧩 内置工具一览

| 工具 | 一句话说明 |
| :--- | :--- |
| **秘密填充** | 加密保存账号密码，一键注入任意窗口 |
| **应用切换器** | 智能窗口切换，未运行的应用自动启动 |
| **Pulsar 设置** | 打开设置、快捷添加上下文应用 |
| **命令启动器** | 启动应用/文件/文件夹/网址，还可给前台窗口发送按键 |
| **Excel 宏执行器** | 在 Excel/WPS 中一键运行已保存的宏 |
| **网页脚本执行器** | 在老旧内网网页中运行自定义脚本 |

### 7. 🎓 新手友好

- 首次启动的引导教程，边看边点学会核心操作；
- 内置**脚本编辑器**与**示例库**，不会写也能从示例开始；
- **办公动作预设包**一键安装，开箱即用；
- 界面支持简体中文 / English。

---

## 🎬 演示

<!-- TODO: 替换为真实的演示视频封面与链接 -->
<div align="center">
  <a href="https://github.com/Smith-Rosco/Pulsar" target="_blank">
    <img src="Pulsar/Pulsar/Assets/Brand/demo.gif" width="640" alt="Pulsar 演示视频封面（占位）" />
  </a>
  <p>
    <a href="https://github.com/Smith-Rosco/Pulsar"><b>📺 点击观看演示视频（占位链接）</b></a>
  </p>
</div>

---

## 📸 截图

<!-- TODO: 添加真实截图，替换以下占位符 -->
| 径向菜单 | 设置界面 | 插件编辑 |
|---------|---------|---------|
| `[截图_径向菜单]` | `[截图_设置界面]` | `[截图_插件编辑]` |

---

## 🌐 多语言支持

可在设置页面中随时切换界面语言：

| 语言 | 支持状态 |
| :--- | :---: |
| 🇨🇳 简体中文 | 🟢 完整支持 |
| 🇺🇸 English | 🟢 完整支持 |

---

## 🧑‍💻 开发者

Pulsar 采用 MIT 开源，欢迎贡献代码、插件与建议。

- **[开发文档（DEVELOPER.md）](./DEVELOPER.md)**：技术栈、项目结构、构建/测试命令、插件开发与架构设计；
- **[架构详解](./ARCHITECTURE.md)** · **[插件开发指南](./PLUGIN_DEVELOPMENT.md)** · **[完整文档索引](./Docs/README.md)**；
- 贡献前请阅读 [CONTRIBUTING.md](./Docs/CONTRIBUTING.md)。

---

## 🤝 社区与贡献

- **更新日志**：[CHANGELOG.md](./CHANGELOG.md) — 版本更新记录
- **贡献指南**：[CONTRIBUTING.md](./Docs/CONTRIBUTING.md) — 如何参与贡献
- **安全问题**：通过 GitHub Issue 或邮件报告安全问题

---

## 📌 项目状态

Pulsar 正在活跃开发中，核心功能与插件体系已趋于稳定，内置工具与插件生态持续增长。

---

## 📄 开源许可证

本项目采用 [MIT License](LICENSE) 开源。
