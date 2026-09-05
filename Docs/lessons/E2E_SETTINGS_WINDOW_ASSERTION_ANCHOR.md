# E2E 设置窗口断言锚点——用窗口级真实控件，不用复合控件的 AutomationId

**日期：** 2026-09-05  
**严重程度：** 中（E2E 断言可靠性）  
**影响范围：** Pulsar.E2E 设置页工作流（`settings-analytics-*-dark`）、后续所有设置窗口 E2E  
**状态：** 已解决

---

## 问题描述

P0/P1 分析页 E2E 打通时，需要"走到设置窗口并确认窗口已打开"。最初尝试用
`Pulsar.Settings.NavView`（Wpf.Ui `NavigationView` 复合控件）的 AutomationId 断言窗口存在，
但 UIA 树中**找不到该节点**——`NavigationView` 是一个大型复合控件，其模板不把外层
AutomationId 透传到可定位的 UIA 元素，`AutomationElement.FromHandle` / `FindFirst` 均落空，
窗口断言直接超时失败。

同时，给导航项加的 `Pulsar.Settings.Nav.<Id>` AutomationId 也**不是开箱即用**的锚点——
它挂在导航项容器上，可见性/命中测试受 Wpf.Ui 模板控制，不能保证断言时一定可定位。

## 根本原因

- Wpf.Ui `NavigationView` 是复合控件（内部含 `NavigationViewItem`、Pane、Header 等大量子元素），
  它自己的 `AutomationProperties.AutomationId` 不映射到任何真实 UIA 元素；
  控件的 UIA 暴露由模板中的内部元素决定，外部附着的 AutomationId 被吞掉。
- UIA 断言必须锚定"**真实存在于 UIA 树**"的控件，而不是"你认为应该存在的"控件。

## 解决方案

用**窗口级、模板无关的真实控件**作为断言锚点：

- 设置窗口右上角保存按钮始终存在且暴露 AutomationId：
  `AutomationProperties.AutomationId="Pulsar.Settings.SaveChangesButton"`（`SettingsWindow.xaml`）。
  该按钮是普通 Wpf.Ui Button，UIA 树一定可定位 → E2E 用 `assert` 步骤断言它 `visible`，
  作为"设置窗口已打开"的可靠信号。
- 导航项/页面级 AutomationId（`Pulsar.Settings.Nav.*`、`Pulsar.Settings.Analytics.*`）保留，
  但只用于"点击/定位具体控件"，不用于"断言窗口存在"。

工作流中的用法（`Workflows/settings-analytics-data-dark.json`）：

```json
{ "type": "command", "id": "open-settings-window", "command": "open-settings" },
{ "type": "wait", "id": "settle-settings-window", "durationMs": 1500 },
{ "type": "assert", "id": "assert-settings-window", "automationId": "Pulsar.Settings.SaveChangesButton", "expected": "visible" }
```

## 修改的文件

| 文件 | 变更说明 |
|------|---------|
| `Pulsar/Pulsar/Views/SettingsWindow.xaml` | 保存按钮携带 `Pulsar.Settings.SaveChangesButton` AutomationId（E2E 断言锚点） |
| `Pulsar/Pulsar/Views/SettingsWindow.xaml.cs` | `BuildNavigationItems` 给导航项加 `Pulsar.Settings.Nav.<Id>` AutomationId |
| `Pulsar/Pulsar/Views/Pages/SettingsAnalyticsPage.xaml` | 空态/筛选/刷新控件加 `Pulsar.Settings.Analytics.*` AutomationId |
| `Pulsar/Pulsar.E2E/Workflows/settings-analytics-*-dark.json` | 断言锚点统一使用 `Pulsar.Settings.SaveChangesButton` |

## 架构教训

1. **断言锚点 = 模板无关的窗口级控件**。复合控件（NavigationView、DataGrid 模板等）的
   AutomationId 不可靠；用窗口固定存在的基础控件（保存按钮、标题、关闭按钮）做"窗口存在"断言。
2. **AutomationId 是纯附加元数据**：加 AutomationId 不改变用户可见行为，生产路径零风险，
   可以放心为 E2E 可测性添加。
3. **E2E 步骤分层**：`open-settings` 命令（DebugCommandServer）负责"打开"，
   `assert SaveChangesButton visible` 负责"确认已打开"，`click Nav.*` 负责"导航到页面"，
   各司其职，任一环节失败定位清晰。
