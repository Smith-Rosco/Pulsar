# E2E 低干扰模式——UIA InvokePattern + 不激活，边玩边测

**日期：** 2026-09-10  
**严重程度：** 中（E2E 可用性 / 用户干扰）  
**影响范围：** `Pulsar.E2E` 全部工作流（opt-in）、`DebugCommandServer.open-settings`、`AppLauncher`  
**状态：** 已解决

---

## 问题描述

默认 E2E 工作流对用户干扰很大，无法"一边打游戏一边跑测试"：

1. **点击移动物理鼠标**：`UiaDriver.ClickElement` 走 FlaUI 的 `Mouse.MoveTo` + `Mouse.Click`（真实 `SendInput`），
   会把用户的鼠标从游戏中挪走，并可能误触发游戏内操作。
2. **打开窗口抢焦点**：`DebugCommandServer` 的 `open-settings` 用 `window.Show()` + `window.Activate()`，
   强制把设置窗口切到前台 → 全屏游戏被切出（DXGI 全屏直接最小化）。

## 根本原因

E2E 的初衷是"像真人一样驱动界面"（真实输入、真实焦点），这对**可靠性**是优点，
但对**用户在场时的可用性**是硬伤。真实 `SendInput` 与前台激活都是进程外、全局副作用，
没有任何开关能"只作用于被测窗口"。

关键洞察：**这两件事其实可以解耦**。UIA 协议本身提供了不依赖物理输入、不依赖前台激活的驱动通道：

- **`IInvokePattern.Invoke()`**：直接驱动控件的 automation peer，等价于"程序化点击"。
  不移动光标、不需要窗口前台（WPF 的 peer 在窗口可见即可用）。
- **`ShowActivated = false`**：窗口显示但不夺取焦点，前景应用保持不变。

## 解决方案

新增 **`--low-interference`**，全程 **opt-in**（默认关闭 → 现有工作流零行为变化）：

| 层 | 变更 |
|---|---|
| `Program.cs` | `run` 命令解析 `--low-interference`（布尔开关，无值；从 `i += 2` 配对循环中先行剔除） |
| `RunOptions.LowInterference` | 新增属性，传给 Runner |
| `AppLauncher.Launch(..., lowInterference, log)` | 追加 `--ui-debug-low-interference` 到子进程参数 |
| `UiaDriver.PreferInvokePattern` | `ClickElement` 优先 `TryInvoke`（`Patterns.Invoke.IsSupported` → `Pattern.Invoke()`），失败**静默回退**物理点击；计数 `InvokeClickCount` / `PhysicalClickCount` 便于诊断 |
| `DebugModeOptions.UiDebugLowInterferenceFlag` | 复用 DI 单例；仅与 `--ui-debug` 同时生效 |
| `DebugCommandServer.open-settings` | 低干扰时 `ShowActivated = false` + `Show()`（不 `Activate()`） |

用法：

```bash
Pulsar.E2E.exe run --workflow Workflows\settings-analytics-data-dark.json --app <Pulsar.exe> --low-interference
```

## 关键设计决策

1. **回退而非报错**：`TryInvoke` 捕获所有异常返回 `false`。自定义命中测试面板（如轮盘 `SlotOrb`）
   可能不暴露 InvokePattern，或 provider 声明支持却运行期失败——此时静默降级为物理点击，
   保证工作流仍能跑通（只是那一击会动鼠标）。**宁可降级，不可中断**。
2. **开关必须 opt-in**：低干扰改变了"点击语义"（InvokePattern vs 真实鼠标），
   可能掩盖依赖真实输入路径的缺陷。默认保持原行为，只在用户明确要求"别打扰我"时启用。
3. **`ShowActivated` 而非 `WM_MOUSEACTIVATE` 钩子**：后者能在点击时拒绝激活，但 E2E 打开窗口
   走的是命令管道（非鼠标点击），`ShowActivated` 已足够且实现简单、无 HwndSource 生命周期负担。
4. **`--ui-debug-low-interference` 必须伴随 `--ui-debug`**：单独出现时被忽略（`FromArgs` 判据），
   避免生产环境误触发。

## ⚠️ 实测踩坑：UIA 模式**不能**触发鼠标事件驱动的导航

第一版实现后 E2E **在 `assert-filter-combo` 处失败**：点击 `Pulsar.Settings.Nav.Analytics`
后分析页从未出现。诊断包（`uia-tree.txt`）显示导航项是 `type=DataItem`，且点击后页面仍是 Slots。

**根因**：`SettingsWindow.NavigationItem_PreviewMouseLeftButtonUp` 是**鼠标事件处理器**
（`item.PreviewMouseLeftButtonUp += ...`）。它只响应真实鼠标事件：

- `InvokePattern.Invoke()` → WPF 对该 `DataItem` **不暴露** InvokePattern（控件类型不匹配）；
- 补上的 `SelectionItemPattern.Select()` → 能改 UIA 选中态，但**不触发** `PreviewMouseLeftButtonUp`，
  也不会走到 `_shellViewModel.NavigateAsync(...)`。

即：**E2E 里的导航点击此前一直依赖物理鼠标，从未真正走 UIA 通道**。

**解法**（沿项目既有"在汇聚点注入等价事件"范式）：新增调试命令
`{"command":"nav-settings","args":{"pageId":"Analytics"}}`，由 `DebugCommandServer` 直接调用
`SettingsWindow.NavigateToPageAsync(pageId)` → 复用与鼠标/键盘处理器**同一个**
`_shellViewModel.NavigateAsync` 汇聚点。工作流里用 `command` 步骤替代 `click` 步骤：

```json
{ "type": "command", "id": "click-analytics-nav", "command": "nav-settings", "args": { "pageId": "Analytics" } }
```

**教训**：**动手前先确认控件是否支持你要用的 UIA 模式**（`Patterns.X.IsSupported`），
并确认应用侧的处理器挂在什么事件上——`InvokePattern` 只对"真正实现 Invoke 语义"的控件有效，
对"用鼠标事件自己实现导航"的自定义控件无效。回退到物理点击能"跑通"，但会静默破坏低干扰承诺
（本次正是静默回退掩盖了失败，直到断言超时才暴露）。

## 验证结果（2026-09-10）

| 项 | 结果 |
|---|---|
| build | **0 警告 / 0 错误** |
| 全量测试 | **1500/1500**（基线 1494 + 新增 6） |
| 低干扰 E2E（`settings-analytics-data-dark`） | **PASS 24.0s**，14 步全过（nav-settings 导航 → 断言 FilterCombo/RefreshButton → scroll → dump → screenshot → exit） |
| InvokePattern 实证 | 临时探针工作流对 `RefreshButton`（Wpf.Ui Button）走 `click` 步骤 **PASS 24.1s** → 按钮类控件 InvokePattern 通道确认可用 |

## 修改的文件

| 文件 | 变更说明 |
|------|---------|
| `Pulsar/Pulsar/Core/Debug/DebugModeOptions.cs` | 新增 `UiDebugLowInterferenceFlag`、`LowInterference` 属性 |
| `Pulsar/Pulsar/Services/DebugCommandServer.cs` | 构造注入 `DebugModeOptions`；`open-settings` 低干扰分支；新增 `nav-settings` 命令 |
| `Pulsar/Pulsar/Views/SettingsWindow.xaml.cs` | 新增 `NavigateToPageAsync(pageId)` 公开导航入口（供调试命令走同一汇聚点） |
| `Pulsar/Pulsar.E2E/Driver/UiaDriver.cs` | `PreferInvokePattern` + `TryInvoke`（Invoke → SelectionItem）+ 点击计数 |
| `Pulsar/Pulsar.E2E/Driver/AppLauncher.cs` | `Launch` 重载（保留 4 参兼容重载）+ 参数追加 |
| `Pulsar/Pulsar.E2E/Runner/WorkflowRunner.cs` | `RunOptions.LowInterference` + 接线 |
| `Pulsar/Pulsar.E2E/Program.cs` | CLI 解析 + usage |
| `Pulsar/Pulsar.E2E/Workflows/settings-analytics-data-dark.json` | 导航步骤由 `click` 改 `command nav-settings` |
| `Pulsar/Pulsar.Tests/E2E/LowInterferenceModeTests.cs` | 6 例：标志联动 / 顺序无关 / 单独无效 |

## 架构教训

1. **E2E 的"真实度"应可调，而非一刀切**。可靠性与低干扰是两种正交的测试模式，
   用开关切换而不是二选一。
2. **UIA InvokePattern 是"无副作用点击"的标准答案**：不需要前台、不动光标。
   需要真实输入的场景（全局热键、跨进程手势）才必须走 `SendInput`。
   设计 E2E 步骤时先问"这一步真的需要物理输入吗"。
3. **新的可选开关默认必须关闭**，且要有一个"单独出现即无效"的护栏，
   防止误配置把生产行为带偏（本处由 `--ui-debug` 前置条件提供）。
4. **静默回退是双刃剑**：它保证工作流不中断，但也会**掩盖**"该用 UIA 却走了物理输入"。
   诊断手段＝点击计数（`InvokeClickCount` / `PhysicalClickCount`）+ 断言超时的诊断包。
   若发现低干扰模式下仍动鼠标，先查这两项计数，而不是猜。
5. **UIA 模式与控件实现方式必须对齐**（本页踩坑）：`InvokePattern` 只对真正实现 invoke 语义的
   控件有效；自定义控件若用 `PreviewMouseLeftButtonUp` 自己实现行为，UIA 模式无法触发，
   需在应用侧补一个"同一汇聚点"的命令入口（本处 `nav-settings`）。
