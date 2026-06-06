# 前台窗口激活可靠性问题

**日期：** 2026-06-06  
**严重程度：** 高  
**影响范围：** 全局窗口切换、WinSwitcher 插件  
**状态：** 已解决

---

## 问题描述

窗口切换（WinSwitcher）在 Release 构建（自包含单文件发布）中不稳定：有时正常切换，有时只有目标窗口任务栏图标闪烁，屏幕无变化。Debug 模式下正常运行。

### 症状
1. `SetForegroundWindow` 返回 `false`（而非抛异常），目标窗口任务栏图标闪烁
2. 同一应用多次切换可能结果不同（时序相关）
3. 某些应用（如网易云音乐）始终失败，即使 `AttachThreadInput` 成功
4. Release 单文件构建中稳定复现，Debug 模式正常

---

## 根本原因

问题是**三重嵌套 Bug**，共同导致：

### Bug 1：前台锁竞争时序问题

`WindowSwitchStrategy.ExecuteAsync`（`SlotStrategies.cs:139`）先隐藏菜单再激活窗口：

```csharp
context.IsVisible = false;          // ← 隐藏 Pulsar，Windows 开始转移前台
_windowService.ActivateWindow(_window);  // ← 此时 Pulsar 可能已失去前台
```

`IsVisible = false` 使 Pulsar 窗口消失，Windows 在几毫秒内将前台转给其他窗口。紧接着的 `SetForegroundWindow` 取决于 Pulsar 是否仍持有前台——这是纯时序竞态。Debug 模式下调试器介入改变了时序，所以表现不同。

### Bug 2：Dismiss → ReleaseAsync 撤销切换

`IsVisible = false` 触发 `Dismiss()` → 100ms 淡出动画 → `ReleaseAsync()` 恢复到捕获的前台窗口。即使 `SetForegroundWindow` 瞬间成功，`ReleaseAsync` 也会在 100ms 后将前台切回去，撤销切换。

QuickSwitch 通过 `SetRestoreMode(NoRestore)` 正确处理了这一点，但 `WindowSwitchStrategy` 没有。

### Bug 3：`FocusManager` 主路径缺少 `LockSetForegroundWindow(LSFW_UNLOCK)`

`FocusManager.ActivateWindowAsync` 有两路径：
- **主路径**：`AttachThreadInput` 成功 → `AllowSetForegroundWindow(targetPid)` + `SetForegroundWindow`
- **回退路径**：`AttachThreadInput` 失败 → `FallbackActivate`

Release 模式中 `AttachThreadInput` 在 Dispatcher 线程上成功，主路径被命中。但主路径只用了 `AllowSetForegroundWindow`（仅授予目标进程权限），**缺少 `LockSetForegroundWindow(LSFW_UNLOCK)`**（Pulsar 进程级立即解除前台锁）。Release 模式下前台锁严格生效 → `SetForegroundWindow` 失败 → 任务栏闪烁。

Debug 模式下 `AttachThreadInput` 可能因调试器干扰而失败，回退到已包含 `LSFW_UNLOCK` 的路径。

### Bug 4：某些应用窗口拒绝标准激活

网易云音乐等应用（自绘窗口、`WS_EX_NOACTIVATE` 样式、主动调用 `LockSetForegroundWindow(LSFW_LOCK)`）即使 `AttachThreadInput` + `LSFW_UNLOCK` + `AllowSetForegroundWindow` 全部成功，`SetForegroundWindow` 仍返回 `false`。需要更激进的激活策略。

---

## 解决方案

### 修复 1：`WindowSwitchStrategy` 设置 `NoRestore`（`SlotStrategies.cs:136`）

```csharp
_windowService.SetFocusRestoreMode(FocusRestoreMode.NoRestore);
```

防止 `Dismiss` → `ReleaseAsync` 撤销切换。

### 修复 2：主路径添加 `LockSetForegroundWindow(LSFW_UNLOCK)`（`FocusManager.cs:250`）

```csharp
if (attached)
{
    try
    {
        _native.LockSetForegroundWindow(LSFW_UNLOCK);
        try { /* AllowSetForegroundWindow + SetForegroundWindow + BringWindowToTop retry */ }
        finally { _native.LockSetForegroundWindow(LSFW_LOCK); }
    }
    finally { _native.AttachThreadInput(currentThread, targetThread, false); }
}
```

### 修复 3：`ReleaseAsync` 智能检测（`FocusManager.cs:151-158`）

前台已被插件/动作改变时跳过恢复，不再无脑恢复到捕获窗口。

### 修复 4：`ForceActivate` 兜底路径（`FocusManager.cs:403-417`）

当标准路径 + `BringWindowToTop` 重试全部失败时触发：
1. `AllowSetForegroundWindow(-1)`（ASFW_ANY）
2. `keybd_event(VK_MENU)` 模拟 Alt 键按下/释放（授予 Pulsar 前台输入权限）
3. 等待 50ms 让输入事件传播
4. 重新调用 `SetForegroundWindow`

**关于 `keybd_event` 的安全性**：`ForceActivate` 在 `OnSyntheticEventBegin/End` 保护区内执行，全局键盘钩子的 `UpdateModifierTracker` 在抑制期间直接 return，不会污染 `_trackedAltDown` 修饰键状态追踪。之前重构移除 `keybd_event` 的风险（`_trackedAltDown` 污染）已被 `IModifierStateTracker` 架构妥善处置。

### 修复 5：`IFocusNativeAdapter` 新增 `LockSetForegroundWindow` 接口

添加进程级前台锁控制（`LSFW_UNLOCK/LOCK`）到适配器接口和 `WindowsFocusNativeAdapter` 实现。

---

## 修改的文件

| 文件 | 变更说明 |
|------|---------|
| `Services/Interfaces/IFocusNativeAdapter.cs` | 添加 `LockSetForegroundWindow(uint)` 接口 |
| `Services/WindowsFocusNativeAdapter.cs` | 实现 `LockSetForegroundWindow` P/Invoke + `LSFW_LOCK/UNLOCK` 常量 |
| `Services/FocusManager.cs` | 主路径添加 `LSFW_UNLOCK`、`ForceActivate` 兜底、`ReleaseAsync` 智能检测、升级关键日志为 `LogInformation` |
| `Services/WindowService.cs` | `ActivateWindowDetailedAsync` 添加详细日志 |
| `ViewModels/Strategies/SlotStrategies.cs` | `WindowSwitchStrategy` 设置 `NoRestore` 防止撤销、添加日志 |
| `Views/RadialMenuWindow.xaml.cs` | `Dismiss` 等相关流程添加日志 |

---

## 架构教训

1. **前台锁是多层防御体系**：`AttachThreadInput`、`AllowSetForegroundWindow`、`LockSetForegroundWindow`、`SPI_SETFOREGROUNDLOCKTIMEOUT`、`keybd_event` 各自作用不同的层。单一机制无法覆盖所有应用类型。应按优先级串联使用，最后兜底是输入事件模拟。

2. **`IsVisible = false` + 前台操作的时序竞态**：隐藏窗口会触发 Windows 前台转移。在隐藏窗口之前必须确保焦点恢复模式已正确设置（`NoRestore`），避免后续的 `ReleaseAsync` 撤销操作。

3. **Release 模式的差异调试**：JIT 优化、线程调度、UI 渲染速度在 Release 和 Debug 模式下完全不同。前台锁相关的竞态条件只能在 Release 构建中复现。关键路径应使用 `LogInformation` 级别（而非 `LogDebug`）确保生产环境可追踪。

4. **`keybd_event` 在抑制期间安全**：之前重构移除 `keybd_event` 是因为它无条件污染 `_trackedAltDown`。但 `IModifierStateTracker.OnSyntheticEventBegin/End` 已在架构层面解决了这个问题——在抑制期间任何合成事件都会被安全忽略。因此 `keybd_event` 作为受保护的回退路径是合理的。
