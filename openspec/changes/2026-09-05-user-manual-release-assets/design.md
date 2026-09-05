# Design — User Manual & Release Assets

## Context

前置：Change 2 产出叙事与截图/视频，Change 3 产出更新链路，Change 4 产出双形态产物。本变更是 M1 的组装与收尾环节，交付面全部是文档与发布运营，代码改动趋近于零（两个外链）。

## Goals / Non-Goals

**Goals:**
- 非技术用户 10 分钟内完成第一个自动化（M2 验收口径的前置实现）。
- 首个 GitHub Release 资产齐备、可复用的发布 checklist。

**Non-Goals:**
- 在线文档站、搜索、版本化文档路由（先 Markdown 进仓，托管另议）。
- 首启引导流程改造（只加外链，不动 `first-launch-setup-wizard` 能力语义）。

## Decisions

1. **手册以 Markdown 进仓（`Docs/manual/`），中英同构**：与现有 `Docs/` 文档体系一致、可经 GitHub 直接渲染；后续如需在线站点再抽离。备选「直接写在 Release 页」被否——不可维护、不可本地化。
2. **手册结构按用户任务而非功能清单组织**：「我要跑宏」「我要登旧系统」「我要切窗口」三支柱场景为一级目录，功能参考退居附录——与叙事红线一致。
3. **发布 checklist 独立成文（`Docs/ops/RELEASE_CHECKLIST.md`）**：门槛（构建/测试/QA）、资产清单、发布后动作三段式；不写成 workflow 自动化（首个 Release 先人工走查一遍再考虑自动化）。
4. **Release 说明 = 模板 + CHANGELOG 提炼**：CHANGELOG 面向开发者（ADR 级细节），Release 说明面向用户（亮点 + 下载 + 已知问题），两者不互相替代。

## Risks / Trade-offs

- 手册与 UI 迭代会过时——发布 checklist 中加「手册截图/步骤复核」一项兜底。
- 首个 Release 版本号取发布时 csproj 版本，可能与本变更落地时的 1.10.0 不同——checklist 中以占位符约定。
