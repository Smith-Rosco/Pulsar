# Changelog

All notable changes to Pulsar are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

<!--
  新增条目模板：
  ## [Unreleased]

  ### Added
  - ...

  ### Changed
  - ...

  ### Fixed
  - ...
-->

## [Unreleased]

### Added
- 暂无

### Changed
- 插件运行时宽门面拆分为三个窄 seam（ADR-012，架构审查候选 A）：`IPluginRegistry` 收缩为注册面（发现·激活·查询，8 方法），`ExecuteAsync` 移入新执行面 `IPluginExecutor`，重扫/停用/状态/授权/卸载移入新运维面 `IPluginRuntimeOps`；三个接口由同一 `PluginRuntimeKernel` 单例实现并经 DI 注册。透传包装类 `PluginRegistry` 删除，执行热路径/生命周期编排/设置页各自改注入最窄 seam。
- 熔断策略去 UI/遥测依赖（ADR-013，架构审查候选 D）：`PluginCircuitBreakerPolicy` 收敛为纯状态机（构造仅 `ILogger`），打开/恢复经 `Tripped` / `Recovered` 事件广播；新增 `PluginBreakerNotificationService` 观察者 adapter 订阅事件并把迁移转成健康遥测记录与本地化托盘通知，启动协调器在托盘初始化后解析激活。文案与行为保持与迁移前一致。
- 可回收 ALC 卸载不变量收口（架构审查候选 E）：`PluginLoader.TryUnloadExternalContext` 现在一次性完成 `Unload()` 发起 + 强制 GC 泵（`GC.Collect`×2 + `WaitForPendingFinalizers`），调用方（`PluginRuntimeKernel.DeactivatePluginAsync`）不再内联 GC 序列，只负责调用前的引用切断。
- 插件清单解析收敛为单一事实来源（架构审查候选 C）：新增 `PluginManifestReader`（static），把「`plugin.manifest.json` → 回退 `manifest.json`」文件名解析与大小写不敏感反序列化收口为一处，四处内联复制（`PluginLoader.TryReadExternalManifest`、`LocalPluginScanner.ScanInstalledPlugins`、`PluginPackageManager.HasValidManifest`/`ReadAndValidateManifest`）改为调用共享 reader。Id 空判定、权限 token、版本兼容与各自的失败消息仍留在调用方错误层，语义逐字不变。
- 插件卡片能力声明进 metadata，通用 VM 移除插件 ID 特判与 service-locator（ADR-015，架构审查候选 F）：`PluginCapabilities` 新增四个默认 false 的 UI 能力标志（`SupportsScriptEditor` / `HasBuiltinExamples` / `HasCustomConfigDialog` / `SupportsWindowInspector`），由 WinSwitcher（自定义配置对话框 + Window Inspector）与 Web Scripts（脚本编辑器 + 示例库）在各自 `GetMetadata()` 自述。`PluginViewModel` / `PluginSettingsDialogViewModel` 改按能力分支，并把 `IServiceProvider.GetService<T>()` 隐藏依赖改为构造显式注入（窗口/进程注册表/脚本文件/脚本校验/示例库/日志）；两个 Manager VM 与 `ExternalPluginViewModel` 同步删除 provider 透传。行为与改前一致，未声明能力的插件（含全部外部插件）渲染不变。
- 运行时状态存储的读操作改为纯读（ADR-016，架构审查候选 G）：`PluginRuntimeStateStore.GetSnapshot` 对未知插件的回退快照不再 `TryAdd` 落缓存，快照字典只由通过校验的 `Transition()` 写入——读查询不再带写副作用，被拒绝的非法转移（如对未注册插件 `Transition(..., Running)`）不再留下默认快照痕迹。验证后否决了报告建议的「拆 `PluginRegistry` + `LifecycleStateMachine` 两模块」方案：该 store 是深模块（双私有字典的配对不变量被封装在 6 方法小接口内），拆分只会把协调成本外移并连带重写全部调用方，收益为零。

### Fixed
- 清除全部 32 条基线编译器警告（**0 警告基线达成**，此前「32 警告全部来自基线文件」清零）：`Pulsar.E2E/Driver/Recorder.cs` 事件处理器签名补 `object?`（CS8622）；`AppStartupCoordinator` 9 个可选 ctor 参数与 2 个 debug factory 字段补可空注解（CS8625/CS8619 系）；`AppStartupCoordinatorTests` 3 个 late-init 属性补 `= null!`（CS8618）、debug factory 局部变量补 `?`、`dispatcherProvider` 缺省分支补 `!`（CS8603/CS8604 系）。纯注解修改，无行为变更。来源为并行会话/IDE 的外部改动，经构建 + 定向（AppStartupCoordinatorTests 8/8）+ 全量（1059/1059）三重验证后落地。
- `PluginManagerViewModel` 声明 `IPluginRuntimeOps` 字段但构造器从未注入（ADR-012 迁移遗留，运行到插件管理页即 NRE/破坏 0 警告基线）；构造器现补上 `runtimeOps` 参数并赋值。
- **ADR-018 一致性修复（候选 I 回归）**：候选 I 初版实现与 ADR 声明相反——非法组合 `OnboardingState="Complete"` + `HasCompletedTutorial=false` 声明为「return（自愈）」，实际 gate 却落入 tutorial 分支再次进教程（根因：ADR problem statement 把旧内联代码 branch 3 的恒 return 读成了「再次进教程」，实现照抄了错误声明）。修复于投影层：`OnboardingState` 投影对终态 `"Complete"` 自愈 `HasCompletedTutorial = HasCompletedTutorial || onboardingState=="Complete"`，使非法组合投影为 `(true, true, false)` → gate 返回。经三源证据核实（gate 代码 / 原始投影 / 旧内联检查 git diff）并完成消费者审计：投影 `HasCompletedTutorial` 的唯一调用点即 gate，6 种合法组合映射全部不变。`OnboardingVerificationTests` 中锁定旧错误机制的断言反转为 `ShouldHealCompletedTutorial`。详见 `Docs/decisions/018-*.md` Amendment（2026-09-04）。

### Architecture review (round 2)
- **架构（H）**：`RadialMenuViewModel` 不再实现 `IMenuSession`；4 个零引用成员（`IsInSubMenu`/`SetActionExecuted`/`RestoreRootMenu`/`EnterSubMenuAsync`）删除；保留 `IsVisible`（去掉 setter）/ `ActionExecuted` / `IsFlickOutEscaped`（XAML DataTrigger 依赖）。`IsVisible` setter 无人调用，去除。
- **架构（J）**：`MenuSession` 的 `GestureReleaseFadeDelayMs = 180` 与 `RadialMenuWindow.Dismiss` 的 `160ms` 淡出合并为新 `ViewModels/MenuTiming` 静态类（`DismissFade=160`、`DismissGraceMs=20`、`DismissAwait=>180`），把「180 ≥ 160」这条跨模块不等式显式命名为 `DismissGraceMs`。SlotOrb 的 300/320 hover 时长不属于此契约，保持原样。修正 `RadialMenuWindow.xaml.cs:207` 自相矛盾的注释（"slightly slower than 320" 与 "160" 矛盾）。
- **架构（K · ADR-017）**：`AppStartupCoordinator` 的 `IServiceProvider` 字段删除，24 处 `GetRequiredService/GetService<>` 全部替换为构造注入或 `Lazy<T>` / `Func<T>` 工厂。`App.xaml.cs` 新增 11 个工厂注册。保留 ADR-013 时序（中继在托盘初始化后解析）、`--ui-debug` 输入门禁（`GlobalKeyboardHook` 不在 ui-debug 下预解析）、transient VM 防捕获（`FirstLaunchSetupWizardViewModel` 走 `Func<>` 工厂）。
- **架构（I · ADR-018）**：`AppStartupCoordinator.StartDeferredInitialization` 的 3 行内联首次启动判定替换为 `IOnboardingStateService.GetStateAsync()` 投影读取；读端不再绑定 `OnboardingState` 的 4 个字符串字面量。非法组合 `OnboardingState="Complete"` + `HasCompletedTutorial=false`（`ProfilesConfig.cs:354-357` 文档化的非法不变量）语义保持「return（自愈）」不变——ADR-018 problem statement 曾把旧内联代码 branch 3 读反（旧代码 `!Equals("Complete","SetupWizardComplete")`=true 恒 return，本就是自愈的），候选 I 初版按该错误声明实现成了进 tutorial，已回正（见 Fixed）。6 种合法组合 → return 条件映射不变。`OnboardingVerificationTests` 新增 4 个测试锁 `HasCompletedSetup` 在 `SetupWizardComplete`/`Complete` 上的投影、非法组合自愈、`LastTutorialStep="Skipped"` → `HasSkippedTutorial` 映射、`OnboardingState="Complete"` 无条件短路。
- **测试（K · ADR-017/018）**：`AppStartupCoordinator` 首次获得单元测试（`Pulsar.Tests/Services/AppStartupCoordinatorTests.cs`，8 个）：2 个 ctor null-guard、生产启动顺序（CallOrder）、`--ui-debug` 下 `GlobalKeyboardHook` 不预解析、`--ui-debug-hooks` 的 publisher-before-window 时序、ADR-013 中继在托盘初始化后解析、非法 onboarding 组合被 gate 拒绝、合法状态经投影自愈后到达 tutorial（经 RecordingLogger 断言生产代码内部 catch 吞掉的 NRE，验证错误隔离）。技术要点：Moq 无法代理 concrete 类 → 真实实例/throwing factory；WPF 单测断言边界限定在 `MainWindow.Show()` halt 之前可观测的信号（Hotkey/Mouse 解析在其后，单测不可达）。

- **架构（O）**：`GlobalKeyboardHook.UseHybridMode` 的写点从 `AppStartupCoordinator.ConfigureKeyboardHookAsync` 收回 `HotkeyService.InitializeAsync`（订阅前先 `ConfigureHookMode()`），按键 module 自己持有"hook 该用什么模式"的知识；协调器的 -14 行方法与 `App.xaml.cs` 的 `Lazy<GlobalKeyboardHook>` 工厂删除。顺带修复启动时序窗口：原顺序是先订阅（hotkeyService 初始化）后写模式（协调器），`IsHybridMode=false`（Legacy）用户启动头几百 ms hook 以默认 Hybrid 运行——现在写入先于订阅，时序窗口关闭。新增 HotkeyService Hybrid/Legacy/缺配置 fallback 3 个测试；hook 解析链 CallOrder 语义不变。
- **架构（N）**：`PluginSlot` 分离的 6 个死展示成员删除：`QuickEditBadgeText` / `SummaryFallbackText`（全仓零消费者，含 XAML）与仅剩单调用点的 `GetLoc()` 整体拔除；`SetValidationSummary` 签名改为 `(summary, ValidationSeverity)` 显式传入——修复从 message 字符串反推 severity 的漏分类活 bug（"expects {type}" / "Validation failed" 两类错误此前显示为 Warning），同时消除将来 validation 消息本地化时字符串反推全盘失效的定时炸弹。新增 `RefreshSlotValidationSummaries_SeverityTravelsWithType_NotFromMessageKeywords` 回归测试。
- **架构（M）**：Settings 对话框流配方收口单一 owner `ViewModels/Settings/SettingsDialogFlows.cs`（`RunAsync<TViewModel>` 异步/同步双变体 + `RunConfirmationAsync` 双变体）。`SettingsViewModel` 13 处对话框调用点中 9 处迁移（6× RunAsync + 3× RunConfirmationAsync），4 处按 M-Q2 两档判定保留直调并注释理由：ResetConfig（4 参自定义按钮文案）、PickSecret（`SelectedSecretId` 协议非 DialogResult）、OpenSlotConfiguration（纯 show）、PickIcon（else 恢复分支语义配方不建模）。新增 10 个配方直测锁「show → Confirmed → delegate → 不 Confirmed 不调」。
- **架构（L · ADR-020）**：右拖拽手势编排（≈465 行：字段区 + `RefreshGestureConfig`/`ApplyGestureConfig`/`ApplyPendingGestureConfig`/`BuildIsolationSettingsSnapshot`/`FeedRightDragGesture`/`ResolvePendingGestureUp`/`OnGlobalMouseMove`/`IsModifierHeld`）从 `RadialMenuViewModel` 整体迁入 `MenuSession`，[DEBUG-RDX] 日志逐字保留，D2/D3/D4/LEAK-FIX 语义不变。VM 收缩为两个薄转发（`OnGlobalMouseEvent` → `session.FeedRightDragGesture`、`OnGlobalMouseMove` → `session.FeedGlobalMouseMove`），ADR-008 决策 2（VM = input-source adapter + binding projection）收尾完成。`RefreshGestureConfig` 并入 `session.RefreshConfig`/`Initialize`；session ctor 新增 3 个可空依赖（`IGestureIsolationService?`/`IGlobalMouseService?`/`Action<RadialMenuMode>?` 渲染预热回调，组合根惰性解析 VM 注册）；`IUiDispatcher` 增 `InvokeWithInputPriority`（D4 输入优先级）；9 个测试文件的私有 `DirectUiDispatcher` 收敛为 `TestHelpers/DirectUiDispatcher.cs`；Isolation/Leak 手势测试迁移为 session 面直驱。

- **测试（K 修复 · 全量套件死锁）**：K 测试落地后全量 `dotnet test` 必挂死（testhost 零输出，15+ 分钟无进展；并行/串行皆挂，定向跑 19/19 秒过）。根因：`AppStartupCoordinator` tutorial 分支内联访问 `System.Windows.Application.Current.Dispatcher`，测试 #7 依赖「宿主中 `Application.Current` 为 null → NRE」这一进程全局假设；先跑的 `ThemeServiceTests` 等四个测试类 `new Application()` 留下非 null 的 Current（xunit collection 隔离不隔离 AppDomain 静态），其 Dispatcher 永不泵消息 → `InvokeAsync` 排队后永不完成。修复：ctor 新增可选 seam `Func<Dispatcher> dispatcherProvider`（默认 `() => Application.Current?.Dispatcher`，生产语义等价；MS.DI 类型注册经可选参数默认值自动生效，App.xaml.cs 无改动），测试注入固定 `() => null` 使断言确定化。详见 `Docs/lessons/XUNIT_APPLICATION_CURRENT_DEADLOCK.md`。

### Verified
- 构建 0 错误（NU1900 网络警告与基线一致；CS8625 在 ADR-017 引入的 `Lazy<T>=null` 默认参数上基线即存在，未新增）
- `dotnet test Pulsar.Tests` → **1045 / 1045 通过**（基线 1037 + 8 个 `AppStartupCoordinatorTests`；默认并行 22s——含上述全量死锁修复）
- L/M/N/O 落地后全量回归：**1059 / 1059 通过**（基线 1045 + HotkeyService hook 模式 3 + SlotEditor severity 1 + SettingsDialogFlows 10；构建 0 错误、改动文件零新增警告）
- 警告清理落地后：构建 **0 错误 0 警告**（32 条基线警告清零）；全量 **1059 / 1059 通过**（默认并行 ~22s，与 O/N/M/L 后基线一致）

### Docs & conventions
- **工作记忆统一为单源（ADR-019）**：`Docs/journal/` 成为所有 AI harness（WorkBuddy / opencode / 未来 harness）唯一跨会话工作记忆；禁止向 harness 原生记忆（`.workbuddy/memory/` 等，gitignored）重复写入正文，最多一行指针。历史回填：2026-09-01~03 自 `.workbuddy/memory/` 无损迁入 `Docs/journal/`，2026-09-04 独有内容（UI 自动化调研 + visual-ai-ui-automation 落地）并入当日 journal，gitignored 原件清理。journal 永不删除（过期归档走 `Docs/archive/`）；正文语言以中文为准（CONTRIBUTING 语言规则对工作记忆豁免）。`session-journal` skill 双份（`.agents/skills/` 与 `.opencode/skills/`）同步为同一规范，AGENTS.md / CONTRIBUTING 同步更新。
- **journal 体积上限 + worktree 并行纪律（ADR-021）**：会话仪式改为只读 `Docs/journal/NEXT.md` + 最新日文件**尾部**（最后一个 `## Session` block），不再整文件读取——单日文件按 ~15 KB 上限，超限经 `git mv` 原样归档至 `Docs/journal/archive/` 并开新指针文件（09-03 / 09-04 已归档）；NEXT.md 成为跨会话「下一步」单一权威，条目块 ≤25 行。并行开发纪律：每 agent 独立 worktree + 独立分支，`Docs/journal/` 与 `CHANGELOG.md` 仅在 `main` 提交（feature worktree 经 `git show main:…` 读取），构建/测试隔离免费（各 worktree 独立 `bin/obj`）。AGENTS.md §8/§10 与 `session-journal` skill 同步更新。

## [1.10.0] - 2026-09-04

### Added
- 首页新增办公自动化入口，高频办公动作一键直达
- 新增办公动作预设包，支持安装/卸载生命周期与首次使用引导
- 书签脚本新增应用内脚本编辑器
- 书签脚本新增示例库导入流程与编辑器集成
- 教程新增 Web 脚本示例库与引导场景

### Changed
- 优化设置页布局、未保存状态徽标与过渡动画

### Fixed
- 修复书签脚本编辑器按钮样式解析问题（自动合并按钮样式模板）
- 插件设置页拆分内置/外部插件页签，外部插件获得完整管理能力
- 插件页名称、描述、分类与健康状态全面本地化

## [1.9.1] - 2026-09-03

### Added
- **渲染器插件化**（roadmap 方向二延伸）：`IRadialRendererRegistry` 注册表（owner 归属、内置 id 防遮蔽、`ui.render` 权限门控、`Changed` 失效事件）；`StyleRendererFactory` 解析顺序 = 注册表 → 内置 DI 集 → Default 兜底；插件禁用/卸载时自动注销其渲染器；设置页渲染器下拉动态枚举插件贡献项；新增权限令牌 `ui.render`。附 `Pulsar/Samples/NeonRendererPlugin` 样例插件（虚线霓虹环 + 模糊高亮）与 QA 清单（`openspec/changes/renderer-plugin-registry/qa-checklist.md`）。
- 插件运行时停用链 `IPluginRegistry.DeactivatePluginAsync`：`OnUnloadAsync` → 移除运行时状态与 catalog 条目 → 注销渲染器贡献 → 失效发现缓存 → 卸载插件程序集上下文（释放 DLL 文件锁）。
- 外部插件启用/禁用开关：外部插件管理器页新增 `ToggleSwitch`（`PluginPackageInfo.IsEnabled` + `TogglePluginCommand`），立即生效——启用即激活插件，禁用跑 `OnDisableAsync` 并无条件注销其渲染器贡献（回落 Default）。
- 发布流程支持本地构建号 `x.y.z.n`（`-Build` 参数），版本号不写入 csproj；产物内附 `build-info.txt`（版本 / 构建号 / channel / 时间 / commit）。

### Changed
- **产品定位叙事调整（重定位 M0）**：README 首屏由"生产力启动器"改为"重度办公效率工作台 · 驯服老旧办公系统"，简介与功能叙述重排——办公自动化三支柱（一键宏 / 老旧网页脚本 / 安全填表登录）前置，并新增 `Docs/reports/` 报告区（市场评估 + 重新定位方案）。
- **网页脚本插件更名**：BookmarkletRunner 显示名 `Browser Scripts` → `Web Scripts`（zh-CN：浏览器脚本 → 网页脚本），描述更新为老旧内网网页定位，本地化键 `Plugin.Name/Description.*` 同步迁移（新增派生描述键，保证中英键集对齐）。
- **README 徽章修正**：Release v1.8.0 → v1.9.1；Tests 330+ → 897（2026-09-03 实测）。
- 将右键手势路径的 28 处 `[DEBUG-RDX]` 诊断日志由 `LogInformation` 降级为 `LogDebug`（`RadialMenuViewModel` 19 / `GlobalMouseHook` 8 / `GlobalMouseService` 1），消除生产环境「每条鼠标事件写一条 Information 级日志」的开销与日志膨胀。诊断信息完整保留，排查时开启 Debug 级别即可。
- 本地化收敛决策：明确 Pulsar 仅适配中英双语（en + zh-CN），不做 zh-TW / ja。对 `Strings.zh-CN.resx` 做全量校验：与 EN 1037 键逐一对齐、占位符零错配、无空值；移除孤儿键 `Plugin.Bookmarklet.MissingScriptPath`（代码实际引用 `Bookmarklet.Error.MissingScriptPath`），两语言键集现已完全一致。
- 设置页渲染器下拉文案明确化（「渲染器（径向菜单样式）」），描述中提示插件会追加选项。
- 发布技能重构为可独立运行的 PowerShell 脚本；`Pack-Zips` 增加三级 zip 回退（pwsh → powershell → System32 bsdtar），每级做 PK 魔数校验。
- `Set-ProjectVersion` 同步更新 `<FileVersion>` / `<AssemblyVersion>`，修正 exe 文件属性版本长期停留在 1.8.0.0 的问题。
- 新增 `Update-Changelog.ps1`：把 `[Unreleased]` 固化为版本段，`New-ReleaseTag` 的版本提交现包含 CHANGELOG。
- `Get-ReleaseInfo` 从 origin remote 解析仓库地址，不再硬编码 `Smith-Rosco/Pulsar`；tag 选择由创建时间最新改为 semver 最大。

### Fixed
- **运行时安装后插件不激活**：安装流程的授权调用早于发现刷新，被 "unknown plugin" 静默拒绝，`Profiles.json` 插件区保持为空。现在安装成功后先刷新发现、再授权、再立即激活，无需重启应用。
- **运行中卸载/覆盖安装外部插件失败**：发现阶段加载的插件 DLL 从未卸载导致文件锁定；部分卸载残留的无 manifest 目录死锁（列表不可见但安装被挡）。现在卸载走完整停用链（含程序集上下文卸载）后删文件，安装自动清理残骸目录，目录删除带重试。
- **重启后外部插件贡献消失**：外部插件启动时只发现不激活（懒激活），渲染器等在 `OnEnableAsync` 里注册的环境贡献在每次重启后静默丢失。现在启动协调器在延迟发现后立即激活所有已启用的外部插件。
- **卸载偶发「Access to path … denied」**：collectible 程序集上下文的卸载是 GC 驱动的，`Unload()` 仅发起拆卸，DLL 文件锁要等 GC 真正回收上下文才释放。现在停用链在卸载上下文后强制 GC 回收，紧随其后的目录删除不再失败。
- **打开过「插件管理」页后卸载仍失败（descriptor 钉住 ALC）**：外部插件的 `ImplementationType` 是 collectible ALC 里的 `Type`，被 `PluginManagerViewModel` 的 descriptor 列表长期持有，即便强制 GC 也无法回收上下文。现在 `ImplementationType` 改为可置空，停用链在移除 catalog 条目前置空它，切断钉住引用。

---

## [1.9.0] - 2026-09-03

### Added
- **级联子菜单**（roadmap 方向三）：`PluginSlot.SubActions` / `SubSlotDescriptor` / `CascadeSubMenuDescriptor`；Ring 与 Fan（≤3 项、翼角 ±30°，>3 自动回落 Ring）二级布局与命中算法（`SubMenuLayoutEngine`）；二级动作编辑器 `SubSlotEditorRow`；钻入入口；按槽位类型智能注入默认子动作（`SmartSubActionDefaults`）。
- **多形态径向渲染器**（roadmap 方向二）：`Core/Rendering/` 渲染器契约 + `StyleRendererFactory`（未知 id 安全回落 Default）；内置 Default / ClassicRing / Glassmorphism 三套；3 套主题预设（MatchaForest / GlacialIce / MorandiMuted）+ 模式色调 token。
- **自定义图标库**（roadmap 方向二）：`IconHelper` 支持 SVG 路径数据（`Geometry.Parse`）；新增 `CustomIconStore`，持久化到 `%AppData%\Pulsar\CustomIcons\`，图标选择器支持导入。
- **手势外甩取消**（roadmap 方向一）：光标超出轮盘半径 × `GestureFlickOutRadiusMultiplier`（默认 1.5）即虚化取消；仅对右键手势唤出的菜单生效，热键唤出不参与。
- **手势进程隔离**（roadmap 方向一）：`GestureIsolationService` 支持白名单 / 黑名单双模态，并旁路 `Progman` / `WorkerW` / `Shell_TrayWnd`，避免桌面与任务栏误判为全屏。

### Changed
- 子菜单进入泛化为策略化描述符（`SubMenuDescriptor` / `StrategyId`），窗口切换子菜单降级为其中一种策略。
- 槽位编辑器可视化层重构（`slot-wheel-editor-architecture` / `slot-wheel-editor-visualization`）。
- 内置插件重命名为面向用户的显示名。

### Fixed
- 内置插件的显示名与描述补齐本地化。

### Docs
- 文档树整合与索引重建；新增 `Docs/roadmap/` 与仓库健康、用户体验评审快照。
- `opsx` 命令统一更名为 `openspec`。

---

## [1.8.1] - 2026-09-01

### Fixed
- 引导教程：解析窗口切换槽位路径、修复旧版配置、与实时热键对齐。
- README 快速开始锚点失效；仓库链接指向 `Smith-Rosco/Pulsar`。

### Changed
- 发布技能改为 CI 驱动，tag message 携带完整 release notes。

---

## [1.8.0] - 2026-09-01

### Added
- **右键拖拽手势修复**（roadmap 方向一 / 专项分析）：引入位移阈值 + 未达阈值重放（`GestureSummonMode` / `GestureDragThreshold`，默认 25px），根治「释放后意外把右键发给原始程序」；修复配置刷新导致的 `Reset()` 释放竞态；手势唤出与关闭改走 `DispatcherPriority.Input`，跟手度对齐热键路径。
- **可插拔径向渲染器契约**（roadmap 方向二）：`IRadialRenderer` / `IRadialThemeTokens` / `RadialThemeTokenSet` / `ModeToneTokenDecorator`，配套主题预设解析。
- **配置备份与恢复**：支持含密码保护的秘密项导出 / 导入（见 `Docs/guides/CONFIG_BACKUP_AND_RESTORE.md`）。
- 设置 → 常规新增日志级别选择器。
- 无障碍：图标按钮补 `AutomationProperties.Name`；导航容器键盘焦点收敛。

### Changed
- Fluent 设计对齐的 UX 重构（P0–P3）：间距 / 圆角 token 管线、统一 `EmptyState` 组件、清理僵尸 token。
- `WindowService` 深化重构（ADR-010）：注入式协作者、单一资格评估器接缝 `IWindowEligibilityEvaluator`、窗口捕获与图标抽取下沉为 `IWindowCaptureService`、库存一致性收敛到 `IWindowInventoryCoordinator`，并清理死代码。
- 菜单首帧加载改为单阶段：结构优先的窗口枚举。
- 默认语言改为 zh-CN（中文优先）。
- CI：构建 full 与 portable 两类产物并统一命名。
- 文档：README 改为中文主版本并新增英文镜像与 CHANGELOG；AGENTS.md 精简以降低每会话上下文占用。

### Fixed
- 切换模式缓存未命中时回退到实时窗口枚举。
- 槽位自动配色无法恢复。
- Fluent accent token 未解析导致按钮文字不可读。
- 选项卡切换时 `EmptyState` 按钮样式触发 `XamlParseException`。
- 自定义导航指示器在 DPI 变化 / 窗格折叠时错位。
- `ApplySettingsTheme` 空引用且阻塞 UI。
- `SlotsPerPageChangedMessage` 未走空安全的 `InvokeOnUiInput`。
- 激活槽位模糊淡出改为平滑过渡，不再突变为清晰圆环。
- 热键默认值与代码行为对齐（Command = `Ctrl+Shift+Q`，Switch = `Ctrl+Q`）。
- VbaRunner 通过工作簿 Normal 样式应用 DengXian 字体，使新建单元格继承。
