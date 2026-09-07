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

### Changed
- **架构深化 C1（Execution Handoff）**：`IMenuSession` 新增 `BeginExecution(slot)`，把「记录执行 Slot + 立刻隐藏菜单」的顺序收归实现。此前策略层 5 处手写 `IsVisible = false`，其中仅 2 处配对 `SetActionExecuted`、且 `WindowSwitchStrategy` 未传 slot（导致 E2E 断言退化为按索引猜测）。现 3 处真正的动作执行走 seam，2 处导航类保持原样。
- **架构深化 C3（Visual State Coordinator）**：`UpdateVisuals` 的 9 参数（含 3 回调 + 1 布尔开关）收敛为单个 `VisualStateContext`；ADR-024 D8（中心身份保留）与 D11（cascade 期间抑制动态标题）的推导从 `MenuSession` 搬入协调器，语义不再两地重复。新增 `IRadialMenuVisualStateCoordinator` seam，该模块此前测试覆盖为 0。
- **架构深化 C2（部分）**：删除 `WindowService.IsProcessNameBlacklisted` —— 与 `WindowEligibilityEvaluator` 内实现逐字相同但**生产零调用**，仅被自身 8 条测试钉住。规则收敛到唯一实现并改为 public，测试改指。
- **架构深化 C6（推荐引擎时钟 seam 收口）**：`PluginRecommendationEngine` 的 `_clock` 成为唯一时间源——`CheckUnusedPlugin` / `CheckInactivePlugin` 的 3 处裸 `DateTime.UtcNow` 改走 `_clock().ToUniversalTime()`（时区语义不变：趋势日键仍用本地日期）。Unused/Inactive 阈值测试改为注入固定时钟，新增「近期使用不触发」反向守护，该 seam 由空壳变实。

### Fixed
- `WindowSwitchStrategy` 不再丢弃执行 Slot 身份（`SetActionExecuted(true)` 未传 slot），E2E「哪个 Slot 执行了」标签不再退化为索引解析。

## [1.11.0] - 2026-09-06

### Added
- **应用内自动更新**（ADR-025）：三级容灾检查 + 镜像故障转移下载 + SHA256 校验 + About 页 UI + 启动后台检查 + 托盘通知
- **安装包与便携版双形态**（ADR-026）：Inno Setup 安装器 + 自包含单文件便携版；`dev.ps1 publish` 一键产出
- **中英双语用户手册**：`Docs/manual/` 五章 + RELEASE_CHECKLIST
- **单实例互斥**：命名 Mutex 防止多开，第二实例激活已有窗口
- **设置页 UI 精简**：保存按钮图标+文字；长文本描述精简入 Tooltip

### Fixed
- **存量 CS8604 警告清零**：MenuSession 非 Task 分支传 `_lastContext` 补 `!`：`MenuSession.LoadPageContentAsync` 非 Task 分支向 `InsertCreatorSlot` / `IPageProviderFactory.CreateCommandPage` 传 `_lastContext`（`PulsarContext?` → 非空形参）产生 4 条 CS8604。该分支第 882 行已用 `_lastContext!.TargetProcessName` 解引用（null 会先 NRE），按文件既有约定补 `!`（MenuSession.cs:891/894）。**build 0 警告 0 错误，全量 1143/1143 通过**。注：stash 回溯确认该警告随 3f68c13（Ring 极坐标统一）引入，非本变更产物。

### Added
- **宣传截图 E2E 管线 + 发布资产目录（openspec 2026-09-05-repositioning-narrative-rollout Phase 2）**：可复现首屏用图管线——fixture `office-workbench-dark.json`（Dark + MatchaForest，8 commandMode 办公槽 + 6 switchMode + Fan 级联）、workflow `promo-release-screens.json`（34 步，set-invocation-point 居中 + 5 张截图）、一体化纯黑壁纸脚本 `run-promo-wallpaper.ps1`（设壁纸→MinimizeAll→跑 E2E→恢复原壁纸，保证干净背景）。E2E 基建：`DebugCommandServer` 增 `set-invocation-point` 命令（直接设定菜单召唤屏幕坐标，绕开光标 teleport 不可靠问题）、`InputDriver` 增 `MoveTo`（SetCursorPos P/Invoke）、WorkflowModel/Parser/Runner 增 `move-cursor` 步骤类型。产出 4 张 1920×1080 PNG 入 `Docs/media/release/`（01 主界面 / 02 轮盘呼出 / 03 Excel 跑宏瞬间 / 04 窗口切换子菜单），菜单像素质心 (1260,734) 确认居中。`.gitignore` 从 `Docs/media/` 改为 `Docs/media/*` + `!Docs/media/release/` 例外；`Docs/media/release/README.md` 明确三方边界（release/ 入 git / Docs/media/ 其他子目录忽略 / E2E artifacts/ 不入 git）+ 命名约定 + 再生步骤 + review checklist。promo-release-9 PASS 30.7s。
- **内置插件显示身份叙事对齐 + 回归测试（openspec 2026-09-05-repositioning-narrative-rollout Phase 1）**：三支柱叙事落地插件显示层——VbaRunner 描述升「一键跑宏」（自动化领衔）、PkiPlugin 升「DPAPI 加密保护、可编程安全登录动作」（凭据护城河，DPAPI 已核实 `CredentialsManager`）、BookmarkletRunner 补「老旧企业系统重复点击 → 一键动作」（老旧系统助手）；EN/zh resx（`Plugin.Description.WebScripts/ExcelMacros/AutoFill`）与 C# 描述同步，插件 Id/配置键/显示名（resx 键推导源）全部不动。新增 `BuiltInPluginDisplayIdentityTests`（12 用例：显示名→resx 键约定绑定防改名断裂、zh 叙事关键词、Id 稳定、EN C#↔resx 同步防两套叙事）；`Docs/Plugins/PkiPlugin.md` 标题对齐 AutoFill、`VbaRunner.md` 补显示名。**全量 1143/1143 通过，0 错误**（存量 CS8604 警告 ×4 为 MenuSession 既有问题，stash 验证与本变更无关）。仓库根新增 `RELEASE_NOTES.md` 模板（供 user-manual-release-assets change 使用）。

### Changed
- **级联子菜单三 bug 修复 + E2E 基建（2026-09-06 用户三报）**：①执行解析——`ExecuteSelectionAsync` 由只查根 `Slots` 改为 `ResolveActiveSlotSource()`（SubMenu+Cascade+idx≥1 → `SubMenuSlots`），子轮盘态选中子槽位释放不再误执行根槽（原误解析到同索引根槽 QA Fan-3）；②状态残留——`IsVisible=false` 分支统一清理子菜单态（`ReleaseSubMenuSlots` + 重置 descriptor/menuState/分页/原点），下次唤起不再凭空出现旧子槽位；③位置——E2E 权威几何验证渲染与 ADR-024 D1 完全一致（半径 160、±30° 翼弧、UIA 物理像素 ×1.5 换算吻合），"位置不对"确认为残留子槽位以旧位置渲染的可见症状（同根因②）。E2E：`DebugCommandServer` 增 `profile` 参数（`DebugForcedActiveProfile` 强制 profile，绕开前台无 profile 时 `InsertCreatorSlot` 干扰）、`slot-click/slot-hover` 前真实 `SetCursorPos` 停靠（防 16ms 光标采样回盖合成悬停）、`SetActionExecuted` 带 label payload；两个复现 workflow + UIA dump 全 PASS。
- 新增 `scripts/dev.ps1` 开发命令封装（journal 11:52 起多次提及的遗愿落地）：`build` / `test` / `commit` / `all` 四个子命令，执行前自动修补沙箱 shell 剥离的 Windows 环境变量。env 修补**只补缺失、进程级、永不覆盖**（APPDATA / LOCALAPPDATA / USERPROFILE 经 USERPROFILE→HOMEDRIVE+HOMEPATH→USERNAME 链派生；ProgramFiles 优先 ProgramW6432；ProgramFiles(x86) 与 CommonProgramFiles(x86) 由补好的 ProgramFiles 派生；SystemRoot / windir / ProgramData 由 SystemDrive 派生），根治 dotnet NuGet `Value cannot be null (path1)` 崩溃；dotnet 不在 PATH 时回退 `%ProgramFiles%\dotnet\dotnet.exe`。build/test 目标固定 `Pulsar/Pulsar.sln` 与 `Pulsar.Tests.csproj`（规避 sln 不在仓库根的 MSB1009 坑）并透传额外参数；commit 默认 `git add -u` 仅暂存 tracked 改动（呼应「勿误提交」约定），`-All` 切 `git add -A`。PS 5.1 兼容、纯 ASCII（规避 PS 5.1 读无 BOM UTF-8 的中文乱码坑）、native 启动失败诚实退出 127（不谎报成功）。

### Changed
- **子轮盘触发模型统一 + 标题遮挡修复（2026-09-06 用户两报，ADR-024 v1.2.2 D10/D11）**：
  - **D10 Ring 命中统一为极坐标扇区触发**：`HitTestRing` 删除 [r-25, r+25] 窄环带下界——死区外至环外缘（dist ∈ [DeadZone, r+slotSize/2]）按角度扇区命中，与父轮盘 `SlotLayoutEngine.HitTest`（死区外纯扇区制）及 Fan（[DeadZone, fanExtent]）同构；环外空白区保持 -1 不误触；死区内 0 = 父身份收起锚点语义不变。
  - **D11 动态标题 cascade 期间抑制**：`UpdateDynamicVisuals` 标题写入在 cascade 子菜单打开时置空（复用 `preserveCenterIdentity` 判断）——消除下半部父槽 Ring 子环与 Y=385 固定标题带的视觉重叠，同时去除与中心身份（Ring=父 slot）的重复提示；收起后下一次 `UpdateVisuals` 自动恢复。
  - 测试：`SubMenuLayoutEngineTests` Ring 环带内侧扇区命中 2 新增 + 外缘 -1 重构；`CascadeSubMenuLayoutRuntimeTests` 新增真实场景“环带间隙触发”与“标题抑制/恢复”（fire-and-forget 收起轮询）；**全量 1129/1129 通过**（基线 1125 + 4）。
- **架构深化 1+2（2026-09-06 架构审查，行为保持）**：
  - **C1 级联几何深化**：`BuildCascadeParentPose` 全文（方向推导、maxSafeRadius、Fan gap 压缩、Ring 0.90 钳制、扇区 `maxWing=min(π/6, sectorHalf−orbHalfAngle)`、3 子槽放宽 `minNonOverlap`、死区 `max(10, centerSize/2)`）+ `EffectiveCascadeStyle` + 常量 `FanGap`/`FanMinGap`/`SubMenuRingRadiusRatio` 迁入 `SubMenuLayoutEngine`；`ISubMenuLayoutEngine` 新增 `SubMenuPoseContext` + `BuildParentPose` + `ResolveEffectiveStyle`；MenuSession 保留页切片/视口换算/坐标平移分派/写槽，姿势构建变薄适配器。几何知识单一归属（ADR-024 D1/D1a/D5/D6/D7），编辑器预览 seam 就绪（UI 未做）。
  - **C2 SlotOrb 渲染编排**：消灭视图层服务定位器 + 静态缓存——新增 `IRadialRendererResolver` + `StyleRendererResolver`（config revision 缓存 + registry.Changed 失效，语义与原 SlotOrb 一致）并注册单例；SlotOrb 构造时一次解析 resolver，highlight 走同一 seam；`RadialMenuViewModel.ApplyRadialRendering` 改吃同一 resolver（消除同工厂双解析路径）；视差步进/目标计算抽为纯静态 `ParallaxMotion`（`ComputeTargetOffset` + `Step`），CompositionTarget 循环 + 光标/DPI 原生读取留在控件（薄胶水）。
  - 测试：`SubMenuLayoutEngineTests` 增 10 姿势用例（含 E2E 扇区 trace 交叉验证）；新增 `StyleRendererResolverTests`（4）+ `ParallaxMotionTests`（6）；`CascadeSubMenuLayoutRuntimeTests` 断言不变（行为等价）。**全量 1125/1125 通过**（基线 1104 + 21）。
- **级联子菜单几何二次规格（ADR-024 v1.2.0，用户 2026-09-06 优化点）**：
  - **D1a Fan 扇区约束**：`SubMenuParentPose` 新增 `FanMaxWingRadians`（默认 30°）；`BuildCascadeParentPose` 传 `min(30°, π/slotsPerPage)` —— 8 槽主轮盘（每槽 45°）时 Fan 翼角收窄至 **±22.5°**，子槽位不再溢出到相邻根槽扇区；布局与命中测试同用该上限（2 翼 ±maxWing / 3 翼 -maxWing·0·+maxWing，half-sector 跟随）。**v1.2.1 补充**：翼角再扣掉 orb 角宽 `atan(slotSize/2/r)≈8.9°` → 8 槽 2 子槽实测 **±13.6°**，子槽 orb 整体落入父槽 45° 扇区（中心贴边时 orb 外半仍会溢出，用户 QA Fan-2 反馈）；3 子槽在 r=160 下 45° 扇区放不下 3 个不重叠 orb（3×50 弧长 > 扇区弧 122.5），取不重叠最窄翼展 ~18.7°（中心仍在扇区内）。
  - **D7 Ring 中心 Slot 可见且为主 Slot**：`EnterCascadeVisualsAsync` Ring 分支不再把 CenterSlot 并入主轮盘淡出，而保留在父槽位并随子槽位一起 bloom 回显（opacity 1）；CenterSlot 继承父 Slot 的**图标+label**；`RadialMenuVisualStateCoordinator.UpdateVisuals` 新增 `preserveCenterIdentity`——cascade 子菜单内悬停锚点（0）不再把中心重置为通用"返回"，Ring 中心保持父身份、Fan 中心保持冻结根态。
  - 测试：`SubMenuLayoutEngineTests` 新增扇区约束 3 用例（±22.5° 布局 + 布局/命中一致）；`CascadeSubMenuLayoutRuntimeTests` Fan 断言 ±30°→±22.5°→±13.6°（orb 外缘贴扇区）+ 新增 Fan3 不重叠/扇区内用例；**全量 1104/1104 通过，0 警告**；E2E `fan-sector-constraint-2` / `ring-center-visible-4` PASS（UIA dump 实测：Fan 子槽 dx=±37.7=160·sin13.6°、orb 外缘恰好 -112.5° 贴扇区边界；Ring 中心 orb 与父槽中心重合、label=父名）。
- **级联子菜单 Fan/Ring 几何与交互重构落地（ADR-024，grilling 对齐 D1–D9）**：
  - **D1+D6 Fan 几何**：`BuildCascadeParentPose` 重写——Fan 圆心保持主轮盘中心、半径 `R+gap`（gap=70，越 maxSafeRadius 225 动态压缩至保底 50），子槽位在主轮盘同心圆外圈展开，与根槽位重叠在结构上不可能；新增 `FanGap`/`FanMinGap` 常量。
  - **D7 Ring 替换模式**：Ring 子轮盘以父 Slot 画布坐标为圆心展开完整子环；CenterSlot 移到父位置承载返回动作（仅 Ring），主轮盘纯淡出。
  - **D4/D8 Fan 叠加模式**：主轮盘零变换冻结为只读背景，父 Slot 留原位高亮描边、作为单击收起锚点（`HitTestCascadeSubMenu` 父足迹返 0，`UpdateActiveSlotCore` index 0 点亮父 anchor）。
  - **D3/D9 删视口 glide 与收缩动画**：`EnterSubMenuAsyncCore` 拆分 cascade/window 两条路径——cascade 走 `EnterCascadeVisualsAsync`（子槽位从父 Slot 绽开：位移+淡入，主轮盘零变换、画布不动）；返回走 `RestoreFromCascadeAsync`（无 glide）。window 子菜单保留原路径。
  - **D5 动态甩出取消**：`UpdateFlickOutEscapeState` 改用 `GetActiveWheelMetrics()`（原点+半径跟随当前活跃轮盘，复用 pose 无第二真值源）。Root 阈值 135 位级不变；Fan 240；Ring 121.5。
  - **D2 Fan 超限有效样式**：`EffectiveCascadeStyle()` 统一判定——Fan 描述符子动作数 > `SubMenuLayoutEngine.FanMaxSlots`（转公开）时按 Ring 渲染，pose/策略/动画与编辑器警示一致。
  - 测试：`CascadeSubMenuStrategyTests` 全面改断言 `SubMenuSlots`（新增 Fan 不触 CenterSlot 用例）；`CascadeSubMenuLayoutRuntimeTests` 几何断言 81→160 / Ring 圆心改父 Slot；`SubMenuCoordinatorStrategyTests` 传入专用集合。**全量 1098/1098 通过，0 警告。**

- 插件运行时宽门面拆分为三个窄 seam（ADR-012，架构审查候选 A）：`IPluginRegistry` 收缩为注册面（发现·激活·查询，8 方法），`ExecuteAsync` 移入新执行面 `IPluginExecutor`，重扫/停用/状态/授权/卸载移入新运维面 `IPluginRuntimeOps`；三个接口由同一 `PluginRuntimeKernel` 单例实现并经 DI 注册。透传包装类 `PluginRegistry` 删除，执行热路径/生命周期编排/设置页各自改注入最窄 seam。
- 熔断策略去 UI/遥测依赖（ADR-013，架构审查候选 D）：`PluginCircuitBreakerPolicy` 收敛为纯状态机（构造仅 `ILogger`），打开/恢复经 `Tripped` / `Recovered` 事件广播；新增 `PluginBreakerNotificationService` 观察者 adapter 订阅事件并把迁移转成健康遥测记录与本地化托盘通知，启动协调器在托盘初始化后解析激活。文案与行为保持与迁移前一致。
- 可回收 ALC 卸载不变量收口（架构审查候选 E）：`PluginLoader.TryUnloadExternalContext` 现在一次性完成 `Unload()` 发起 + 强制 GC 泵（`GC.Collect`×2 + `WaitForPendingFinalizers`），调用方（`PluginRuntimeKernel.DeactivatePluginAsync`）不再内联 GC 序列，只负责调用前的引用切断。
- 插件清单解析收敛为单一事实来源（架构审查候选 C）：新增 `PluginManifestReader`（static），把「`plugin.manifest.json` → 回退 `manifest.json`」文件名解析与大小写不敏感反序列化收口为一处，四处内联复制（`PluginLoader.TryReadExternalManifest`、`LocalPluginScanner.ScanInstalledPlugins`、`PluginPackageManager.HasValidManifest`/`ReadAndValidateManifest`）改为调用共享 reader。Id 空判定、权限 token、版本兼容与各自的失败消息仍留在调用方错误层，语义逐字不变。
- 插件卡片能力声明进 metadata，通用 VM 移除插件 ID 特判与 service-locator（ADR-015，架构审查候选 F）：`PluginCapabilities` 新增四个默认 false 的 UI 能力标志（`SupportsScriptEditor` / `HasBuiltinExamples` / `HasCustomConfigDialog` / `SupportsWindowInspector`），由 WinSwitcher（自定义配置对话框 + Window Inspector）与 Web Scripts（脚本编辑器 + 示例库）在各自 `GetMetadata()` 自述。`PluginViewModel` / `PluginSettingsDialogViewModel` 改按能力分支，并把 `IServiceProvider.GetService<T>()` 隐藏依赖改为构造显式注入（窗口/进程注册表/脚本文件/脚本校验/示例库/日志）；两个 Manager VM 与 `ExternalPluginViewModel` 同步删除 provider 透传。行为与改前一致，未声明能力的插件（含全部外部插件）渲染不变。
- 运行时状态存储的读操作改为纯读（ADR-016，架构审查候选 G）：`PluginRuntimeStateStore.GetSnapshot` 对未知插件的回退快照不再 `TryAdd` 落缓存，快照字典只由通过校验的 `Transition()` 写入——读查询不再带写副作用，被拒绝的非法转移（如对未注册插件 `Transition(..., Running)`）不再留下默认快照痕迹。验证后否决了报告建议的「拆 `PluginRegistry` + `LifecycleStateMachine` 两模块」方案：该 store 是深模块（双私有字典的配对不变量被封装在 6 方法小接口内），拆分只会把协调成本外移并连带重写全部调用方，收益为零。

### Fixed
- **级联子菜单 Fan 布局忽略父方向角（人工 QA 首轮发现，change `2026-09-05-cascade-submenu-fan-qa` 任务 3.1）**：`SubMenuLayoutEngine.ComputeChildPositions` 的 Fan 分支把相对翼角（±30°/0°）当绝对角使用（漏加 `DirectionRadians`），子项不随父槽位方向展开、全部落到画布右侧 ±30°/0° 并与根环槽位叠置（观感「子项消失、全是圆形」）；而 `HitTestFan` 正确地把鼠标点减去 `DirectionRadians` 后在父局部基比对——**布局与命中在非朝东父槽位上全面分家**。单测未拦截的根因：测试 pose 的 `DirectionRadians=0`，相对角恰等于绝对角，且 Ring 有方向性测试而 Fan 没有。修复为 Fan 分支翼角加上父方向角（与 Ring 分支同口径）；新增 6 个回归测试——`ComputeChildPositions_Fan_ShouldRespectParentDirection`（direction=−90° 时三翼落 −120°/−90°/−60°）、`Fan_LayoutAndHitTest_ShouldAgreeAtEveryChildCenter`（4 父方向 × 1–3 子项布局↔命中对账）、`CascadeSubMenuLayoutRuntimeTests` ×3（真引擎 + 真 MenuSession 走完整进入链路：Fan2 落小环 / Ring5 均布小环 / filler 留根环）。全量 1066/1066 通过（原基线 1060）。
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
- `scripts/dev.ps1` 验证：最恶劣场景（显式剥离 7 个 NuGet 关键环境变量）下自修复生效——6 个缺失变量逐条修补、dotnet PATH 缺失时 fallback 命中；native 启动被环境阻断时脚本诚实退出 127（加固前缺陷：`exit $null` 谎报成功，已修）。回归门禁走可靠路径确认仓库绿：build **0 警告 0 错误**（10.2s）、全量 **1059 / 1059**（23s）。注：本会话 PowerShell 工具沙箱禁 native 子进程生成（whoami / cmd / dotnet 全部空输出空退出码），`dev.ps1 build` 的真实终端端到端留一次调用验证：`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev.ps1 build`。
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
