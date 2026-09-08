# Journal NEXT（持久待办，单一权威）

> **用途**：跨会话「下一步」的单一权威清单。每个 `## Session` block 不再重复罗列待办，只在本文件更新——完成即划线 `~~…~~`，新增即在末尾追加（ADR-021）。
> **会话仪式**：Session start 必读本文件（小体积）；涉及具体某天的「做了什么/坑」才去读对应 `Docs/journal/YYYY-MM-DD.md` 尾部或归档。

## 待办
- [x] ~~架构深化 1+2（C1 级联几何深化 + C2 SlotOrb 渲染编排）已本地提交（023f21a），待用户确认后 push。~~
- [x] ~~子轮盘触发统一 + 标题遮挡修复（2026-09-06 用户两报，ADR-024 v1.2.2 D10/D11）：Ring 命中统一为死区外至环外缘极坐标扇区触发、cascade 期间动态标题抑制；全量 1129/1129 通过。~~ **已本地提交**（`3f68c13` D10/D11 ring polar），在未 push 队列中。
- [x] ~~架构深化 1+2（C1 级联几何深化 + C2 SlotOrb 渲染编排，2026-09-06 架构审查）：全量 1125/1125 通过。~~ **已本地提交**（`c4045bf` archive cascade-submenu-fan-qa 相关），在未 push 队列中。

- [ ] ~~Fan 位置问题收尾（change `2026-09-05-cascade-submenu-fan-qa`，QA 暂停中）：用户打完游戏后按 `submenu-radius-geometry-amendment-draft.md` §5 拍 4 张现象照 → 草案定稿 → ADR-011 追加 Amendment（推荐方案 A：Fan 移根环外侧 r≈120；Ring 视现象）→ 实施 + 新增「子项互不重叠/不撞中心圆」回归断言 → A3/A4 + B–H 全部用例 → 3.2/4.x 收口归档。当前真实配置仍是 QA fixture（`Profiles.json.bak-before-fan-qa` 还原点）；E2E 待用户有空再跑。~~ **已过时（2026-09-06）：**ADR-024 D1 已定义 Fan 几何（R+gap=160、±30°、同心），本会话经 GEOMETRY-TRACE + DPI 换算实测与规格吻合；三 bug 修复已 E2E PASS。旧草案（r≈120）不再适用。
- [ ] 观察 1-2 个会话：AGENTS.md 瘦身后 agent 是否经 §3 指针去 `Docs/lessons/` 取坑位全文（防"全表靠内联"回潮，ADR-022 后续）。
- [ ] 观察若干会话：确认无 harness 再向 `.workbuddy/memory/` 写正文（若复发 → 考虑 Junction 收口，ADR-019 后续）。**2026-09-05 检查：合规**——仅一行指向 journal 的指针（183B），无正文复发。
- [x] ~~OpenWiki 补缺页：余额恢复后 `openwiki --update` 补 3 页（architecture/system-overview · architecture/window-switching · quickstart）；反复跳过的页可临时切 deepseek-v4-pro。注意 `.github/workflows/openwiki-update.yml` 每天 08:00 UTC 定时跑，推 GitHub + 配 key 会每日消耗额度；本地补丁 `repository-runner.js`（2 处 "LOCAL PATCH (2026-09-05)"）在 `npm update -g openwiki` 后需重打。~~ **已取消（2026-09-08 用户裁决）**：成本高、产出效果不佳；现有 `openwiki/` 内容维持现状。
- [x] ~~设置页 UI 精简：保存按钮改为图标+文字"保存"；长文本描述精简化或将详情藏入 tooltip，保证观感简洁有序。~~ **已完成**：SettingsWindow 保存按钮加 Content（Save Changes/保存更改）；SettingsGeneralPage 4 处偏长 Description 精简 + 新增 DescriptionToolTip（QuickSwitchTimeout / RightDragSwitcherModifier / RightDragActionModifier / Gesture.EnableToggle），中英双语 resx 同步；build 0 警告 0 错误，全量 1227/1227 通过。
- [ ] 子轮盘交付待用户真机验收：三 bug 修复（`artifacts/bug1-after-fix` · `bug2-after-fix` · `bug3-geometry`）+ ADR-024 v1.2.1 两项几何优化（Fan 扇区约束 orb 外缘收进扇区 / Ring 中心=父 Slot，`artifacts/fan-sector-constraint-2` · `ring-center-visible-4`），E2E 均已 PASS。
- [x] ~~提交未落地改动（34 文件，含 ADR-024 v1.2.0 + 三 bug 修复 + E2E 基建 + 扇区/回归单测）：等用户确认后 commit（当前 main 工作树）。~~ **已提交并 push**：`0cab1ff`/`d978d2c`/`8c236da`（ADR-024 相关），main 与 origin/main 同步。
- [x] ~~**openspec repositioning Phase 2（宣传截图）**：E2E 管线 + 4 张 1920×1080 首屏用图 + `Docs/media/release/` 目录约定。~~ **已提交** `893870c`，4 张 PNG 已入库，main 与 origin/main 同步。
- [x] ~~**openspec repositioning Phase 3（Demo 视频）**：三支脚本已定稿并入 `93044dc`（`Docs/media/release/videos/demo-video-scripts.md`，Excel 跑宏 40s / 老旧网页脚本注入 45s / 登录填表自动化 40s）；剩余 3.2 录制剪辑 + 3.3 mp4 入库，需真机录屏。~~ **已取消（2026-09-08 用户裁决）**：现有动图已足够，不再录视频；脚本文档保留备查。
- [x] ~~**openspec repositioning Phase 4（README 重写）**：`README.md`（zh）首屏定位语 + 三支柱场景 + 真实截图 + 差异化一句话；`README_EN.md` 同步；自查不再以「启动器」自居。~~ **已完成**（`93044dc` 安全批次）。
- [x] ~~可选清理：`MenuSession.cs` 的 `[CLICK-TRACE]`/`[EXEC-TRACE]`/`[GEOMETRY-TRACE]`/`[VISUAL-TRACE]` Debug 日志（提交前或后续随手删）。~~ **已完成**：7 条 trace 日志全部移除（CLICK-TRACE ×3 / EXEC-TRACE ×2 / GEOMETRY-TRACE ×1 / VISUAL-TRACE ×1），无残留变量；build 0 警告 0 错误，全量 1227/1227 通过。
- [x] ~~openspec 四 change 纯代码剩余项（in-app-auto-update 3.x DI集成 / installer 3.1 单实例 / user-manual 3.1 Release说明）。~~ **已完成**：UpdateOrchestrator + About UI + 启动集成 + 命名 Mutex 单实例 + RELEASE_NOTES-1.11.0.md；build 0 警告 0 错误，全量 1227/1227。
- [x] ~~**需真机/外部动作的剩余项**（无法在不唤起应用的约束下推进）~~ **2026-09-06 23:55 真机验证收口**（commit `d346b3f`）：
  - ~~in-app-auto-update 4.3：真实网络 QA（正常+断网路径）+ 打测试 tag 验证 UpdateAvailable 全链路~~ ✅ UpToDate + UpdateAvailable（v1.11.1 测试 release 验证托盘通知）均通过；发现并修复 Lazy<UpdateOrchestrator> 未注册的 DI bug
  - ~~installer 2.4：真实安装→启动→覆盖升级→卸载（数据保留验证）~~ ✅ 静默安装→运行→静默卸载全链路通过；%AppData%\Pulsar 103 文件保留
  - ~~installer 3.2/3.3：双实例交叉验证（安装版↔Standalone）~~ ✅ Mutex 单实例保护验证通过（第二个实例 5s 内自动退出）
  - ~~user-manual 3.2/3.3：资产上传 + 打 tag v1.11.0 发布 GitHub Release~~ ✅ v1.11.0 已发布（三资产，notes 无 BOM 中文正常）
- [x] ~~**剩余需外部环境的项**（本机无法完成；Demo 视频已于 2026-09-08 取消）：
  - installer 1.3：干净 VM 冒烟（无 .NET Runtime 环境启动 Standalone zip 验证自包含运行时）~~ **已收口（2026-09-08 用户真机验证）**：v1.12.0 Standalone zip（commit `b5c0ed4` 构建的本地包）在无 .NET 的新机器上解压运行通过——自包含运行时冒烟 ✅，同时覆盖窗口切换真机回归 ✅ 与托盘菜单/主题图标真机观察 ✅（C2 收口 P1 项 3 一并关闭）。

- [x] ~~**架构审查 C2 未竟（2026-09-07 暂停，等用户裁决）**~~ **已收口**（commit 见下方提交状态行）：用户裁决走 (a)。前门新增 `EvaluateStructural(snapshot)` / `EvaluateSnapshot(snapshot, scope)` 两阶段词汇；`WindowInventoryService` 改注入前门 evaluator，`IWindowInventoryService` 移除黑名单谓词参数（作用域按路径固定）；coordinator 不再转发谓词。新增前门词汇测试 10 + inventory 协议测试 4（该文件首次有网）；全量 1248/1248。~~ 需真机回归窗口切换（P1 项 3）**已验证通过**（2026-09-08 用户新机器真机，随 v1.12.0 Standalone 冒烟一并收口）。
- [ ] 架构审查 C4（设置面 Draft 泄漏，2026-09-07 口径已修正）：不是「1 属性 + 10 处访问」的机械替换——需给 `SettingsEditorSession` 设计变更 API（写点 :112/:492/:952）+ 定脏标记归属 + 绑定刷新语义，Session 0 专属测试。grill 第二轮建议与 C5 同批、测试先行，**等用户裁决**（延期 or 本会话做）。**2026-09-07 用户已裁决：(b) 与 C5 合并进专门会话。**
- [ ] 架构审查 C5（教程层）：改动面最大（XAML + 1168 行 0 测试 View），与 C4 同批进专门会话（用户已裁决）。
- [x] ~~架构审查 C6（推荐引擎时钟 seam 空壳）~~ **已落地**（commit `1bc09ad`）：3 处裸 `DateTime.UtcNow` 收口到 `_clock().ToUniversalTime()`（时区语义保持：趋势日键仍本地日期）；Unused/Inactive 阈值测试注入固定时钟 + 新增「近期使用不触发」反向守护；全量 1234/1234。
- [x] ~~**C1/C3/C2 全量 + C6 已本地提交**（`e441e73` / `b6980e2` / `1bc09ad` / `5a71f17`），main ahead origin/main 5，待用户 push。~~ **已 push**（2026-09-08 会话开始，`6b91e79..09a7f1d` 13 commits 含 C 系列）。
- [x] ~~**架构审查 C3（MRU「上一个窗口」单一权威）**~~ **已落地并 push**（2026-09-08）：`QuickSwitchEngine` 吸收 MenuPrevious 槽（`SetMenuSnapshot/GetMenuSnapshot`，不过滤）成为 Window History 单一权威；`WindowService` 5 处读写重指引擎；删除 `IFocusHistory` 全链路 + `QuickSwitchAsync` + `QuickSwitchResult` + `RecordPreviousWindow` ×2 + `RegisterOrUpdateWindow`；引擎 +6 测试、门面 +1 双轨钉死测试，WindowTrackingServiceTests 重写；全量 1257/1257，build 0 警告 0 错误。ADR-028 + CONTEXT.md Window History 术语已更新。**已提交 `b3619ab` 并 push origin/main**（`Docs/reports/2026-09-07-DOC_STRUCTURE_AUDIT.html` 噪声改动已 restore）。
- [x] ~~**低成本批次 6 项已实施未提交**（2026-09-08，全量 1269/1269）：S2（RebuildCache 补调 ConfigureHookMode）/ S3（wizard 走 dispatcher seam）/ C7（PluginLocalization.ConventionLookup 收口）/ C6（DialogService.HasTemplate 校验+集成映射测试）/ C8（usage tracker 注入 clock）。~~ **已提交** `96ca9ba`（低成本架构批次收口 S2/S3/C7/C6/C8/S4）。
- [x] ~~**S4 删除 DemandPermission 死链**（grill Q6，护栏 1 命中暂停）~~ **已落地**（2026-09-08，用户「同意删除」）：删除 `IPluginPermissionInterceptor` 接口 + 2 adapter + `PluginPermissionDeniedException` + `PluginExecutionContext` 属性/ctor/`DemandPermission` + kernel attach；`PluginPermissionService`/`IsKnown` 真实权限门保留；TESTING_GUIDE.md 过时示例同步修正；全量 1269/1269。
- [x] ~~**剩余项目在 handoff**：`%TEMP%\pulsar-handoff-2026-09-08.md` —— C1（Execution Handoff 收束）/ C2（TrayService 拆分）/ C4（执行-拆除竞态 seam）/ C5（ImplementationType 不可变性）+ S1 附注；推荐顺序 C1 → C4 → C5 → C2。~~ **四项全部落地（2026-09-08 下午，grill auto-with-guardrails + TDD）**：
  - **C1** `19c7f43`：4 处手写 mark→switch→notify 收束进 `MenuSession.ExecuteQuickSwitchAsync()`；ProcessGroupStrategy/BackActionStrategy 直写 IsVisible 经裁定保留（无目标/取消不属动作执行）；1269/1269。
  - **C4** `091f575`：`PluginExecutionPipeline.AcquireExecutionGateAsync` seam + `DeactivatePluginAsync` 先 drain 后拆（超时 fail-close 抛异常中止卸载），+5 并发交叉测试；1274/1274。
  - **C5** `c4f20ce`：`ImplementationType` setter 收口 internal（护栏 1 暂停后用户选方案 D，非破坏）；1274/1274。
  - **C2**：`IAutoStartService`/`AutoStartRegistryService`（注册表隔离，可注入测试子键）+ `TrayMenuBuilder`（菜单纯函数化，AutomationId 结构钉死）+ `TrayIconService` 编排化（服务定位 → `Func<SettingsWindow>` 工厂，复用 tutorial 既有注册）；+5 测试。ITrayService 接口与 12 处消费方零改动。
- [ ] 架构审查 S1（附注）：`RadialMenuLayoutCoordinator` / `CommandPageProvider` / `ProcessPageProvider` 仍 0 直接测试（ADR-023:53 承诺的 page-provider tests 未兑现），后续候选。
- [x] ~~README 对比章节（2026-09-08 用户新增「🆚 与同类工具对比」+「✨ 四个不可替代点」，README.md / README_EN.md 双语 + 导航锚点）未提交，可与低成本批次一并 commit+push。~~ **已随 2026-09-08 清空工作树批次提交**（含去 AI 味重写、Design/icon-concepts 纳管、NEXT 取消项）。

## 已完成（历史保留）

- [x] ~~archify 插件系统架构图（2026-09-05，commit `7ecb5bd` + `dadc7b2`）：15 组件/15 关系/3 boundary，showcase 9/9 检查通过，12 处源码引用对照 `8df4281` 经 `--repo-root` 核验；`deliver` 冻结 spec `9a7582b4` → HTML `cd3021c8`。可视证据（visual-check PNG/JSON）与 `Pulsar/graphify-out/` 已入 gitignore，不入库。~~
- [x] ~~架构审查候选 1 (Strong) 落地（2026-09-05）：菜单执行路径服务定位器消除——新增 IPageProviderFactory seam，20 处 GetService → 显式构造依赖，PageProvider 首次可测，CommandPageProvider 重复构造收口为 InsertCreatorSlot helper。构建 0 警告 0 错误，1059 测试全过。ADR-023 + CONTEXT.md 术语已更新。~~
- [x] ~~复核 rules-stack 落地 audit log 并 push（2026-09-05）：用户复核无否决，4 commits（`76389cb`..`53a964c`）push 到 origin/main；journal 整理一并提交。~~
- [x] dev.ps1 真实终端端到端确认（2026-09-05）：`dev.ps1 build` → 0 警告 0 错误 6.8s；`dev.ps1 commit` 路径本次落地 NEXT 更新时 dogfood 通过。此前「沙箱禁 native spawn」限制仅在旧会话环境，按需确认模式可直接跑。
- [x] Pulsar.E2E 运行时端到端验证（2026-09-05，run `20260905-013738`，**PASS 18.6s**）：`radial-menu-open-via-command.json` 12 步全过——debug 实例启动、fixture 注入、录屏、命令通道开菜单、`Pulsar.Slot.0` UIA 断言、截图、关闭。产物在 `Pulsar/Pulsar.E2E/artifacts/`（已 gitignore）。截图右下角可见径向菜单轮盘（用户视觉确认），与 UIA 断言一致。
- [x] `ValidateOnboardingInvariants` 无人调用 gap（2026-09-05 核实已闭环）：`ConfigService.cs:300` 已在配置加载路径调用并打 `[ConfigInvariants]` 警告日志，NEXT 旧条目过时。
- [x] journal 轮转 + worktree 并行纪律（ADR-021，2026-09-04 23:53，commit `09ddae8`）：会话仪式改 tail + NEXT.md，日文件 ~15KB 归档轮转，AGENTS.md §10 worktree 纪律，全程经独立 worktree 实施并 ff 合并 main。
- [x] 候选 O/N/M/L 全部落地（2026-09-04 23:0x，commit `8b24da6`/`bf681ce`/`5e65a98`/`c5a35a4`）。
- [x] 全量测试死锁修复「第二次提交」（2026-09-04，commit `5412911`，含 `XUNIT_APPLICATION_CURRENT_DEADLOCK.md`）。
- [x] 构建警告清理至 0（2026-09-04 23:32，commit `784343f` + `470fba6`）。
- [x] openspec 四 change 安全批次（2026-09-06，commit `93044dc`）：in-app-auto-update 纯逻辑层（1/2/4.1，84 测试）+ user-manual（1.1-1.4/2.1-2.2）+ installer 脚本（1.1/1.2/2.1-2.3/4.1-4.3）+ repositioning（3.1 脚本/4.1-4.3 README/5.3）；ADR-025 + ADR-026；build 0 警告 0 错误，全量 1227/1227。各 change 剩余项（DI 集成/真实网络 QA/干净 VM 冒烟/Release 发布/视频录制）均依赖真机或外部发布动作，待用户环境执行。
