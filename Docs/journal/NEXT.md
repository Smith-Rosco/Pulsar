# Journal NEXT（持久待办，单一权威）

> **用途**：跨会话「下一步」的单一权威清单。每个 `## Session` block 不再重复罗列待办，只在本文件更新——完成即划线 `~~…~~`，新增即在末尾追加（ADR-021）。
> **会话仪式**：Session start 必读本文件（小体积）；涉及具体某天的「做了什么/坑」才去读对应 `Docs/journal/YYYY-MM-DD.md` 尾部或归档。

## 待办

- [ ] `scripts/dev.ps1`：封装 build/test/commit + env 前缀（规避 PS 5.1 无 `&&`），journal 11:52 起的长期遗愿。
- [ ] Pulsar.E2E 运行时端到端验证：`dotnet run --project Pulsar.E2E -- run Workflows/radial-menu-open-via-command.json --app <Pulsar.exe>`（真实交互会话，visual-ai-ui-automation 收尾）。
- [ ] 观察若干会话：确认无 harness 再向 `.workbuddy/memory/` 写正文（若复发 → 考虑 Junction 收口，ADR-019 后续）。
- [ ] `ValidateOnboardingInvariants` 当前是 `public static` 公共方法但无人调用（ADR-018 提到的 gap；不在原候选范围内）。

## 已完成（历史保留）

- [x] 候选 O/N/M/L 全部落地（2026-09-04 23:0x，commit `8b24da6`/`bf681ce`/`5e65a98`/`c5a35a4`）。
- [x] 全量测试死锁修复「第二次提交」（2026-09-04，commit `5412911`，含 `XUNIT_APPLICATION_CURRENT_DEADLOCK.md`）。
- [x] 构建警告清理至 0（2026-09-04 23:32，commit `784343f` + `470fba6`）。
