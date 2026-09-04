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

### Fixed
- 暂无

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
