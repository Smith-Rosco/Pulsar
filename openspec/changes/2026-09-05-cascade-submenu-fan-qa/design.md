# Design — Cascade SubMenu Fan QA 收尾

## Context

方向三主干已落地（`SubMenuLayoutEngine` Fan/Ring 几何 + `CascadeSubMenuStrategy` + 分页），单测覆盖几何与策略（`SubMenuLayoutEngineTests` / `CascadeSubMenuStrategyTests` / `CascadeSubMenuEntryTests`），但渲染与真实交互链路从未经过人工验证。命中算法为纯几何（相对 `SubRingRadius`，DIP 输入），理论规避了高 DPI 像素命中问题——这正是 QA 要实证的假设。

## Goals / Non-Goals

**Goals:**
- 用最小人工成本实证 Fan/Ring 全链路行为与既有 spec 一致。
- 缺陷「发现即修」，修复回归既有 spec，不扩需求。

**Non-Goals:**
- 不做布局重构、不加新布局形态、不调默认值（Fan 仍是默认 `LayoutStyle`）。
- 不覆盖 NEXT.md 观察类待办（非实现任务）。

## Decisions

1. **`skip_specs: true`**：本变更是验证 + 回归修复，无任何需求级 delta；修复的判据就是 `cascade-submenu-layout` 既有 spec。备选方案「虚构一条 QA 流程需求」被否——openspec 约定明确禁止为通过校验而发明需求。
2. **QA 矩阵以 `qa-checklist.md` 为载体**（沿用 renderer-plugin-registry 变更的成熟格式）：用例编号 + 通过标准 + Change History，执行结果可追溯。
3. **缺陷分级处理**：命中/渲染错误 → 修复 + 回归测试；纯视觉微调（间距/字号）→ 若涉及 spec 语义记 ADR-011 Amendment，否则 journal 记录即可。

## Risks / Trade-offs

- 人工 QA 依赖真实环境（DPI / 双主题 / 多显示器），无法自动化——这是该欠账拖至今日的根因，本变更接受该成本。
- 若 QA 揭示分页语义缺陷，修复可能触及 `MenuSession` 命中路径，需全量回归（1059 基线）——工作量上浮已计入任务分节。
