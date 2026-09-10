# Pulsar 开发文档（Developer Guide）

> 面向开发者、贡献者与 AI Agent 的**技术文档**。用户向内容请看 [README.md](./README.md)。

**Pulsar** 是基于 .NET 8 的高性能 Windows 办公自动化工作台：WPF + WinForms 双栈、MVVM（CommunityToolkit.Mvvm）+ 依赖注入（DI）、热键唤起的径向菜单、可扩展插件系统。

---

## 📋 目录

1. [技术栈与环境要求](#-技术栈与环境要求)
2. [项目结构](#-项目结构)
3. [构建 · 运行 · 测试 · 发布](#-构建--运行--测试--发布)
4. [架构概览](#-架构概览)
5. [插件系统](#-插件系统)
6. [核心设计理念](#-核心设计理念)
7. [AI 优先开发](#-ai-优先开发)
8. [本地化约定](#-本地化约定)
9. [文档导航](#-文档导航)

---

## 🛠️ 技术栈与环境要求

| 类别 | 选型 |
| :--- | :--- |
| **框架** | .NET 8.0（WPF + WinForms） |
| **UI 模式** | MVVM + Dependency Injection（CommunityToolkit.Mvvm） |
| **UI 库 / 原生** | WPF-UI、Hardcodet.NotifyIcon.Wpf、gong-wpf-dragdrop、System.Drawing.Common、Microsoft.Windows.CsWin32 |
| **DI / 日志** | Microsoft.Extensions.DependencyInjection、Serilog（结构化日志） |
| **测试** | xUnit + Moq（1000+，以 `dotnet test` 实际输出为准） |
| **环境** | Windows 10+（x64）、.NET 8.0 SDK（运行仅需 Runtime，编译需 SDK） |

---

## 📂 项目结构

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
│   ├── Interfaces/             #   插件运行时三窄 seam（注册面/执行面/运维面）
│   │   ├── IPluginRegistry.cs  #     注册面：发现·激活·查询
│   │   ├── IPluginExecutor.cs  #     执行面：ExecuteAsync
│   │   └── IPluginRuntimeOps.cs#     运维面：重扫·停用·状态·授权·卸载
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
│   ├── Presets/               #   办公动作预设包（目录/安装/生命周期）
│   └── Tutorial/              #   交互式入门引导系统
├── Styles/                    # 自定义 WPF 样式（Pulsar 按钮、插槽、滚动条）
├── Themes/                    # 主题 XAML（深色 + 浅色）
└── Resources/                 # 本地化资源 (.resx)
    ├── Strings.resx           # 英文（基础语言）
    └── Strings.zh-CN.resx     # 简体中文
```

> 说明：仓库根目录还包含 `Pulsar.Tests/`（xUnit + Moq 测试）、`Pulsar.Simulator/`（无头插件模拟器）与 `Pulsar.Samples/`（示例插件，如 `NeonRendererPlugin`）。

---

## 🔨 构建 · 运行 · 测试 · 发布

```bash
# 还原依赖 & 编译
dotnet restore Pulsar/Pulsar/Pulsar.csproj
dotnet build Pulsar/Pulsar/Pulsar.csproj

# 运行（默认热键：Ctrl+Shift+Q 命令模式、Ctrl+Q 切换模式）
dotnet run --project Pulsar/Pulsar/Pulsar.csproj

# 运行测试（1000+，以 dotnet test 实际输出为准）
dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj

# 无头插件模拟（AI 驱动的插件测试）
dotnet run --project Pulsar/Pulsar.Simulator -- --plugin "com.pulsar.winswitcher" --action "activate" --args "{\"app\":\"chrome\"}"

# 发布自包含版本（完整发布工作流见 Docs/ops/BUILD_AND_RUN.md）
dotnet publish Pulsar/Pulsar/Pulsar.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:PublishDir="Artifacts\publish\v<Version>"
```

完整构建 / 发布 / 打包流程见 [Docs/ops/BUILD_AND_RUN.md](./Docs/ops/BUILD_AND_RUN.md)。

---

## 🏗️ 架构速览

> 以下每条只给一句话结论；细节在权威源，**不在此复述**。

| 机制 | 一句话 | 权威源 |
| :--- | :--- | :--- |
| PulsarContext | 唤起时冻结的不可变上下文快照，重型属性懒加载；每次执行的可变数据在 `PluginExecutionContext`（AsyncLocal） | [PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md) |
| 插件运行时三窄 seam | 同一 `PluginRuntimeKernel` 单例上的注册/执行/运维三个窄面，按消费方注入最窄面 | [PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md) · [ADR-012](./Docs/decisions/012-plugin-runtime-three-seams.md) |
| 扩展插件断路器 | 纯状态机：1 分钟 3 崩溃 → 60 秒禁用 → 半开重试 | [PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md) · [ADR-013](./Docs/decisions/013-circuit-breaker-observation-seam.md) |
| 焦点回旋镖 | 输入注入类插件遵循 `捕获→执行→隐藏→恢复焦点→延迟→注入` 循环 | [ARCHITECTURE.md §3.3](./ARCHITECTURE.md) |
| 配置单写者 | `Profiles.json` 唯一事实源；`GetSnapshot()` 深拷贝只读，写入经 `ConfigEditSession`（revision 守卫） | [ADR-005](./Docs/decisions/005-config-single-writer.md) · [ADR-009](./Docs/decisions/009-config-snapshot-seam.md) |
| 多主题注入 | `App.xaml` 无全局样式，每个 Window/Page 在 `InitializeComponent()` **之后**调 `ApplyTheme()` | [WPF_THEME_INJECTION_PITFALLS.md](./Docs/lessons/WPF_THEME_INJECTION_PITFALLS.md) |

---

## 🧩 插件系统

分层（Core/Extension）语义、生命周期与熔断细节见 [PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md)；外部插件安全模型（manifest 权限门控、先授权后实例化）是硬性不变量，见 [AGENTS.md §2](./AGENTS.md)。

**内置插件清单**：

| 插件 | ID | 描述 | 分层 |
|------|----|------|------|
| 秘密填充 (Secret Fill) | `com.pulsar.pki` | DPAPI 加密凭据库，UI 自动化注入用户名/密码，支持延迟与自动提交 | 核心 |
| 应用切换器 | `com.pulsar.winswitcher` | 智能窗口切换（模糊搜索），未运行自动启动，支持发现黑名单 | 核心 |
| Pulsar 设置 | `com.pulsar.system` | 打开设置、快速添加上下文应用、系统命令 | 核心 |
| 命令启动器 | `com.pulsar.command` | 启动应用/文件/文件夹/URL，向前台窗口发送按键序列 | 扩展 |
| Excel 宏执行器 | `com.pulsar.vbarunner` | 在 Excel/WPS 中运行已保存的宏，支持智能指令 | 扩展 |
| 网页脚本执行器 | `com.pulsar.bookmarklet` | 在老旧内网网页中运行自定义脚本（内置脚本编辑器 + 示例库） | 扩展 |

**开发新插件** → [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md)，架构细节 → [Docs/architecture/PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md)。

---

## 🧠 核心设计理念

- **肌肉记忆优先**：高频动作固定在固定方位，替代线性 Alt-Tab 遍历；
- **插件化应用容器**：从"启动器"演进为"应用容器"，统一插件系统承载业务逻辑；
- **AI 友好开发**：无头模拟器 + 结构化 JSON 输出 + 全面测试套件（见下节）。

---

## 🤖 AI 优先开发

整个项目为 AI Agent 协作进行了优化：

- **无头模拟器**：无需 WPF 界面即可运行插件，解析结构化 JSON 输出（`Pulsar.Simulator`）；
- **隔离副作用**：所有 OS 操作通过接口抽象（`IInputSimulator`、`IClipboardMonitor`、`IProcessLauncher` 等），可用 Moq 单元测试；
- **全面测试套件**：1000+ 个 xUnit 测试覆盖 ViewModel、服务与插件逻辑（以 `dotnet test` 实际输出为准）；
- **自纠错循环**：模拟器 → 解析错误 → 修复代码 → 重新运行直到通过。

---

## 🌐 本地化约定

硬性规则全文见 [AGENTS.md §2 Localization](./AGENTS.md)（唯一权威源）。要点：禁止在 C#/XAML 硬编码用户可见字符串；插件元数据按 `SlotParam.{AlphaNumOnly(Label)}` / `SlotAction.{AlphaNumOnly(Label)}` 约定自动本地化；新增翻译同步 `Strings.resx` + `Strings.zh-CN.resx`。

---

## 📚 文档导航

完整索引与任务路由的唯一入口是 **[Docs/README.md](./Docs/README.md)**。常用直达：

| 资源 | 描述 |
|------|------|
| [AGENTS.md](./AGENTS.md) | AI 辅助开发规范（不变量、坑点速查、任务路由） |
| [Docs/architecture/](./Docs/architecture/) | 架构细节（插件系统、对话框系统、输入注入等） |
| [Docs/ops/BUILD_AND_RUN.md](./Docs/ops/BUILD_AND_RUN.md) | 构建与运行参考 |
| [openspec/](./openspec/) | 行为规格（specs / changes / archive） |

---

## 📌 项目状态

Pulsar 正在活跃开发中。架构、插件 API 与核心功能趋于稳定，扩展插件生态持续增长。更新历史见 [CHANGELOG.md](./CHANGELOG.md)。
