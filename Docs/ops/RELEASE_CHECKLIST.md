# 发布 Checklist（Release Checklist）

> 发布是把构建产物交给外部用户的**外部契约动作**——执行发布前按「暂停等确认」纪律向用户/维护者确认，勾选完才动手。

## 使用方式

每个版本发布前从上到下逐条勾选；任何一条不过 → 停止发布，回到修复流程。发布完成后在 `Docs/journal/` 当日 session 留档并收口。

## 1. 发布前门槛（Quality Gates）

- [ ] `scripts/dev.ps1 build`：0 警告 0 错误（WorkBuddy 环境下用 bash + env 前缀 dotnet 等效验证，见 AGENTS.md）
- [ ] `scripts/dev.ps1 test`：全量测试通过（当前基线见最新 journal；禁止引用未在本机复现过的数字）
- [ ] Fan 级联子菜单 QA 通过（人工，QA checklist 留档）
- [ ] 更新链路 QA 通过（对 `Smith-Rosco/Pulsar` 打高于本地的测试 tag 验证 UpdateAvailable 全链路，QA checklist 留档）
- [ ] 手册链接有效：`Docs/manual/` 各章互链 + 截图引用（`Docs/media/release/`）均可解析

## 2. 资产清单（Assets）

- [ ] `Pulsar-v{version}-Setup.exe`（安装版，Inno 打包）
- [ ] `Pulsar-v{version}-Standalone-win-x64.zip`（独立版，self-contained single-file）
- [ ] SHA256 清单（覆盖以上全部资产，附在 Release 正文）
- [ ]（可选）演示视频链接（`Docs/media/release/videos/` 或外部托管）
- [ ] 发布说明：基于 `Docs/ops/TEMPLATE_RELEASE_NOTES.md` 模板成稿（用户视角亮点 + 已知问题 + 系统要求）

## 3. 发布后动作（Post-Release）

- [ ] 打 tag（`v{version}`）并推送
- [ ] GitHub Release 发布（说明 + 资产上传）
- [ ] 验证 Release 页下载可用
- [ ] 验证更新检测对已发布 tag 的识别（对齐 app-update-service spec）
- [ ] `CHANGELOG.md` 归位（Unreleased → 对应版本）
- [ ] Announce（README/社区渠道，主题落在「老旧系统自动化」叙事）
- [ ] `Docs/journal/` 收口：M1 验收标准逐条对照（全新机器可安装并自动更新；文档可无痛上手）

## 交叉引用

- 发布 ADR / 架构决策：`Docs/decisions/`（安装器选型、禁裁剪、三级容灾更新架构等）
- 会话记录与验收留痕：`Docs/journal/YYYY-MM-DD.md`（ADR-019 单一来源）
- 文档路由规范：`Docs/CONTRIBUTING.md`
- 手册（发布链接指向）：`Docs/manual/README.md`
