# Journal NEXT（持久待办，单一权威）

> **用途**：跨会话「下一步」的单一权威清单。每个 `## Session` block 不再重复罗列待办，只在本文件更新——完成即划线 `~~…~~`，新增即在末尾追加（ADR-021）。
> **会话仪式**：Session start 必读本文件（小体积）；涉及具体某天的「做了什么/坑」才去读对应 `Docs/journal/YYYY-MM-DD.md` 尾部或归档。

## 待办

- [ ] ~~Fan 位置问题收尾（change `2026-09-05-cascade-submenu-fan-qa`，QA 暂停中）：用户打完游戏后按 `submenu-radius-geometry-amendment-draft.md` §5 拍 4 张现象照 → 草案定稿 → ADR-011 追加 Amendment（推荐方案 A：Fan 移根环外侧 r≈120；Ring 视现象）→ 实施 + 新增「子项互不重叠/不撞中心圆」回归断言 → A3/A4 + B–H 全部用例 → 3.2/4.x 收口归档。当前真实配置仍是 QA fixture（`Profiles.json.bak-before-fan-qa` 还原点）；E2E 待用户有空再跑。~~ **已过时（2026-09-06）：**ADR-024 D1 已定义 Fan 几何（R+gap=160、±30°、同心），本会话经 GEOMETRY-TRACE + DPI 换算实测与规格吻合；三 bug 修复已 E2E PASS。旧草案（r≈120）不再适用。
- [ ] 观察 1-2 个会话：AGENTS.md 瘦身后 agent 是否经 §3 指针去 `Docs/lessons/` 取坑位全文（防"全表靠内联"回潮，ADR-022 后续）。
- [ ] 观察若干会话：确认无 harness 再向 `.workbuddy/memory/` 写正文（若复发 → 考虑 Junction 收口，ADR-019 后续）。**2026-09-05 检查：合规**——仅一行指向 journal 的指针（183B），无正文复发。
- [ ] 子轮盘交付待用户真机验收：三 bug 修复（`artifacts/bug1-after-fix` · `bug2-after-fix` · `bug3-geometry`）+ ADR-024 v1.2.1 两项几何优化（Fan 扇区约束 orb 外缘收进扇区 / Ring 中心=父 Slot，`artifacts/fan-sector-constraint-2` · `ring-center-visible-4`），E2E 均已 PASS。

- [ ] 架构审查 S1（附注）：`RadialMenuLayoutCoordinator` / `CommandPageProvider` / `ProcessPageProvider` 仍 0 直接测试（ADR-023:53 承诺的 page-provider tests 未兑现），后续候选。

- [ ] **主题重构（ui-ux-pro-max，2026-09-09 中午）**：`Theme.Dark/Light.xaml` 按设计系统「Code dark + run green」（Slate 色阶）重构——键名契约不变，语义组重排；Orb #2D2D2D→#334155、激活通道→Sky 系、深色危险 hover→#EF4444；`RadialThemeTokenSetTests` 钉死值同步；全量 1361/1361、build 0/0；`Docs/design-system/pulsar/MASTER.md` 持久化 + `artifacts/theme-refactor-preview.html` 预览。
- [ ] **图标圆角化（2026-09-09 12:20）**：`build_radial_ico.py` 加 36px 圆角矩形遮罩，light/dark ico（7 尺寸帧）+ 256 masters + 根 Pulsar.ico 全部圆角透明；build 0/0、1361/1361。
- [ ] **⚠️ 提交被安全软件拦截（阻塞中）**：git.exe 在 E 盘写 loose object 恒定 `Permission denied`（PS/py/cmd 正常、C 盘仓库正常、E 盘任何仓库均失败）→ 判定为安全软件（勒索防护/自定义防护）对 git 在 E 盘的进程级拦截。**待用户放行 git（或确认处置方式）后重试 `git add` + 两次提交（主题重构 / 图标圆角）。** **已解除（2026-09-09 下午核实）**：`e7c4cfe`/`abea161` 已入库，git 恢复正常。
- [ ] **轮盘面板 UX 轨 U1-U6 已落地（2026-09-09 下午，grill auto-with-guardrails + TDD）**：U1 hover enter 300→120ms（`SlotOrb.HoverEnter/ReleaseDuration` 钉死）/ U2 磁吸上限 120→400px/s / U3 翻页 nudge BackEase→QuarticEase / U4 键盘扇区导航（←→↑↓ 选槽 + 双键对角 + Enter 执行 + 1-9 直达 + PgUp/PgDn 翻页；新增 `RadialKeyboardNavigator`）/ U5 标签激活展开 40→72 / U6 死注释清理 + flick-out 0.2→0.16s。新增 27 测试，全量 **1388/1388**，build 0/0。**待真机**：U2 手感 A/B、U4 键盘手感、U5 标签展开视觉。UIA ControlType 变更顺延（E2E 依赖 Custom peer）。调研报告 `Docs/reports/2026-09-09-RADIAL_PANEL_UX_RESEARCH.html`；~~重构轨 R1-R4（MenuSession 拆分）未启动~~（R1 已落地，见下条）。
- [ ] **重构轨 R1 已落地（2026-09-09 14:15，grill auto-with-guardrails）**：新增 `Services/WheelGeometry.cs` 单一真相源（500 画布/250 中心/50 默认槽径），收口 `SlotLayoutEngine` / `RadialMenuLayoutCoordinator` / `SlotWheelEditorViewModel` 三处 (250,250) 硬编码（纯重构零行为变化）；S1/ADR-023 测试债清偿——新增 `SlotLayoutEnginePoseSnapshotTests`（9：8/10/12 槽全参数快照 + 全槽位坐标 + HitTest sanity）与 `RadialMenuLayoutCoordinatorTests`（8：GetLayoutMetrics/RebuildSlots/RefreshAnimationTargets/ApplyConfigSlotCountChange），全量 **1405/1405**，build 0/0。**⚠ 已记录未修**：半径双路径分歧（CalculateOptimalLayout 固定-50 → N=10 97.08 vs GetLayoutMetrics 缩放 → 90.61），收敛留待后续 ADR。
- [ ] **右键手势延迟优化（搁置，2026-09-09 用户裁定不深入，后续有机会再做）**：手势开启时普通右键响应慢 = pending-swallow 设计固有代价（DOWN 被吞、松键后 mouse_event 回放）+ LL 鼠标钩子同步执行订阅者（回调阻塞拖慢全系统输入，UI 繁忙时放大）。非回归（R2 前后 replay 调用点逐字一致）。优化候选方向：(a) 钩子回调极速化/独立线程；(b) 评估回放时序（如 down 即乐观透传 + 特例召回，需重审 LEAK-FIX 语义）。另：Gesture 临时页注册竞态（页面打不开仅警告）与 `e.Handled=true` 崩溃韧性策略亦未决策，随本项一并择机处理。崩溃防御本体已修（`5a2c930`）。
- [ ] **重构轨 R2 已落地（2026-09-09 15:45，grill auto-with-guardrails）**：`GestureInputRouter`（新增）迁出右拖手势 claim/promote/replay 编排（MenuSession **3609→3357 行**；公共面 `FeedRightDragGesture`/`FeedGlobalMouseMove` 不变）；新增 15 用例（编排策略首次脱离完整 harness 可测），既有 Leak/Isolation 套件原样通过作行为守护；全量 **1420/1420**，build 0/0。flick-out 判定（一行决策，依赖子菜单 pose）明确不迁（ROI 过低）。**R4 已落地（17:15）：MenuWatchdog 迁出（3357→3317 行），+7 用例，全量 1427/1427，build 0/0。剩余：R3 SubMenuTransitionController + CenterIdentityPolicy（需子轮盘真机验收通过后动）。**

## 已完成（历史保留）

- [ ] resx 孤儿键独立审计（低成本 change 候选）：全量扫描 1168 键发现 ~296 疑似孤儿，须先排除约定查找键（`SlotParam.*`/`SlotAction.*`/`PluginPermission.*` 等动态拼接前缀）；范围纪律见 ADR-029 Addendum。
