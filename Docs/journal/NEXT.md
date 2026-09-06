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
- [ ] OpenWiki 补缺页：余额恢复后 `openwiki --update` 补 3 页（architecture/system-overview · architecture/window-switching · quickstart）；反复跳过的页可临时切 deepseek-v4-pro。注意 `.github/workflows/openwiki-update.yml` 每天 08:00 UTC 定时跑，推 GitHub + 配 key 会每日消耗额度；本地补丁 `repository-runner.js`（2 处 "LOCAL PATCH (2026-09-05)"）在 `npm update -g openwiki` 后需重打。
- [ ] user手动添加：须将设置页面的保存按钮改为图标家问题“保存”。同时，也需要将设置页面里的各种长文本描述精简化或者将详情藏入tooltip，保证观感简洁有序。
- [ ] 子轮盘交付待用户真机验收：三 bug 修复（`artifacts/bug1-after-fix` · `bug2-after-fix` · `bug3-geometry`）+ ADR-024 v1.2.1 两项几何优化（Fan 扇区约束 orb 外缘收进扇区 / Ring 中心=父 Slot，`artifacts/fan-sector-constraint-2` · `ring-center-visible-4`），E2E 均已 PASS。
- [x] ~~提交未落地改动（34 文件，含 ADR-024 v1.2.0 + 三 bug 修复 + E2E 基建 + 扇区/回归单测）：等用户确认后 commit（当前 main 工作树）。~~ **已提交并 push**：`0cab1ff`/`d978d2c`/`8c236da`（ADR-024 相关），main 与 origin/main 同步。
- [x] ~~**openspec repositioning Phase 2（宣传截图）**：E2E 管线 + 4 张 1920×1080 首屏用图 + `Docs/media/release/` 目录约定。~~ **已提交** `893870c`，4 张 PNG 已入库，main 与 origin/main 同步。
- [ ] **openspec repositioning Phase 3（Demo 视频）**：三支脚本已定稿并入 `93044dc`（`Docs/media/release/videos/demo-video-scripts.md`，Excel 跑宏 40s / 老旧网页脚本注入 45s / 登录填表自动化 40s）；剩余 3.2 录制剪辑 + 3.3 mp4 入库，需真机录屏。
- [x] ~~**openspec repositioning Phase 4（README 重写）**：`README.md`（zh）首屏定位语 + 三支柱场景 + 真实截图 + 差异化一句话；`README_EN.md` 同步；自查不再以「启动器」自居。~~ **已完成**（`93044dc` 安全批次）。
- [ ] 可选清理：`MenuSession.cs` 的 `[CLICK-TRACE]`/`[EXEC-TRACE]`/`[GEOMETRY-TRACE]`/`[VISUAL-TRACE]` Debug 日志（提交前或后续随手删）。

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
