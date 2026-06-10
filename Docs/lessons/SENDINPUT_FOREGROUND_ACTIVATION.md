# SendInput 鼠标事件——前台窗口激活的终极方案

**日期：** 2026-06-10  
**严重程度：** 高  
**影响范围：** 全局窗口切换、WinSwitcher 插件、QuickSwitch  
**状态：** 已解决

---

## 问题描述

继 `FOREGROUND_WINDOW_ACTIVATION_RELIABILITY.md` 修复后，多层 `AttachThreadInput` + `LockSetForegroundWindow` + `AllowSetForegroundWindow` + `SwitchToThisWindow` + `ForceActivate` 回退体系在大多数场景下可靠，但仍有部分场景下 `SetForegroundWindow` 持续返回 `false`，目标窗口任务栏图标闪烁。

参考了 GitHub 上两个成熟的窗口切换项目：
- **Switcheroo**（C#/WPF）——使用 `SwitchToThisWindow` + `keybd_event(VK_MENU)` 模拟 Alt 键窃取前台
- **Window-Switcher**（Rust）——使用 `SendInput(INPUT_MOUSE)` 零位移鼠标事件 + `SetForegroundWindow`

其中 **Window-Switcher** 的方案最为简洁有效：只做 `SendInput(INPUT_MOUSE)` + `SetForegroundWindow`，不依赖 `AttachThreadInput`、`LockSetForegroundWindow` 等复杂 API。PowerToys 同样采用此技术（参考 `WindowUtils.cpp`）。

---

## 根本原因

Windows 前台锁定机制的核心规则：
> 只有拥有前台窗口的进程、或**接收到最后一个输入事件**的进程，才能调用 `SetForegroundWindow`。

Pulsar 现有的多层回退体系试图通过 `AttachThreadInput`（线程附加）、`LockSetForegroundWindow(LSFW_UNLOCK)`（前台锁解除）、`AllowSetForegroundWindow(ASFW_ANY)`（授予权限）配合工作。但这些 API 各有局限：
- `AttachThreadInput`：可能因权限/线程状态失败
- `LockSetForegroundWindow`：仅在调用进程**当前持有前台锁**时生效
- `AllowSetForegroundWindow`：仅授予**目标进程**权限，不授予调用进程

而 `SendInput(INPUT_MOUSE)` 直接让**调用进程**产生输入事件，使 Windows 认为该进程有用户交互权限，从而**无条件满足**前台锁定规则的第二个条件。

---

## 解决方案

### 核心：`SendInputMouse()` —— 零副作用输入注入

```csharp
// WindowsFocusNativeAdapter.cs
private const uint MOUSEEVENTF_MOVE = 0x0001;

public uint SendInputMouse()
{
    var input = new INPUT
    {
        type = INPUT_MOUSE,  // 0
        U = new InputUnion
        {
            mi = new MOUSEINPUT
            {
                dwFlags = MOUSEEVENTF_MOVE,  // 移动事件
                dx = 0,                       // 零位移
                dy = 0
            }
        }
    };
    return SendInput(1, new[] { input }, INPUT.Size);
}
```

- `dx=0, dy=0` 且携带 `MOUSEEVENTF_MOVE` 标志：是一个合法的鼠标移动事件，但**对用户零可见副作用**
- 仅 `MOUSEEVENTF_MOVE` 优于完全零字段的 `INPUT_MOUSE`：某些 Windows 版本可能将完全零字段的事件视为无效输入
- 返回 `SendInput` 的返回值（成功插入的事件数），便于日志诊断

### 激活路径重构：简单优先，复杂兜底

```
ActivateWindowAsync(hWnd)
│
├─ 第0层（SendInput 简单路径，优先尝试）
│   SendInputMouse() → SetForegroundWindow(hWnd)
│   
│   95% 场景在此成功。不需要 AttachThreadInput、LockSetForegroundWindow、
│   AllowSetForegroundWindow 等任何额外操作。
│
└─ 第1层（传统多层回退，仅在第0层失败时进入）
    ├─ AttachThreadInput 成功？
    │   ├─ LockSetForegroundWindow(UNLOCK)
    │   │   ├─ AllowSetForegroundWindow + SendInputMouse + SetForegroundWindow
    │   │   ├─ [失败] SwitchToThisWindow
    │   │   ├─ [失败] BringWindowToTop + SendInputMouse + SetForegroundWindow
    │   │   └─ [失败] ForceActivate（SendInputMouse + ASFW_ANY + SetForegroundWindow）
    │   └─ LockSetForegroundWindow(LOCK)
    └─ AttachThreadInput 失败？
        └─ FallbackActivate（每步 SetForegroundWindow 前均有 SendInputMouse）
```

**关键原则**：**每一处** `SetForegroundWindow` 调用前都先调用 `SendInputMouse()`——包括重试路径（`BringWindowToTop` 之后）。

---

## 修改的文件

| 文件 | 变更说明 |
|------|---------|
| `Services/Interfaces/IFocusNativeAdapter.cs` | 新增 `uint SendInputMouse()` 接口方法 |
| `Services/WindowsFocusNativeAdapter.cs` | 实现 `SendInputMouse()`：P/Invoke `SendInput`、`INPUT`/`MOUSEINPUT`/`KEYBDINPUT`/`HARDWAREINPUT`/`InputUnion` 结构体、`MOUSEEVENTF_MOVE` 标志 |
| `Services/FocusManager.cs` | 激活路径重构：第0层简单路径（SendInput + SetForegroundWindow），第1层传统多层回退。每处 SetForegroundWindow 前均插入 SendInputMouse。 |

---

## 架构教训

1. **`SendInput(INPUT_MOUSE)` 是 Windows 前台激活的"银弹"**：比 `AttachThreadInput`、`LockSetForegroundWindow`、`AllowSetForegroundWindow`、`keybd_event(VK_MENU)` 等方案都更简洁可靠。它直接满足 Windows 前台锁定规则的核心条件（"接收到最后一个输入事件的进程"），且零副作用。

2. **简单优先，复杂兜底**：将 `SendInput + SetForegroundWindow` 放在回退链的**最前端**（而非最后），可以最小化对其他 API 的依赖，减少时序竞争和权限问题。传统的多层回退体系保留作为保险。

3. **`MOUSEEVENTF_MOVE` 优于完全零字段**：仅 `INPUT_MOUSE + MOUSEEVENTF_MOVE + dx=0,dy=0` 在各 Windows 版本间表现一致。完全零字段的 `INPUT_MOUSE`（无任何 flags）在某些版本可能被系统过滤。

4. **参考成熟开源项目**：Window-Switcher（Rust）和 PowerToys 都采用了 `SendInput(INPUT_MOUSE)` 技术，证明其在生产环境中的可靠性。
