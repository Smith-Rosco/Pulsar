# Multi-Window Switching Logic Issue

**日期：** 2026-03-18  
**严重程度：** 中  
**影响范围：** WinSwitcher 插件、多窗口进程切换  
**状态：** 已解决

---

## 问题描述

WinSwitcher 类型的 slot 在切换到有多个窗口的进程时，无法正确切换到最近激活的窗口。

### 症状

1. 触发 WinSwitcher slot 时，总是切换到同一个窗口（通常是第一个创建的窗口）
2. 即使用户最近使用的是该进程的另一个窗口，也不会切换到那个窗口
3. 多窗口进程（如 Chrome、VS Code、File Explorer）的切换体验不符合用户预期

### 用户期望行为

当触发 WinSwitcher slot 切换到有多个窗口的进程时：
- 应该切换到该进程**最近激活**的窗口
- 如果当前已经在该进程的某个窗口，应该切换到该进程的**次最近激活**的窗口
- 切换顺序应该基于**真实的用户交互历史**，而不是窗口创建顺序或进程 PID

---

## 根本原因

### 技术细节

**问题代码位置：** `WindowService.cs` 的 `SwitchToProcessAsync` 方法（约 345-411 行）

```csharp
// ❌ 错误实现
var targetWindows = new List<(IntPtr Handle, int ZOrder, uint ProcessId)>();
int zOrder = 0;

foreach (var proc in processes)
{
    if (proc.MainWindowHandle != IntPtr.Zero)
    {
        // 使用枚举顺序作为 "Z-Order"
        targetWindows.Add((proc.MainWindowHandle, zOrder++, winPid));
    }
}

// 按 "Z-Order" 排序（实际上是枚举顺序）
var targetWindow = targetWindows.OrderBy(w => w.ZOrder).First();
```

### 根本原因分析

**1. Z-Order 概念混淆**
- 代码注释声称使用 "Z-Order tracking"，但实际上只是枚举顺序
- `Process.GetProcessesByName()` 返回进程的顺序是**任意的**（通常按 PID），不是 Z-Order
- 真正的 Z-Order 需要通过 `EnumWindows` 或 `GetWindow(GW_HWNDNEXT)` 获取

**2. Z-Order ≠ Activation Order**
- **Z-Order**: 窗口的视觉堆叠顺序（哪个窗口在最上层）
- **Activation Order**: 窗口的激活时间顺序（哪个窗口最近被用户交互）
- 对于窗口切换功能，**Activation Order 才是正确的指标**

**3. 忽略现有的窗口追踪基础设施**
- 系统已经有完善的 `WindowActivationMonitor`（全局窗口激活监听器）
- 系统已经有 `_windowRegistry`（全局窗口注册表），实时追踪每个窗口的 `LastActivationTime`
- 系统已经有 `_windowHistory`（窗口历史栈），记录用户的窗口切换历史
- **但 `SwitchToProcessAsync` 完全没有使用这些数据！**

**4. 架构不一致**
- `SwitchToPreviousWindow`（Quick Switch 功能）正确使用了窗口历史栈和注册表
- `SwitchToProcessAsync`（WinSwitcher 功能）使用了完全不同的（错误的）逻辑
- 两个功能应该共享相同的窗口追踪数据源

### 类比之前的问题

这个问题与之前修复的 "WinEvent Hook 线程上下文问题" 类似：

| 问题 | WinEvent Hook Issue | Multi-Window Switching Issue |
|------|---------------------|------------------------------|
| **症状** | Hook 注册成功但事件不触发 | 有窗口追踪数据但切换逻辑不使用 |
| **根因** | Hook 在错误的线程上注册 | 切换逻辑使用错误的数据源 |
| **本质** | 数据收集与消费的架构断层 | 数据收集与消费的架构断层 |

---

## 解决方案

### 架构修复

**核心思路：** 使用 Window Registry 的真实激活时间数据

#### 1. 修改 `SwitchToProcessAsync` 方法

```csharp
public Task<bool> SwitchToProcessAsync(string processName)
{
    return Task.Run(() =>
    {
        string targetName = processName.ToLower().Replace(".exe", "");
        var processes = Process.GetProcessesByName(targetName);
        
        if (processes.Length == 0)
        {
            _logger?.LogDebug("[SwitchToProcess] Process not found: {ProcessName}", processName);
            return false;
        }
        
        // ✅ 正确：使用 Window Registry 追踪真实激活时间
        IntPtr currentForeground = GetForegroundWindow_Native();
        
        // 收集所有窗口及其真实激活时间
        var targetWindows = new List<(IntPtr Handle, DateTime LastActivation, string Title)>();
        
        foreach (var proc in processes)
        {
            if (proc.MainWindowHandle != IntPtr.Zero)
            {
                IntPtr hwnd = proc.MainWindowHandle;
                
                // 从注册表查询真实激活时间
                DateTime lastActivation;
                if (_windowRegistry.TryGetValue(hwnd, out var entry))
                {
                    lastActivation = entry.LastActivationTime;
                }
                else
                {
                    // 新窗口：使用 DateTime.MinValue 降低优先级
                    lastActivation = DateTime.MinValue;
                }
                
                targetWindows.Add((hwnd, lastActivation, GetWindowTitle(hwnd)));
            }
        }
        
        if (targetWindows.Count == 0) return false;
        
        // 按真实激活时间降序排序（最近的在前）
        var sortedWindows = targetWindows.OrderByDescending(w => w.LastActivation).ToList();
        
        // 选择最近激活的非当前窗口
        var targetWindow = sortedWindows.FirstOrDefault(w => w.Handle != currentForeground);
        
        // Fallback: 单窗口情况
        if (targetWindow.Handle == IntPtr.Zero)
        {
            targetWindow = sortedWindows.First();
        }
        
        ForceForegroundWindow(targetWindow.Handle);
        return true;
    });
}
```

#### 2. 增强日志记录

```csharp
// 多窗口进程：记录所有候选窗口
if (targetWindows.Count > 1)
{
    _logger?.LogInformation("[SwitchToProcess] Multi-window process detected: {ProcessName} ({Count} windows)", 
        processName, targetWindows.Count);
    
    for (int i = 0; i < sortedWindows.Count; i++)
    {
        var w = sortedWindows[i];
        bool isCurrent = (w.Handle == currentForeground);
        _logger?.LogDebug("[SwitchToProcess]   [{Index}] '{Title}' - LastActivation: {Time}, IsCurrent: {IsCurrent}", 
            i, w.Title, w.LastActivation, isCurrent);
    }
}

// 记录最终选择
_logger?.LogInformation("[SwitchToProcess] Smart switch: {ProcessName} -> '{Title}' (LastActivation: {Time})", 
    processName, targetWindow.Title, targetWindow.LastActivation);
```

---

## 验证方法

### 日志检查点

**多窗口进程切换时应该看到：**

```
[INF] [SwitchToProcess] Multi-window process detected: chrome (3 windows)
[DBG] [SwitchToProcess]   [0] 'GitHub - Chrome' - LastActivation: 2026-03-18 14:32:15, IsCurrent: False
[DBG] [SwitchToProcess]   [1] 'Google - Chrome' - LastActivation: 2026-03-18 14:30:42, IsCurrent: True
[DBG] [SwitchToProcess]   [2] 'YouTube - Chrome' - LastActivation: 2026-03-18 14:28:10, IsCurrent: False
[INF] [SwitchToProcess] Smart switch: chrome -> 'GitHub - Chrome' (LastActivation: 2026-03-18 14:32:15)
```

### 功能测试

1. **测试场景 1：多窗口进程切换**
   - 打开 Chrome 浏览器，创建 3 个窗口（A, B, C）
   - 按顺序激活：A → B → C
   - 切换到其他应用（如记事本）
   - 触发 WinSwitcher slot 切换到 Chrome
   - **预期：** 切换到窗口 C（最近激活的）

2. **测试场景 2：当前窗口跳过**
   - 当前在 Chrome 窗口 C
   - 触发 WinSwitcher slot 切换到 Chrome
   - **预期：** 切换到窗口 B（次最近激活的，跳过当前窗口 C）

3. **测试场景 3：新窗口处理**
   - 当前在 Chrome 窗口 A
   - 创建新窗口 D（未在注册表中）
   - 触发 WinSwitcher slot 切换到 Chrome
   - **预期：** 切换到窗口 A（新窗口 D 优先级最低）

4. **测试场景 4：单窗口进程**
   - 打开记事本（单窗口）
   - 切换到其他应用
   - 触发 WinSwitcher slot 切换到记事本
   - **预期：** 正常切换到记事本窗口

---

## 关键知识点

### Window Activation Tracking 架构

Pulsar 的窗口追踪系统由三层组成：

1. **WindowActivationMonitor**（数据采集层）
   - 使用 `SetWinEventHook` 监听 `EVENT_SYSTEM_FOREGROUND` 事件
   - 实时捕获所有窗口激活事件
   - 必须在 UI 线程上启动（需要消息循环）

2. **Window Registry**（数据存储层）
   - `ConcurrentDictionary<IntPtr, WindowRegistryEntry>`
   - 存储每个窗口的 `FirstSeenTime` 和 `LastActivationTime`
   - 定期清理已关闭窗口（防止内存泄漏）

3. **Window History Stack**（用户历史层）
   - `Stack<IntPtr>` 存储最近 10 个激活的窗口
   - 用于 Quick Switch 功能（Ctrl+Q）
   - 去重逻辑：栈顶窗口不重复记录

### Z-Order vs Activation Order

| 概念 | 定义 | 获取方式 | 用途 |
|------|------|----------|------|
| **Z-Order** | 窗口的视觉堆叠顺序 | `EnumWindows`, `GetWindow(GW_HWNDNEXT)` | 窗口管理器、任务栏排序 |
| **Activation Order** | 窗口的激活时间顺序 | `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` | 窗口切换、Alt+Tab 行为 |

**关键区别：**
- Z-Order 是**空间概念**（哪个窗口在上面）
- Activation Order 是**时间概念**（哪个窗口最近被使用）
- 对于窗口切换功能，**Activation Order 更符合用户预期**

### Process.GetProcessesByName() 的陷阱

```csharp
var processes = Process.GetProcessesByName("chrome");
// ❌ 错误假设：processes 按 Z-Order 或激活时间排序
// ✅ 实际情况：processes 按任意顺序（通常是 PID）排序
```

**正确做法：**
- 使用 `Process.GetProcessesByName()` 仅用于**查找进程**
- 使用 `WindowRegistry` 或 `WindowHistory` 获取**激活顺序**

---

## 设计原则

### 1. 数据源一致性原则

**规则：** 同一类功能应该使用相同的数据源

```csharp
// ✅ 正确：Quick Switch 和 WinSwitcher 都使用 Window Registry
public void SwitchToPreviousWindow()
{
    IntPtr targetWindow = FindValidHistoryWindow(realCurrentWindow);
    // 使用 _windowHistory 和 _windowRegistry
}

public Task<bool> SwitchToProcessAsync(string processName)
{
    // 使用 _windowRegistry.LastActivationTime
}
```

### 2. 数据采集与消费分离原则

**规则：** 数据采集（Monitor）和数据消费（Service）应该解耦

```
WindowActivationMonitor (采集)
    ↓ WindowActivated 事件
WindowService.OnGlobalWindowActivated (处理)
    ↓ 更新
Window Registry (存储)
    ↓ 查询
SwitchToProcessAsync / SwitchToPreviousWindow (消费)
```

### 3. 显式数据流原则

**规则：** 数据流向应该通过代码和注释明确表达

```csharp
// [Refactor] Smart Window Switching for Multi-Window Processes
// Uses Window Registry to track real activation times (via WindowActivationMonitor)
public Task<bool> SwitchToProcessAsync(string processName)
{
    // 从注册表查询真实激活时间
    if (_windowRegistry.TryGetValue(hwnd, out var entry))
    {
        lastActivation = entry.LastActivationTime;
    }
}
```

---

## 相关文件

| 文件 | 修改内容 |
|------|----------|
| `Services/WindowService.cs` | 重构 `SwitchToProcessAsync` 方法（345-411 行） |
| `Plugins/Core/WinSwitcher/WinSwitcherPlugin.cs` | 无需修改（调用接口不变） |

---

## 参考资料

- [WINEVENT_HOOK_THREAD_CONTEXT.md](./WINEVENT_HOOK_THREAD_CONTEXT.md) - 相关的窗口追踪问题
- [SetWinEventHook - Microsoft Docs](https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook)
- [EVENT_SYSTEM_FOREGROUND](https://docs.microsoft.com/en-us/windows/win32/winauto/event-constants)
- [Process.GetProcessesByName - Microsoft Docs](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.getprocessesbyname)

---

## 经验教训

1. **不要假设 API 的返回顺序** - `Process.GetProcessesByName()` 不保证任何特定顺序
2. **Z-Order ≠ Activation Order** - 窗口堆叠顺序和激活顺序是两个不同的概念
3. **复用现有基础设施** - 系统已有窗口追踪机制，不要重新发明轮子
4. **架构一致性很重要** - 相似功能应该使用相同的数据源和逻辑
5. **日志是诊断的关键** - 多窗口场景需要详细的日志来验证行为

---

**最后更新：** 2026-03-18  
**修复版本：** Pulsar v1.0.0+  
**相关 Issue：** WinSwitcher 多窗口切换逻辑错误
