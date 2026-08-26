# 幽灵窗口：用通用物理规则根治，不要硬编码类名补丁

**日期**: 2026-08-26

## Symptom

快速切换（Quick Switch）或进程切换偶尔落到一个"看不见"的窗口上：用户期望切到某个应用，但屏幕毫无变化。
日志显示切换"成功"（`SetForegroundWindow` 返回 true、前台确实是目标 HWND），但目标窗口用户根本看不见。
先后出现过：

- WPS 的 `KxWppQuickHelpBarContainer`（WPS 演示的 helper 窗口）
- Chrome 的 `Chrome Legacy Window`（Chrome 的 legacy 宿主窗口，0x0 / 屏幕外）

## Root Cause

这些窗口 **`WS_VISIBLE` 已置位**、不被 DWM cloaked、不是工具窗口、没有 owner —— 它们通过了 `IsAltTabWindow`
的全部通用检查（可见 → 非 cloaked → 非 tool window → 无 owner），于是污染 MRU 历史、成为快速切换目标，
把真正的目标窗口（如 Windows Security 凭据窗口）挤掉。**共性根因是"物理上不可见"：矩形为零尺寸或完全位于
虚拟屏幕之外**，而通用判定里根本没有这一条。

### 错误做法：逐个硬编码类名补丁

第一次遇到（KxWppQuickHelpBarContainer）时，在 `SystemWindowClassBlacklist` 里硬编码类名解决。这暴露了两个问题：

1. 每个新幽灵都要往代码里加一个类名 → 持续"打地鼠"，且需要重新发版。
2. `Chrome Legacy Window` 的类名（`Chrome_WidgetWin_1`）与**所有真实 Chrome 窗口相同**——按类名排除
   会误伤整个 Chrome。类名根本不是这类幽灵的区分信号；**标题或矩形**才是。

## Fix

把"物理可见性"做成判定链里的一条**通用硬规则**，而不是按窗口逐个打补丁：

```
结构规则（自身/可见/cloaked/工具/owned）
  → 物理可见性：!IsIconic 且（矩形为空 ∨ 零尺寸 ∨ 与虚拟屏幕无正重叠）→ ExcludedOffScreen
  → 系统类名黑名单
  → 进程黑名单（调用方作用域）
  → 用户规则（WindowClass / TitlePattern / ProcessName，Allow 绝对放行）
```

- 一次根治 KxWppQuickHelpBarContainer 与 Chrome Legacy Window 同类问题（它们正是"可见但屏幕外/零尺寸"）。
- 类名/标题类疑难杂症交给**用户规则**（Window Inspector 一键生成进程+类+标题正则的规则），不依赖发版。
- 最小化窗口豁免（`IsIconic`），避免误伤合法的最小化 Alt-Tab 目标。

## 设计要点

- **单一判定模块** `IWindowEligibilityPolicy.Evaluate(WindowEligibilitySnapshot, processBlacklist?)`，
  快速切换 / 发现枚举 / 进程激活三处共用；`FromHwnd` 是唯一 native seam。原来"每处各抄一遍判定"是
  bug 反复出现的温床。
- **标题规则读取成本控制**：`GetWindowText` 是跨进程 SendMessage、可能阻塞。仅当存在 TitlePattern 规则
  （`HasTitleDependentRules`）时才在热路径读标题；发现路径二次判定。
- **激活后校验**：`ActivateWindowDetailedAsync` 成功后复核物理可见性，不可见则逐出 MRU + 托盘提示，
  打破"切到幽灵 → 又进历史 → 再切幽灵"的循环。

## Deep Dive

- `Docs/guides/WINDOW_SWITCHING_REFACTORING.md` —— Window Eligibility 架构与关键文件
- `CONTEXT.md` —— Window Eligibility / Exclusion Rule 领域术语
- 相关测试：`Pulsar.Tests/Services/WindowEligibilityPolicyTests.cs`
