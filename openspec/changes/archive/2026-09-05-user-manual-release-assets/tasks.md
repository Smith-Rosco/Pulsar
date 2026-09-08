# Tasks — User Manual & Release Assets

## 1. 用户手册（中英）

- [x] 1.1 `Docs/manual/` 骨架：`README.md`（目录）+ 任务导向章节结构（安装与首次运行 / 我要跑宏 / 我要登旧系统 / 我要切窗口 / 常见问题）
- [x] 1.2 中文手册正文：含 SmartScreen「更多信息→仍要运行」、管理员权限说明、卸载数据保留说明；截图复用 Change 2 发布资产
- [x] 1.3 英文手册同步（同构同叙事）
- [x] 1.4 手册入口接线：About 页外链 + 首启引导完成页外链 + README 文档区链接；`scripts/dev.ps1 build` + 定向 UI 测试确认链接改动无回归

## 2. 发布 checklist

- [x] 2.1 `Docs/ops/RELEASE_CHECKLIST.md`：发布前门槛（0 警告构建 / 全量测试 / Fan QA 过 / 更新链路 QA 过）、资产清单（双形态 + SHA256 + 手册链接）、发布后动作（tag、Announce、CHANGELOG 归位）
- [x] 2.2 checklist 与 ADR/journal 体系交叉引用（发布属外部契约动作，遵循「暂停等确认」纪律）

## 3. 首个 GitHub Release 组装

- [x] 3.1 Release 说明成稿（基于模板 + CHANGELOG 提炼，用户视角亮点 + 已知问题 + 系统要求）— **已完成**：`Docs/ops/RELEASE_NOTES-1.11.0.md`，含三支柱亮点、双形态下载表、系统要求、新增/改进/修复完整清单、安全隐私、已知限制（未签名 SmartScreen / ISCC 未实机验证 / 真实网络 QA 待验证 / 视频待录制）
- [x] 3.2 资产上传清单核对：`Pulsar-v{version}-Setup.exe` / `Pulsar-v{version}-Standalone-win-x64.zip` / SHA256 清单 /（可选）演示视频链接
- [x] 3.3 打 tag 并发布（人工执行，走 checklist）；验证 Release 页下载 + Change 3 更新检测对已发布 tag 的识别
- [x] 3.4 journal 收口：M1 验收标准逐条对照（全新机器可安装并自动更新；文档可无痛上手）
  - **2026-09-08 对照收口**（以 journal 真机记录为验收证据）：① 全新机器可安装并自动更新 —— v1.11.0 / v1.12.0 均已在 GitHub Release 发布并验证（三资产、notes 中文无 BOM）；更新链路 UpdateAvailable 全链路已在 Change 1 的 4.3 真机验证通过（v1.11.1 测试 release 触发托盘通知）。② 全新机器安装 —— v1.12.0 Standalone zip 在无 .NET 新机器解压运行通过（Change 2 的 1.3）。③ 文档可无痛上手 —— 用户手册 + README 双语重写 + RELEASE_NOTES 模板已入库（本 change 1.x/2.x）。剩余未覆盖项（installer 覆盖升级真机、手册多读者试用）留作后续观察，不阻塞 M1。
