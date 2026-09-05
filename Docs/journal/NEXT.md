# Journal NEXT（持久待办，单一权威）

> **用途**：跨会话「下一步」的单一权威清单。每个 `## Session` block 不再重复罗列待办，只在本文件更新——完成即划线 `~~…~~`，新增即在末尾追加（ADR-021）。
> **会话仪式**：Session start 必读本文件（小体积）；涉及具体某天的「做了什么/坑」才去读对应 `Docs/journal/YYYY-MM-DD.md` 尾部或归档。

## 待办

- [ ] 复核 rules-stack 落地 audit log（2026-09-05，grill auto）：AGENTS.md v4.0.0 瘦身 / `dev.ps1 verify-rules` / ADR-022 / harness-matrix。**本地已提交未推送**——无否决后 push 到 origin/main。
- [ ] 观察 1-2 个会话：AGENTS.md 瘦身后 agent 是否经 §3 指针去 `Docs/lessons/` 取坑位全文（防"全表靠内联"回潮，ADR-022 后续）。
- [ ] 观察若干会话：确认无 harness 再向 `.workbuddy/memory/` 写正文（若复发 → 考虑 Junction 收口，ADR-019 后续）。**2026-09-05 检查：合规**——仅一行指向 journal 的指针（183B），无正文复发。

## 已完成（历史保留）

- [x] dev.ps1 真实终端端到端确认（2026-09-05）：`dev.ps1 build` → 0 警告 0 错误 6.8s；`dev.ps1 commit` 路径本次落地 NEXT 更新时 dogfood 通过。此前「沙箱禁 native spawn」限制仅在旧会话环境，按需确认模式可直接跑。
- [x] Pulsar.E2E 运行时端到端验证（2026-09-05，run `20260905-013738`，**PASS 18.6s**）：`radial-menu-open-via-command.json` 12 步全过——debug 实例启动、fixture 注入、录屏、命令通道开菜单、`Pulsar.Slot.0` UIA 断言、截图、关闭。产物在 `Pulsar/Pulsar.E2E/artifacts/`（已 gitignore）。截图右下角可见径向菜单轮盘（用户视觉确认），与 UIA 断言一致。
- [x] `ValidateOnboardingInvariants` 无人调用 gap（2026-09-05 核实已闭环）：`ConfigService.cs:300` 已在配置加载路径调用并打 `[ConfigInvariants]` 警告日志，NEXT 旧条目过时。
- [x] journal 轮转 + worktree 并行纪律（ADR-021，2026-09-04 23:53，commit `09ddae8`）：会话仪式改 tail + NEXT.md，日文件 ~15KB 归档轮转，AGENTS.md §10 worktree 纪律，全程经独立 worktree 实施并 ff 合并 main。
- [x] 候选 O/N/M/L 全部落地（2026-09-04 23:0x，commit `8b24da6`/`bf681ce`/`5e65a98`/`c5a35a4`）。
- [x] 全量测试死锁修复「第二次提交」（2026-09-04，commit `5412911`，含 `XUNIT_APPLICATION_CURRENT_DEADLOCK.md`）。
- [x] 构建警告清理至 0（2026-09-04 23:32，commit `784343f` + `470fba6`）。
