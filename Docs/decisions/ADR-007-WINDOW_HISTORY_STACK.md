# ADR-007: 窗口历史栈机制 (Window History Stack)

**状态**: ✅ 已实施  
**日期**: 2026-03-08  
**决策者**: Architecture Team  
**影响范围**: WindowService, Quick Switch 功能

---

## 背景 (Context)

### 问题描述

用户报告了一个严重的 UX 问题：

> "我经常使用远程桌面，常常会发现从全屏下的远程桌面切换到最近打开的窗口，然后再触发切换到最近打开窗口的功能，没有返回到远程桌面。"

### 根本原因

经过深入分析，发现问题出在 `WindowService.SwitchToPreviousWindow()` 的实现逻辑：

1. **状态覆盖问题**：每次唤起 Pulsar 都会用当前前台窗口覆盖 `_previousWindowHandle`，导致历史丢失
2. **语义错误**：方法名叫 `SwitchToPreviousWindow()`，但实际行为是切换到 Z-Order 中的"下一个"窗口
3. **无法往返切换**：用户期望 `远程桌面 ↔ Chrome ↔ 远程桌面`，但实际行为是 `远程桌面 → Chrome → VSCode`

### 时序分析

```
T0: 远程桌面全屏 (前台)
    _previousWindowHandle = NULL

T1: 用户按热键 → Pulsar 唤起
    _previousWindowHandle = 远程桌面 HWND ✅

T2: Quick Switch 触发
    GetNextWindowInZOrder(远程桌面) → 切换到 Chrome ❌

T3: 用户再次按热键 → Pulsar 唤起
    _previousWindowHandle = Chrome HWND (远程桌面引用丢失！) ❌

T4: Quick Switch 触发
    GetNextWindowInZOrder(Chrome) → 切换到 VSCode ❌
    远程桌面永远无法通过 Quick Switch 返回
```

---

## 决策 (Decision)

采用 **窗口历史栈 (Window History Stack)** 机制，类似浏览器的前进/后退功能。

### 核心设计

```csharp
// WindowService.cs
private Stack<IntPtr> _windowHistory = new Stack<IntPtr>();
private const int MaxHistorySize = 10;
```

### 关键方法

#### 1. `RecordWindowActivation(IntPtr hwnd)`
- 记录窗口激活到历史栈
- 自动去重（栈顶相同窗口不重复记录）
- 排除 Pulsar 自身
- 限制栈大小为 10

#### 2. `SwitchToPreviousWindow()`
- 从历史栈弹出窗口
- 验证窗口有效性（`IsWindow()` + `IsAltTabWindow()`）
- 自动跳过已关闭的窗口
- Fallback 到 `_previousWindowHandle`

---

## 实施细节 (Implementation)

### 修改的文件

1. **WindowService.cs**
   - 新增 `_windowHistory` 栈和 `_historyLock` 锁
   - 实现 `RecordWindowActivation()` 方法
   - 重构 `SwitchToPreviousWindow()` 使用历史栈
   - 修改 `SetPreviousWindow()` 自动调用 `RecordWindowActivation()`

2. **IWindowService.cs**
   - 新增 `RecordWindowActivation(IntPtr hwnd)` 接口定义

3. **IsAltTabWindow() Bug 修复**
   - 修复 Cloaked 窗口判断逻辑（`return false` 而不是 `return true`）
   - Cloaked 窗口（虚拟桌面/UWP 挂起）不应出现在 Alt-Tab 列表

### 代码示例

```csharp
public void RecordWindowActivation(IntPtr hwnd)
{
    if (hwnd == IntPtr.Zero) return;
    
    // 排除 Pulsar 自身
    NativeMethods.GetWindowThreadProcessId(hwnd, out uint processId);
    if (processId == _currentProcessId) return;
    
    lock (_historyLock)
    {
        // 去重
        if (_windowHistory.Count > 0 && _windowHistory.Peek() == hwnd)
            return;
        
        _windowHistory.Push(hwnd);
        _logger.LogInformation("[WindowHistory] Recorded: {Hwnd}, Stack: {Size}", 
            hwnd, _windowHistory.Count);
        
        // 限制栈大小
        if (_windowHistory.Count > MaxHistorySize)
        {
            var temp = _windowHistory.ToArray();
            _windowHistory = new Stack<IntPtr>(temp.Take(MaxHistorySize).Reverse());
        }
    }
}

public void SwitchToPreviousWindow()
{
    IntPtr current = GetForegroundWindow_Native();
    
    lock (_historyLock)
    {
        // 弹出当前窗口
        if (_windowHistory.Count > 0 && _windowHistory.Peek() == current)
            _windowHistory.Pop();
        
        // 查找第一个有效的历史窗口
        while (_windowHistory.Count > 0)
        {
            IntPtr prev = _windowHistory.Pop();
            
            if (NativeMethods.IsWindow(prev) && IsAltTabWindow(prev))
            {
                ForceForegroundWindow(prev);
                // 将窗口推回栈（为下次切换做准备）
                _windowHistory.Push(prev);
                _windowHistory.Push(current);
                return;
            }
        }
    }
    
    // Fallback
    if (_previousWindowHandle != IntPtr.Zero && IsAltTabWindow(_previousWindowHandle))
        ForceForegroundWindow(_previousWindowHandle);
}
```

---

## 优势 (Benefits)

### 1. 符合用户直觉
- ✅ 支持多次往返切换（远程桌面 ↔ Chrome ↔ 远程桌面）
- ✅ 行为符合 Alt+Tab 的心智模型
- ✅ 自动处理窗口关闭场景

### 2. 架构优雅
- ✅ 单一职责：`_previousWindowHandle` 用于 PulsarContext，`_windowHistory` 用于 Quick Switch
- ✅ 线程安全：使用 `_historyLock` 保护栈操作
- ✅ 可扩展：未来可轻松扩展为窗口历史面板

### 3. 鲁棒性
- ✅ 自动验证窗口有效性
- ✅ 自动跳过已关闭的窗口
- ✅ Fallback 机制保证不会失败

---

## 测试场景 (Test Scenarios)

### 场景 1: 远程桌面往返切换
```
操作: 远程桌面 → 热键 → Chrome → 热键 → 远程桌面
预期: ✅ 成功返回远程桌面
实际: ✅ 通过
```

### 场景 2: 多窗口快速切换
```
操作: A → B → C → 热键 → B → 热键 → C
预期: ✅ 在最近两个窗口间切换
实际: ✅ 通过
```

### 场景 3: 窗口关闭处理
```
操作: A → B → 关闭 B → 热键
预期: ✅ 自动跳过 B，切换到 A
实际: ✅ 通过
```

### 场景 4: 历史栈满
```
操作: 激活 15 个窗口
预期: ✅ 只保留最近 10 个
实际: ✅ 通过
```

---

## 日志输出 (Logging)

新增详细的日志记录用于调试：

```
[WindowHistory] Recorded window: 0x12345 (Title: Chrome), Stack size: 3
[SwitchToPreviousWindow] Current foreground: 'Chrome' (0x12345)
[SwitchToPreviousWindow] History stack size: 3
[SwitchToPreviousWindow] Checking history window: 'Remote Desktop' (0x67890)
[SwitchToPreviousWindow] Switching to history window: 'Remote Desktop' (0x67890)
```

---

## 相关问题修复 (Related Fixes)

### Bug: Cloaked 窗口判断错误

**位置**: `WindowService.cs:510`

**问题**: Cloaked 窗口（虚拟桌面上的窗口）被错误地标记为"有效的 Alt-Tab 窗口"

**修复**:
```csharp
// Before
if (isCloakedVal != 0)
    return true;  // ❌ 错误

// After
if (isCloakedVal != 0)
    return false;  // ✅ 正确
```

---

## 未来扩展 (Future Enhancements)

### 1. 窗口历史面板
显示最近访问的 10 个窗口，支持鼠标点击切换

### 2. 快捷键前进/后退
- `Ctrl+Tab`: 前进到下一个窗口
- `Ctrl+Shift+Tab`: 后退到上一个窗口

### 3. 持久化历史
跨会话保存窗口历史（需要序列化窗口标识符）

### 4. 智能排序
根据窗口使用频率和时间加权排序

---

## 参考资料 (References)

- [Windows Z-Order Documentation](https://docs.microsoft.com/en-us/windows/win32/winmsg/window-features#z-order)
- [Alt-Tab Window Filtering](https://devblogs.microsoft.com/oldnewthing/20071008-00/?p=24863)
- [AGENTS.md](../AGENTS.md) - 架构决策指南

---

## 变更历史 (Change Log)

| 日期 | 版本 | 变更内容 |
|------|------|----------|
| 2026-03-08 | 1.0.0 | 初始实施：窗口历史栈机制 |
| 2026-03-08 | 1.0.1 | 修复 Cloaked 窗口判断 Bug |

---

**审核者**: Architecture Team  
**批准者**: Project Lead  
**状态**: ✅ 已合并到主分支
