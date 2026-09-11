# AGENTS.md（中文版）- AI Agent 操作指南

> 本文件是 `AGENTS.md` 的**完整简体中文译版**，供审查使用。
> 翻译原则：**路径、命令、类名、方法名、resx 键名、代码块一律保持原文**，仅翻译说明性文字；专业术语采用「中文（English）」双标，便于对照原版排查。
> ⚠️ 本文件目前为审查稿，**尚未替代根目录 `AGENTS.md`**（后者含 `.agents/skills/` 等机器消费方硬编码读取的段落，替换前需确认）。

在 **Pulsar** 代码库（.NET 8、WPF/WinForms、MVVM + DI）中工作的 AI Agent 操作指南。

---

## 1. 项目快照

**Pulsar** 是一款面向 Windows 的高性能生产力启动器，采用径向菜单（Radial Menu）界面。

- **框架**：.NET 8.0（WPF + WinForms），MVVM（CommunityToolkit.Mvvm）+ 依赖注入
- **核心特性**：径向菜单、全局热键、SecretFill / 凭据管理、可扩展插件系统
- **关键原语**：
  - **PulsarContext**：在径向菜单唤起时捕获的**不可变上下文快照**（懒加载）。每次执行的关联数据（PluginId、Action、ExecutionId）保存在栈作用域的 `PluginExecutionContext`（AsyncLocal）中，**绝不放进** `PulsarContext`。
  - **插件分层（Plugin Tiers）**：Core（必需，快速失败）vs Extension（可选，熔断器保护：1 分钟内崩溃 3 次 = 禁用 60 秒）
  - **配置**：`Profiles.json` —— 唯一事实来源（single source of truth）

---

## 2. 不可协商的铁律（Non-Negotiable Invariants）

### 插件
- 插件内**绝不查询实时窗口状态**，一律使用 `PulsarContext`。
- 外部插件受权限门控：`PluginPermissionService` 会阻止执行，直到 manifest 中声明的每一项权限都出现在 `PluginProfile.GrantedPermissions` 中。外部插件描述符由 `plugin.manifest.json` 构建，**不会实例化类型**；构造函数仅在用户同意后运行。
- Core 插件（`Plugins/Core/`）：必需，无法禁用，崩溃即致命。Extension 插件（`Plugins/`）：可选，受熔断器保护。

**深入阅读**：[Docs/architecture/PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md)、[PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md)

### UI
- **不要使用 `Appearance="Primary"`**（Wpf.Ui 按钮）；改用 `PulsarPrimaryButtonStyle` / `PulsarSecondaryButtonStyle` / `PulsarDangerButtonStyle`（动态资源继承在多宿主 UI 下会失效 → 悬停时文字不可见）。
- 多宿主 UI（Multi-Headed UI）：`App.xaml` **没有**全局样式；每个 Window/Page 都要通过 `IThemeService.ApplyTheme()` 手动注入主题。
- 对 Page，必须在 `InitializeComponent()` **之后**调用 `ApplyTheme()`（加载 Page 会替换其 Resources 字典）。

### 本地化
- **绝不**在 C# 或 XAML 中硬编码面向用户的字符串。使用 `ILocalizationService`（C# 侧 `_loc["Key"]`，XAML 侧 `{lex:Locale Key}`）或插件参数标签约定。
- 插件元数据标签按约定自动本地化：参数 → `SlotParam.{AlphaNumOnly(Label)}`，动作 → `SlotAction.{AlphaNumOnly(Label)}`；回退值为原始标签文本。
- 通过 `PluginResult.Error()` / `PluginResult.Ok()` 返回的插件错误/成功消息**必须**使用 `ILocalizationService`。若被 `ActionFeedbackService` 模式匹配到，需**同时**更新插件侧与匹配侧（双语）。
- 新增翻译：在 `Resources/Strings.resx` 与 `Resources/Strings.zh-CN.resx` 中同时添加 `<data>`；命名规范 `Category.SubCategory.Description`；占位符 `{0}`/`{1}` 配合 `string.Format(...)` 使用。

**关键文件**：`Resources/Strings.resx`、`Resources/Strings.zh-CN.resx`、`Core/Localization/LocalizationService.cs`、`Core/Localization/LocExtension.cs`、`Models/SlotParameterEditorModels.cs`（约定查找逻辑，第 48-58 行）

---

## 3. 高危陷阱 Top-5（+ 索引指针）

> 完整表格见 [Docs/lessons/](./Docs/lessons/)（每个陷阱一个文件：症状 → 根因 → 修复）。此处仅内联出现频率最高的那些。

| 症状 | 根因 / 修复 |
|---|---|
| 主题 DynamicResources 缺失、界面空白 | `ApplyTheme()` 在 `InitializeComponent()` **之前**调用（Page 加载会替换其 Resources）。必须在**之后**调用。 → [WPF_THEME_INJECTION_PITFALLS.md](./Docs/lessons/WPF_THEME_INJECTION_PITFALLS.md) |
| 托盘首次右键菜单主题错误 | `ThemeService.CurrentTheme` 未在 `TrayIconService.BuildContextMenu()` 之前从 `Profiles.json` 初始化。需在 `AppStartupCoordinator` 中、`ITrayService.Initialize()` 之前引导 `IThemeService.Initialize(config.Settings.ThemeEnum)`。 → [WPF_THEME_INJECTION_PITFALLS.md](./Docs/lessons/WPF_THEME_INJECTION_PITFALLS.md) |
| Pulsar 按钮出现强调色叠强调色（蓝底蓝字）或回退灰 | `Accent*` Fluent 令牌无法解析：`ThemeService.ApplyAccent` 必须把 `UiApplication.Current.Resources` 桥接到 `Application.Current.Resources`；强调色填充上的按钮文字使用 `TextOnAccentFillColorPrimaryBrush`，**绝不用** `AccentTextFillColorPrimaryBrush`。 → [WPF_FLUENT_ACCENT_TOKENS_UNRESOLVED.md](./Docs/lessons/WPF_FLUENT_ACCENT_TOKENS_UNRESOLVED.md) |
| 删除 slot / 设置后 `Profiles.json` 回退 | `HotkeyService` 持有过期的 `_config`，`UpdateHotkey()` 会把它重新写回。在 `SettingsViewModel.Save()` 中使用 `RebuildCache()`（从 `_configService.Current` 刷新 `_config`），不要用 `UpdateHotkey()`。 → [HOTKEY_SERVICE_STALE_CONFIG_OVERWRITE.md](./Docs/lessons/HOTKEY_SERVICE_STALE_CONFIG_OVERWRITE.md) |
| 运行时插件卸载 / 覆盖安装失败 | 插件 DLL 被占用（可回收 ALC 从未卸载）。卸载流程：撤销权限 → `DeactivatePluginAsync`（完整拆卸，含 ALC 卸载）→ 带重试地删除；删除前强制 `GC.Collect()`；在 `RemoveDescriptor` 之前把 `descriptor.ImplementationType` 置空。 → [PLUGIN_RUNTIME_INSTALL_UNINSTALL_PITFALLS.md](./Docs/lessons/PLUGIN_RUNTIME_INSTALL_UNINSTALL_PITFALLS.md) |

**全部经验教训**（主题、配置、插件生命周期、异步关闭、输入注入等）：[Docs/lessons/](./Docs/lessons/)

---

## 4. 任务路由与文档索引

| 任务 | 位置 |
|---|---|
| 构建 / 运行命令 | [Docs/ops/BUILD_AND_RUN.md](./Docs/ops/BUILD_AND_RUN.md) |
| 新增 / 修改插件 | [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md)、[Docs/architecture/PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md) |
| 新增对话框 | [Docs/architecture/DIALOG_SYSTEM.md](./Docs/architecture/DIALOG_SYSTEM.md) —— **在 `Themes/DialogTemplates.xaml` 注册 DataTemplate，并在 `Services/DialogCatalog.cs` 登记一行（标题键 / 尺寸 / 按钮 / 主题），再用 `ShowCustomAsync(DialogId.X, content)` 展示。** [ADR-033](./Docs/decisions/033-dialog-catalog-single-registration-surface.md) |
| 新增 / 修改设置页 | `Services/SettingsPageCatalog.cs`（注册；按需动态页签用 `IsTransient`）+ `Services/SettingsPageFactory.cs`（注册创建器）+ resx 键。瞬态页（Transient pages）：按类型单例，干净离开时自动回收 —— [029-settings-transient-pages.md](./Docs/decisions/029-settings-transient-pages.md)。重型配置用瞬态页 / 专用页，**不用模态框**（模态框仅用于一次性确认与选择器） |
| 修改 UI（XAML） | [Docs/guides/UI_BEST_PRACTICES.md](./Docs/guides/UI_BEST_PRACTICES.md)、[Docs/guides/COMPONENT_LIBRARY.md](./Docs/guides/COMPONENT_LIBRARY.md) |
| 径向菜单交互 / 状态 | `ViewModels/MenuSession.cs`（状态机）、`ViewModels/RadialMenuViewModel.cs`（薄绑定投影）。策略类依赖 `IMenuSession`，**绝不依赖 VM**。 [008-menu-session-refactor.md](./Docs/decisions/008-menu-session-refactor.md) |
| 配置持久化 / 写入 | `ConfigService.cs` + `ConfigEditSession.cs`。`GetSnapshot()` 返回深拷贝，**不可直接修改**；所有写入走 `ConfigEditSession`（受修订号保护，revision-guarded）。 [009](./Docs/decisions/009-config-snapshot-seam.md)、[005](./Docs/decisions/005-config-single-writer.md) |
| 输入注入（Secret Fill） | [Docs/architecture/INPUT_INJECTION.md](./Docs/architecture/INPUT_INJECTION.md) |
| WPF UI 问题 | [Docs/lessons/](./Docs/lessons/) |
| 证明 UI / 运行时缺陷已修复（动画、布局溢出） | `.agents/skills/pulsar-ui-runtime-verification/SKILL.md` —— 证据优先于目测：临时追踪通道 → 自驱动 E2E 真实点击 → 修复前后签名对照；当布局在无头环境下无法测量（`ActualWidth == 0`）时，用静态 XAML 守卫钉死该回归 |
| 架构决策 / 文档标准 | [Docs/decisions/](./Docs/decisions/)、[Docs/CONTRIBUTING.md](./Docs/CONTRIBUTING.md) —— **文档路由**（spec vs ADR vs lessons vs journal） |
| 架构总览 | [ARCHITECTURE.md](./ARCHITECTURE.md)、[Docs/README.md](./Docs/README.md) |
| 线程安全与并发 | [Docs/architecture/PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md)（`ConcurrentDictionary`、`Interlocked`、`Dispatcher.InvokeAsync`） |
| 提出 / 跟踪 spec 变更 | [openspec/](./openspec/) —— `/opsx-propose` … `/opsx-archive`（delivery `both`，见 `.opencode/commands/`） |
| 跨会话工作记忆 | `Docs/journal/` —— 单一存储（ADR-019）。`NEXT.md` + 每日文件（session-journal skill，本文件 §8 仪式）；超长天数 → `Docs/journal/archive/`（ADR-021）。**绝不复制**进 harness 原生 memory |
| 路线图与设计提案 | [Docs/planning/](./Docs/planning/) |
| 历史修复报告（非当前事实） | [Docs/archive/](./Docs/archive/) —— 日期前缀 `YYYY-MM-DD-NAME.md`，按月分桶（`2026-03/` 等） |

---

## 5. 代码风格与约定

- C# 12 / .NET 8.0，启用可空引用类型（NRT），Allman 花括号，4 空格缩进，UTF-8。
- 命名：类型 `PascalCase` · 接口 `I`+`PascalCase` · 方法 `PascalCase`（异步加 `Async` 后缀）· 属性 `PascalCase` · 字段 `_camelCase` · 参数/局部变量 `camelCase` · 事件处理器 `On[EventName]`。
- 结构：`Core/`（接口、基类型）· `Plugins/Core/`（必需：SecretFill、Hotkey）· `Plugins/`（扩展）· `Services/` · `ViewModels/` · `Views/`（XAML）· `Helpers/` · `Models/`。
- **DI**：构造函数注入，在 `App.xaml.cs` 中注册。插件运行时 = 基于 `PluginRuntimeKernel` 的三个窄接缝（ADR-012）—— 注入**最窄的那条接缝**，绝不要具体类：
  - `IPluginRegistry`（注册/发现/激活/查询）· `IPluginExecutor`（`ExecuteAsync`）· `IPluginRuntimeOps`（重新扫描/停用/授权/卸载）。
- 熔断器是**纯状态机**（ADR-013）：不注入 `ITrayService` / `IPluginHealthMonitor` / `ILocalizationService`；它只发出 `Tripped` / `Recovered` 事件；副作用归属 `PluginBreakerNotificationService`（在 `AppStartupCoordinator` 中于托盘初始化后激活）。
- 执行关联：`PluginExecutionContext.Current`（栈作用域 AsyncLocal，Dispose 时还原）。
- 线程安全：使用 `ConcurrentDictionary`；热键动作通过 `Dispatcher.InvokeAsync()` 执行。
- MVVM：`[ObservableProperty]` / `[RelayCommand]`（CommunityToolkit）。异步：`async Task`，避免 `async void`。
- 错误处理：对易失败操作包裹 `try/catch`；使用 `ILogger<T>`（**绝不用** `Debug.WriteLine`）。
- 原生互操作：`LibraryImport` / `DllImport` 放在 `NativeMethods` 类中。

---

## 6. 常见工作流

- **新增服务**：在 `Services/Interfaces/` 定义接口 → 在 `Services/` 实现 → 在 `App.xaml.cs`（`ConfigureServices`）注册。
- **新增插件**：选定分层 → 继承 `PluginBase<T>` → 构造函数注入依赖 → 实现 `ExecuteAsync()` 与元数据 → 只使用 `PulsarContext`。示例：`Pulsar/Pulsar/Plugins/Extensions/Command/CommandPlugin.cs`。深入阅读：[PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md) · [Docs/guides/PLUGIN_MIGRATION_GUIDE.md](./Docs/guides/PLUGIN_MIGRATION_GUIDE.md)。
- **新增对话框**：在 `ViewModels/Dialogs/` 实现 `IDialogViewModel` → 在 `Views/Dialogs/Contents/` 建 UserControl → **在 `Themes/DialogTemplates.xaml` 注册 DataTemplate** → **在 `Services/DialogCatalog.cs` 登记一行（id + 标题键 + 尺寸预设 + 按钮 + 主题）** → 用 `DialogService.ShowCustomAsync(DialogId.X, content)` 展示。三条链路由 `DialogCatalogTests` / `DialogTemplateRegistrationTests` 守卫。深入阅读：[Docs/architecture/DIALOG_SYSTEM.md](./Docs/architecture/DIALOG_SYSTEM.md)。
- **新增设置页**：常驻页 → 在 `SettingsPageCatalog` 注册 + 工厂创建器 + resx 标题键；瞬态页（动态页签，由卡片的「配置详情」按钮按需打开）→ 在 `App.xaml.cs` 通过 `ITransientPageService.RegisterDefinition` 以 `isTransient: true` 定义（单例，干净离开时自动回收）。保存流程仍走 `SettingsEditorSession`（`MarkDirty()` → 窗口级 Save）。深入阅读：[029-settings-transient-pages.md](./Docs/decisions/029-settings-transient-pages.md)。
- **修改 UI（XAML）**：在 `Views/` 找到视图 → 绑定到 VM → 从 `Themes/Theme.*.xaml` 取 `StaticResources` → 在 `InitializeComponent()` 后调用 `ApplyTheme()` → 使用 Pulsar 按钮样式。深入阅读：[Docs/guides/UI_BEST_PRACTICES.md](./Docs/guides/UI_BEST_PRACTICES.md)。
- **凭据（Secret Fill）**：`SecretFillPlugin`（`Plugins/Core/SecretFill/`）+ `CredentialsManager`；敏感数据模型上加 `[JsonIgnore]`。

**条件加载（Conditional loading）**（ADR-022）：场景化指令（测试 / 发布 / openspec）应放在 skills 或 `.opencode/commands` 斜杠命令中，**不要**塞进这份始终加载的文件。

---

## 7. 错误处理与日志（Pulsar Sentinel）

- **Serilog** 结构化日志 → `%AppData%\Pulsar\Logs\pulsar-yyyyMMdd.log`；在 `App.xaml.cs` 中通过 `.AddSerilog()` 注册。
- **全局兜底**（`App.xaml.cs`）：1) `DispatcherUnhandledException`（UI 线程，记 Fatal）2) `UnobservedTaskException`（后台任务，记 Error）3) `AppDomain.UnhandledException`（灾难性，记 Fatal）。
- 用法：构造函数注入 `ILogger<T>`；在 catch 块中使用 `LogInformation` / `LogError(ex, ...)`。
- **熔断器**：扩展插件在 1 分钟内崩溃 3 次会自动禁用 60 秒；通过 `ITrayService.ShowNotification`（toast）通知用户；冷却结束后进入半开（Half-Open）状态（单次重试）。

---

## 8. Agent 行为准则与 AI-First 开发

### 会话开始仪式（强制，在做任何实际工作之前）
- **先读 journal（最小切片）**：执行 `session-journal` skill 流程（`.agents/skills/session-journal/SKILL.md`，镜像于 `.opencode/skills/session-journal/SKILL.md`）：读 `Docs/journal/NEXT.md` + 最新 `YYYY-MM-DD.md` 的尾部（最后一个 `## Session` 块；每日文件有大小上限，ADR-021），向用户总结未完成的「下一步」；未经确认，不要启动与未完成条目相矛盾的工作。
- **该 skill 可能不在宿主的注入技能列表中** —— 按上述路径定位并先阅读再行动；仪式的依据是 `Docs/journal/` 与 ADR-019，而不是宿主的技能列表。
- **会话结束**：追加一个 `## Session (HH:MM)` 块（做了什么 / 关键决策·坑 / 相关引用，**≤ 约 25 行**）并更新 `Docs/journal/NEXT.md`。**只追加**，绝不重写或删除历史条目。
- **同时扫一眼 `openspec/changes/`**，若有进行中的变更，在相关时提及。

### 「AI 编程三角」（做功能 / 修 bug 时必须遵守）
1. **隔离副作用（Everything is Mockable，一切皆可打桩）**：绝不让代码与 OS API 硬耦合（`SendKeys`、`Process.Start`、`File.Write`、注册表、UI 自动化）。定义接口（`IInputSimulator`、`IClipboardMonitor`、`IProcessLauncher`）+ Windows 实现；在 `Pulsar.Tests` 中用 `Moq` 验证。
2. **ViewModel 单元测试（重状态而非 UI）**：状态转换在不触碰 XAML 的前提下可验证。测试放 `Pulsar.Tests/ViewModels/`（xUnit）；以编程方式调用命令并断言状态。
3. **无头执行与自纠错**：脱离 WPF 外壳运行插件：`dotnet run --project Pulsar/Pulsar.Simulator/Pulsar.Simulator.csproj -- --plugin "com.x" --args "{...}"`；反复迭代直到 JSON 输出为 `"Success": true`。

**标准序列**：1. 接口 + 失败的测试 → 2. 实现 → 3. `dotnet test` + simulator 跑到全绿 → 4. 绑定 XAML → 请人类做视觉 QA。

### 通用 Agent 规则
- **主动性**：发现明显问题（如缺失 null 检查）时顺手修掉。
- **上下文**：编辑前务必先读文件，以保留局部约定。
- **安全**：绝不提交密钥或 API Key。
- **验证**：改动后运行 `dotnet build`。
- **文档**：架构性变更要同步更新相关文档。
- **靠日志调试**：先沿调用链加 `ILogger` 调试语句；先从 Serilog 输出（`%AppData%\Pulsar\Logs\pulsar-yyyyMMdd.log`）定位，别先猜。

### 交付闸门（Delivery Gate，任何非平凡交付之前）

两项可验证动作，非平凡变更（一次修复、重构、ADR 或主张）**必须同时满足**。「非平凡」= 触及 1 个以上文件，或断言了行为 / 性能类结论。打字错误、单行查询可跳过。

1. **最强反方（最强反方）** —— 在报告「完成」之前，在回复中回答：
   - 这个修复**可能怎样失败**？写出那个反对意见的**最强版本**，不要写稻草人版本。
   - 该方案**隐含假设了什么**？哪一个假设一旦不成立，整套东西就崩？
   - **测试没覆盖什么**？（现有测试套件的盲区）
   *缺少这三项的「完成」报告是不完整的。*
2. **三段标注（三段标注）** —— 每条论断都要区分**事实 / 推断 / 假设**：
   - 【已验证】本次运行实测 —— 引用命令 / 数字（例如 `全量 1491/1491`）。
   - 【推断】基于证据推理而非直接测量（要说明证据是什么）。
   - 【假设】看似合理但未验证 —— 明说，并交出复现步骤 / 下一步。
   *把既有的「禁止编造测试数字」规则，从一条禁令升级为积极的标注动作。*

> 依据：其余推理原则已天然内嵌于此 —— 第一性原理 = §2 铁律，消融实验 = ADR/lesson 机制，奥卡姆剃刀 = ADR-022 瘦身，批判思维 = `grilling` skill。只有这两项缺少挂载点，且二者都能在交付物中被检查。

---

## 9. 常用命令

**推荐**：`scripts/dev.ps1` —— 封装 build/test/commit，自动修复沙箱 shell 剥离的 Windows 环境变量（解决 NuGet `Value cannot be null (path1)` 崩溃）。在 bash 中调用：

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev.ps1 build
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev.ps1 test                          # 全量测试（约 23 秒）
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev.ps1 test --filter "FullyQualifiedName~HotkeyService"
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev.ps1 commit -Message "..." [-All]  # -All = 含未跟踪文件（git add -A）
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev.ps1 all                           # build + 全量 test
```

直接命令（沙箱 shell 可能需要 env 前缀变通写法 —— 见 `scripts/dev.ps1` 头部注释）：

```bash
dotnet build Pulsar/Pulsar/Pulsar.csproj
dotnet run --project Pulsar/Pulsar/Pulsar.csproj
dotnet restore Pulsar/Pulsar/Pulsar.csproj
```

**完整参考**：[Docs/ops/BUILD_AND_RUN.md](./Docs/ops/BUILD_AND_RUN.md)

---

## 10. 并行 Agent 与 Git Worktree 纪律（并发）

本仓库会有多个 AI harness / agent 并行工作。以下规则用于避免跨 Agent 互相覆盖、工作树过期、journal 分叉（ADR-021）。

- **每个并发 Agent 一个独立 worktree，各自一个分支。** 绝不在同一工作目录下跑两个 Agent。主 worktree = 集成分支 + journal 维护者。示例：`git worktree add -b feat/<name> E:\...\Pulsar_Project_wt`。
- **分支纪律**：一个 worktree 对应一个分支；绝不把同一分支签出到两个 worktree（会造成 HEAD 过期）。完成后 rebase 到 `main` 并快进合并（或发 PR）。保持 `main` 常绿。
- **构建 / 测试隔离是免费的**：每个 worktree 有自己的 `bin/obj`；共享的 `.git` 对象与 NuGet 缓存是读安全的 → 并行的 `dotnet build` / `dotnet test` 不会互相破坏。
- **`Docs/journal/` 与 `CHANGELOG.md` 只在 `main` 上提交。** 功能 worktree 通过 `git fetch` + `git show main:Docs/journal/…` 读取它们来完成会话仪式；绝不在自己分支上提交分叉副本。journal 是 append-only 的，因此即便偶发分叉也能干净合并。
- **写 journal 前 / 合并前**：`git status` + `git fetch`；确认没有其他 harness 的未提交改动。
- **访问方式**：Agent 可通过绝对路径在任意 worktree 内操作（`git -C <worktree> …`）。

---

## Agent skills

> **这两个文件是机器消费的技能契约，不是阅读材料。** 由 `.agents/skills/` 下 vendored 的技能（尤其是 `setup-matt-pocock-skills`、`domain-modeling`、`grill-with-docs`、`improve-codebase-architecture`）读写。不要移动或删除 —— 路径是硬编码的；重跑 `setup-matt-pocock-skills` 会重建它们。

- **议题跟踪**：Issues 与 PRD 以 GitHub issues 形式存在；参见 [`Docs/agents/issue-tracker.md`](./Docs/agents/issue-tracker.md) —— 面向技能驱动议题操作的 `gh` CLI 约定。
- **领域文档**：单上下文 —— 仓库根目录的 `CONTEXT.md` + `Docs/decisions/`（**不是**小写的 `docs/adr/`，那是上游多上下文默认值）；参见 [`Docs/agents/domain.md`](./Docs/agents/domain.md)。

---

*译版日期：2026-09-10*
*对应原版版本：v4.0.0（规则栈瘦身 ADR-022：§3 陷阱表 → Top-5 + 指向 Docs/lessons 的指针；§5/§6/§7 收紧；新增条件加载原则。铁律 §2、路由 §4、仪式 §8、dev.ps1 §9、worktree 纪律 §10 均保留。）*

<!-- OPENWIKI:START -->

## OpenWiki

本仓库含有一份自动生成的 `openwiki/` 证据索引。它是可选的即时上线文（just-in-time context），不是启动必读材料。

- 以源码和测试为准。brief 中的未知项与 review 条目属于**验证缺口**，不是自动成立的需求。
- 优先选择能证明该行为变更的**最窄**静默验证方式。保留完整的失败输出。

计划的 OpenWiki GitHub Actions 工作流会刷新仓库 wiki。除非明确要求，不要手工编辑 OpenWiki 生成的页面；应优先更新源码 / 文档，让 OpenWiki 自行重新生成。

<!-- OPENWIKI:END -->
