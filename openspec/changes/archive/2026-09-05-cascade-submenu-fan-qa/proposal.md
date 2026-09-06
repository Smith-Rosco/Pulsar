# Cascade SubMenu Fan QA 收尾

## Why

`2026-09-02-cascade-submenu-layout` 的任务 5.2（Fan 人工 QA）至今未执行，是方向三（级联子菜单）唯一阻塞项，也是 IMPLEMENTATION_VERIFICATION.md 未落地清单 #1。级联子菜单已随 09-03 的入口重排进入首屏叙事（办公自动化工作台），Fan 布局作为默认 `LayoutStyle` 长期处于「有单测、无人工验证」状态——在 M1 对外发布前必须收口。

## What Changes

- **人工 QA 矩阵执行**：按本变更附带的 `qa-checklist.md` 由人工执行——2/3 子项 Fan 渲染与命中、4+ 回落 Ring、分页叠加、Dark/Light 双主题、高 DPI、外甩取消基准（根环）。
- **缺陷修复**：QA 发现的任何与 spec（`cascade-submenu-layout`）不符的行为，按「修复 + 回归测试」处理；修复不引入新需求，只回归既有 spec。
- **文档收口**：IMPLEMENTATION_VERIFICATION.md 未落地清单 #1 划线；若 QA 导致分页语义调整，以 ADR-011 Amendment 记录；journal 记录 QA 结果。
- NEXT.md 中「观察类待办」（AGENTS.md 瘦身取坑位行为、.workbuddy 合规）为会话观察项，**不在本变更范围**。

## Capabilities

（本变更为 QA 验证 + 缺陷回归修复，不引入新能力，也不改变任何既有需求——修复的目标行为就是 `cascade-submenu-layout` 既有 spec。故声明 `skip_specs: true`。）

## Impact

- **Affected code**：预期为零；若 QA 发现缺陷，涉及 `Services/SubMenuLayoutEngine.cs`、`ViewModels/Strategies/CascadeSubMenuStrategy.cs`、`ViewModels/MenuSession.cs`（命中/分页路径）及对应测试文件，修复后需全量回归（1059 基线）。
- **Non-code deliverables**：`qa-checklist.md`（执行记录）、`Docs/roadmap/IMPLEMENTATION_VERIFICATION.md`（清单划线）、journal、（条件性）ADR-011 Amendment。
- **Verification**：`dotnet build` 0 警告 0 错误；`dotnet test Pulsar.Tests` 全量通过；人工 QA 矩阵全过。
