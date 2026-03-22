# 窗口切换系统重构指南

**日期**: 2026-03-21  
**版本**: 1.0.0  
**状态**: 待执行  
**范围**: WindowService, WindowActivationMonitor, WindowHelper, PulsarContext, ProcessPageProvider

---

## 动机

窗口切换系统经历多次迭代后，积累了以下问题：

1. **Native API 重复定义** — Win32 方法被定义了 3-6 次，分散在 4 个位置
2. **WindowService 职责膨胀** — 1335 行的类承担了 8 种不同职责
3. **ProcessPageProvider 职责混合** — 数据加载和 UI 绑定混在一起
4. **静默的炸弹** — 重复的 API 可能在不同 Windows 版本上表现不一致

---

## 当前架构

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  调用方                                                                      │
│  ├── RadialMenuViewModel                                                   │
│  ├── ProcessPageProvider                                                   │
│  ├── SlotStrategies                                                        │
│  └── WinSwitcherPlugin                                                     │
└────────────┬──────────────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  Native API 分散 (4 处!)                                                    │
│                                                                             │
│  PulsarContext.cs L200-204          ← 内联 NativeMethods (1个方法)        │
│  Services/WindowService.cs           ← 两处内联 P/Invoke (20+ 方法)         │
│    ├── L180-204 (构造函数区域)                                            │
│    └── L1294-1333 (内联 NativeMethods 类)                                 │
│  Services/WindowActivationMonitor.cs ← 内联 P/Invoke (3个方法)             │
│  Native/WindowHelper.cs               ← 最大的 API 集合 (但与其他重复)      │
│                                                                             │
│  Native/DwmHelper.cs                 ← 独立, 合理                                                 │
│  Helpers/IconExtractor.cs            ← 部分重复                                                    │
│  Native/InputHelper.cs              ← 独立, 合理                                                   │
│  Native/GlobalKeyboardHook.cs       ← 独立, 合理                                                  │
└────────────┬──────────────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  WindowService 职责清单 (1335 行)                                          │
│                                                                             │
│  1. 窗口枚举 & 过滤       ~200 行                                          │
│  2. QuickSwitch 状态管理  ~150 行                                          │
│  3. 焦点恢复状态机       ~60 行                                            │
│  4. 图标提取与缓存       ~50 行                                            │
│  5. 窗口注册表           ~70 行                                            │
│  6. 窗口捕获 (Preview)   ~150 行                                           │
│  7. 黑名单管理           ~20 行                                            │
│  8. Native P/Invoke     ~40 行 (内联类)                                   │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 目标架构

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  调用方                                                                      │
│  ├── RadialMenuViewModel                                                   │
│  ├── ProcessPageProvider                                                   │
│  ├── SlotStrategies                                                        │
│  └── WinSwitcherPlugin                                                     │
└────────────┬──────────────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  Native/PulsarNative.cs    ← 唯一的 Native API 入口                       │
│                                                                             │
│  窗口系统 API:                                                              │
│  ├── EnumWindows, GetWindow, GetForegroundWindow                          │
│  ├── IsWindow, IsWindowVisible                                            │
│  ├── GetWindowTextLength, GetWindowText                                   │
│  ├── GetWindowThreadProcessId                                             │
│  ├── GetWindowLong, GetWindowRect                                         │
│  ├── IsIconic, ShowWindow, BringWindowToTop                               │
│  ├── SetForegroundWindow, AllowSetForegroundWindow                        │
│  ├── LockSetForegroundWindow, SystemParametersInfo                        │
│  ├── keybd_event                                                          │
│  ├── DwmGetWindowAttribute                                                │
│  ├── PrintWindow, DestroyIcon, DeleteObject                               │
│  ├── GetClassName                                                         │
│  ├── SetWinEventHook, UnhookWinEvent                                      │
│  └── WinEventDelegate, RECT, SHFILEINFO, Constants                       │
└────────────┬──────────────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  Services/WindowService.cs  (结构化, ~1100 行)                             │
│                                                                             │
│  #region Window Enumeration                                                │
│  #region QuickSwitch State                                                 │
│  #region Window Registry                                                   │
│  #region Focus Restore                                                     │
│  #region Icon Management                                                   │
│  #region Blacklist Management                                              │
│  #region Window Capture                                                    │
│  #region Launch & Switch                                                   │
└─────────────────────────────────────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  ViewModels/Strategies/ProcessPageProvider.cs  (精简)                     │
│  ViewModels/Strategies/ProcessWindowMatcher.cs  (新, 数据层)              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 执行计划

按优先级分 5 个阶段执行。

---

### Phase 0: 补充 Native API 入口

**目标**: `Pulsar.Native.PulsarNative` 成为唯一的 Native API 入口。

**文件**: `Pulsar/Pulsar/Native/PulsarNative.cs` (新建)

```csharp
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Pulsar.Native
{
    public static class PulsarNative
    {
        // ──────────────────────────────────────────────────────────────
        // Constants
        // ──────────────────────────────────────────────────────────────
        public const int GWL_EXSTYLE = -20;
        public const int GWL_STYLE = -16;
        public const long WS_EX_TOOLWINDOW = 0x00000080L;
        public const long WS_EX_APPWINDOW = 0x00040000L;
        public const long WS_EX_TOPMOST = 0x00000008L;
        public const int SW_RESTORE = 9;
        public const int SW_SHOW = 5;
        public const int DWMWA_CLOAKED = 14;
        public const uint GW_HWNDNEXT = 2;
        public const uint GW_OWNER = 4;
        public const uint GW_CHILD = 5;
        public const uint SPI_GETFOREGROUNDLOCKTIMEOUT = 0x2000;
        public const uint SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001;
        public const uint SPIF_SENDCHANGE = 0x0002;
        public const uint LSFW_LOCK = 1;
        public const uint LSFW_UNLOCK = 2;
        public const byte VK_MENU = 0x12;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        public const uint SHGFI_ICON = 0x100;
        public const uint SHGFI_LARGEICON = 0x0;
        public const uint SHGFI_SMALLICON = 0x1;
        public const uint SHGFI_USEFILEATTRIBUTES = 0x10;

        // ──────────────────────────────────────────────────────────────
        // Delegates
        // ──────────────────────────────────────────────────────────────
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        public delegate void WinEventDelegate(
            IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        // ──────────────────────────────────────────────────────────────
        // Structures
        // ──────────────────────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        // ──────────────────────────────────────────────────────────────
        // EnumWindows
        // ──────────────────────────────────────────────────────────────
        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        // ──────────────────────────────────────────────────────────────
        // Window State & Visibility
        // ──────────────────────────────────────────────────────────────
        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        // ──────────────────────────────────────────────────────────────
        // Window Text
        // ──────────────────────────────────────────────────────────────
        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        // ──────────────────────────────────────────────────────────────
        // Window Style
        // ──────────────────────────────────────────────────────────────
        [DllImport("user32.dll")]
        public static extern long GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        // ──────────────────────────────────────────────────────────────
        // Window Position
        // ──────────────────────────────────────────────────────────────
        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        // ──────────────────────────────────────────────────────────────
        // Foreground Lock Management
        // ──────────────────────────────────────────────────────────────
        [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
        private static extern bool SetForegroundWindowNative(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("user32.dll")]
        private static extern uint LockSetForegroundWindow(uint uLockCode);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref uint pvParam, uint fWinIni);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        // ──────────────────────────────────────────────────────────────
        // Foreground Lock — Public wrapper (with reference counting)
        // ──────────────────────────────────────────────────────────────
        private static readonly object _fgLock = new();
        private static int _fgLockCount = 0;
        private static uint _originalTimeout = 0;
        private static bool _timeoutRead = false;

        public static bool SetForegroundWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;

            bool lockAcquired = false;
            try
            {
                lock (_fgLock)
                {
                    if (_fgLockCount == 0)
                    {
                        SystemParametersInfo(SPI_GETFOREGROUNDLOCKTIMEOUT, 0, ref _originalTimeout, 0);
                        SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, IntPtr.Zero, SPIF_SENDCHANGE);
                    }
                    _fgLockCount++;
                    lockAcquired = true;
                }

                return SetForegroundWindowInternal(hWnd);
            }
            finally
            {
                if (lockAcquired)
                {
                    lock (_fgLock)
                    {
                        _fgLockCount--;
                        if (_fgLockCount == 0)
                        {
                            SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, (IntPtr)_originalTimeout, SPIF_SENDCHANGE);
                        }
                    }
                }
            }
        }

        private static bool SetForegroundWindowInternal(IntPtr hWnd)
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            try { AllowSetForegroundWindow((int)pid); } catch { }
            try { LockSetForegroundWindow(LSFW_UNLOCK); } catch { }
            keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0);

            if (IsIconic(hWnd))
                ShowWindow(hWnd, SW_RESTORE);

            bool result = SetForegroundWindowNative(hWnd);
            if (!result)
            {
                BringWindowToTop(hWnd);
                result = SetForegroundWindowNative(hWnd);
            }

            try { LockSetForegroundWindow(LSFW_LOCK); } catch { }
            return result;
        }

        // ──────────────────────────────────────────────────────────────
        // DWM
        // ──────────────────────────────────────────────────────────────
        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // ──────────────────────────────────────────────────────────────
        // GDI
        // ──────────────────────────────────────────────────────────────
        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        // ──────────────────────────────────────────────────────────────
        // Shell / Icon
        // ──────────────────────────────────────────────────────────────
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        // ──────────────────────────────────────────────────────────────
        // WinEvent Hook
        // ──────────────────────────────────────────────────────────────
        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(
            uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc,
            uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        // ──────────────────────────────────────────────────────────────
        // Memory
        // ──────────────────────────────────────────────────────────────
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMin, IntPtr dwMax);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentProcess();
    }
}
```

**注意**: 焦点锁管理 (`SetForegroundWindow` 的引用计数逻辑) 原来在 WindowHelper 中是 static，现在统一到 `PulsarNative` 中。这是合理的，因为焦点锁是系统级资源。

**验证**: `dotnet build` → 编译成功。

---

### Phase 1: 统一 WindowActivationMonitor

**目标**: `WindowActivationMonitor` 使用 `PulsarNative`，删除所有内联 P/Invoke。

**文件**: `Pulsar/Pulsar/Services/WindowActivationMonitor.cs`

**变更**:

1. 删除内联的 `WinEventDelegate` 声明 (L144-151)
2. 删除内联的 `SetWinEventHook` / `UnhookWinEvent` 声明 (L153-164)
3. 将所有 `SetWinEventHook` 调用改为 `PulsarNative.SetWinEventHook`
4. 将所有 `UnhookWinEvent` 调用改为 `PulsarNative.UnhookWinEvent`
5. 删除 `EVENT_SYSTEM_FOREGROUND` 和 `WINEVENT_OUTOFCONTEXT` 常量

**替换对照**:

| 原代码 | 替换为 |
|--------|--------|
| `SetWinEventHook(...)` | `PulsarNative.SetWinEventHook(...)` |
| `UnhookWinEvent(...)` | `PulsarNative.UnhookWinEvent(...)` |
| `private delegate void WinEventDelegate(...)` | 删除 (使用 `PulsarNative.WinEventDelegate`) |
| `private const uint EVENT_SYSTEM_FOREGROUND = ...` | 删除 (使用 `PulsarNative.EVENT_SYSTEM_FOREGROUND`) |
| `private const uint WINEVENT_OUTOFCONTEXT = ...` | 删除 (使用 `PulsarNative.WINEVENT_OUTOFCONTEXT`) |

**验证**: `dotnet build` → 编译成功。

---

### Phase 2: 统一 WindowService

这是最核心的阶段。分两步走。

#### Phase 2A: 删除内联 P/Invoke (L1294-1333)

**目标**: `WindowService` 中的所有内联 `NativeMethods` 类改用 `PulsarNative`。

**变更范围**: `Services/WindowService.cs` L1294-1333

删除内联 `NativeMethods` 类后，所有对该类的调用替换为 `PulsarNative`：

| 原代码 (L1294-1333) | 替换为 |
|---------------------|--------|
| `NativeMethods.GetWindow(...)` | `PulsarNative.GetWindow(...)` |
| `NativeMethods.EnumWindows(...)` | `PulsarNative.EnumWindows(...)` |
| `NativeMethods.IsWindowVisible(...)` | `PulsarNative.IsWindowVisible(...)` |
| `NativeMethods.IsWindow(...)` | `PulsarNative.IsWindow(...)` |
| `NativeMethods.GetWindowTextLength(...)` | `PulsarNative.GetWindowTextLength(...)` |
| `NativeMethods.GetWindowText(...)` | `PulsarNative.GetWindowText(...)` |
| `NativeMethods.GetWindowThreadProcessId(...)` | `PulsarNative.GetWindowThreadProcessId(...)` |
| `NativeMethods.DestroyIcon(...)` | `PulsarNative.DestroyIcon(...)` |
| `NativeMethods.DeleteObject(...)` | `PulsarNative.DeleteObject(...)` |
| `NativeMethods.DwmGetWindowAttribute(...)` | `PulsarNative.DwmGetWindowAttribute(...)` |
| `NativeMethods.GetWindowLong(...)` | `PulsarNative.GetWindowLong(...)` |
| `NativeMethods.GetWindowRect(...)` | `PulsarNative.GetWindowRect(...)` |
| `NativeMethods.PrintWindow(...)` | `PulsarNative.PrintWindow(...)` |
| `NativeMethods.RECT` | `PulsarNative.RECT` |
| `NativeMethods.GW_HWNDNEXT` | `PulsarNative.GW_HWNDNEXT` |
| `NativeMethods.GW_OWNER` | `PulsarNative.GW_OWNER` |
| `NativeMethods.GW_CHILD` | `PulsarNative.GW_CHILD` |
| `NativeMethods.GWL_EXSTYLE` | `PulsarNative.GWL_EXSTYLE` |
| `NativeMethods.WS_EX_TOOLWINDOW` | `PulsarNative.WS_EX_TOOLWINDOW` |
| `NativeMethods.WS_EX_APPWINDOW` | `PulsarNative.WS_EX_APPWINDOW` |
| `NativeMethods.DWMWA_CLOAKED` | `PulsarNative.DWMWA_CLOAKED` |
| `private delegate bool EnumWindowsProc(...)` | 删除 (使用 `PulsarNative.EnumWindowsProc`) |

#### Phase 2B: 删除构造函数区域的 private extern (L180-204)

**变更范围**: `Services/WindowService.cs` L180-204

| 原代码 | 替换为 |
|--------|--------|
| `GetForegroundWindow_Native()` | `PulsarNative.GetForegroundWindow()` |
| `GetWindowThreadProcessId(...)` (L182-183) | `PulsarNative.GetWindowThreadProcessId(...)` |

**保留**: `SHGetFileInfo` + `SHFILEINFO` + `SHGFI_*` 常量 — 这些是图标提取专用的 API，`PulsarNative` 中没有保留它们，因为它们不在窗口切换核心路径上。将这些移动到 `WindowService` 的局部区域即可。

#### Phase 2C: 删除本地常量定义 (L1271-1277)

```csharp
// 删除这些重复常量 (使用 PulsarNative 中的定义):
private const int GWL_EXSTYLE_CONST = -20;
private const long WS_EX_TOOLWINDOW_CONST = 0x00000080L;
private const long WS_EX_APPWINDOW_CONST = 0x00040000L;
private const int DWMWA_CLOAKED_CONST = 14;
private const uint GW_HWNDNEXT_CONST = 2;
private const uint GW_OWNER_CONST = 4;
private const uint GW_CHILD_CONST = 5;
```

**验证**: `dotnet build` → 编译成功 → **全部功能验证** (见验证清单)。

---

### Phase 3: 统一 PulsarContext

**目标**: 删除 `PulsarContext` 中的内联 `NativeMethods`。

**文件**: `Core/Plugin/PulsarContext.cs` L200-204

**变更**: 删除内联 `NativeMethods` 类，将调用替换为 `PulsarNative.GetWindowThreadProcessId`。

```csharp
// 删除:
// internal static class NativeMethods { ... }

// 替换调用 (L141):
// Before: NativeMethods.GetWindowThreadProcessId(hwnd, out processId);
// After:  PulsarNative.GetWindowThreadProcessId(hwnd, out uint pid);
```

**验证**: `dotnet build` → 编译成功。

---

### Phase 4: WindowService 内部结构化

**目标**: 不改变任何行为，仅用 `#region` 分组现有代码，提升可读性。

**原则**: 不拆分到独立类（因为外部不需要 mock 这些内部模块）。

**文件**: `Services/WindowService.cs`

```csharp
public class WindowService : IWindowService
{
    // ==========================================
    #region Constructor & Fields
    // ==========================================

    // [原有字段: _logger, _windowService, _processRegistryService, _loggerFactory]
    // [原有字段: _previousWindowHandle, _hideMainWindowAction, _currentProcessId]
    // [原有字段: _windowHistory, MaxHistorySize, _historyLock]
    // [原有字段: _activeSwitchPair, _switchPairLock, QuickSwitchTimeoutMs]
    // [原有字段: _focusRestoreMode, _focusRestoreTarget, _focusLock]
    // [原有字段: _dynamicBlacklist, _blacklistLock]
    // [原有字段: _iconCache, _historyLogSampler, _captureLogSampler, _switchDebugSampler]
    // [原有字段: _windowRegistry, _registryLock, _cleanupTimer]
    // [原有字段: _activationMonitor]
    // [原有字段: _systemBlacklist]

    #endregion

    // ==========================================
    #region Constructor
    // ==========================================

    public WindowService(...) { ... }

    #endregion

    // ==========================================
    #region Public API (IWindowService)
    // ==========================================

    public void SetPreviousWindow(IntPtr handle) { ... }
    public void RecordWindowActivation(IntPtr hwnd) { ... }
    public IntPtr GetPreviousWindow() { ... }
    public void RegisterHideAction(Action hideAction) { ... }
    public void HideMainWindow() { ... }
    public WindowInfo GetForegroundWindow() { ... }
    public bool FocusWindow(string processName) { ... }
    public Task<bool> LaunchApplicationAsync(string command, string? arguments) { ... }
    public Task<bool> SwitchToProcessAsync(string processName) { ... }
    public Task<List<ProcessWindowInfo>> GetActiveWindowsAsync() { ... }
    public Task<List<ProcessWindowInfo>> GetProcessWindowsAsync(int targetProcessId) { ... }
    public void UpdateBlacklist(IEnumerable<string> userBlacklist) { ... }
    public void RecordPreviousWindow() { ... }
    public void SwitchToPreviousWindow() { ... }
    public void SetFocusRestoreMode(FocusRestoreMode mode, IntPtr targetWindow = default) { ... }
    public FocusRestoreMode GetFocusRestoreMode() { ... }
    public void RestoreFocus() { ... }
    public Task<ImageSource?> CaptureWindowAsync(IntPtr hWnd) { ... }
    public ProcessWindowInfo? SelectTargetWindow(List<ProcessWindowInfo> windows) { ... }

    #endregion

    // ==========================================
    #region Window Enumeration
    // ==========================================

    private bool IsAltTabWindow(IntPtr hWnd) { ... }
    private string GetWindowTitle(IntPtr hWnd) { ... }
    private IntPtr GetNextWindowInZOrder(IntPtr current) { ... }

    #endregion

    // ==========================================
    #region QuickSwitch State
    // ==========================================

    private IntPtr FindValidHistoryWindow(IntPtr excludeWindow) { ... }

    #endregion

    // ==========================================
    #region Icon Management
    // ==========================================

    private ImageSource? ExtractIcon(string path) { ... }

    #endregion

    // ==========================================
    #region Window Registry
    // ==========================================

    private WindowRegistryEntry RegisterOrUpdateWindow(IntPtr hwnd, string processName) { ... }
    private void CleanupWindowRegistry() { ... }

    #endregion

    // ==========================================
    #region Internal Event Handlers
    // ==========================================

    private void OnGlobalWindowActivated(IntPtr hwnd) { ... }

    #endregion

    // ==========================================
    #region Private Helpers
    // ==========================================

    private void ForceForegroundWindow(IntPtr hWnd) { ... }

    #endregion
}
```

**注意**: Phase 4 应该在 Phase 2 完成后作为独立的 git commit，原因是区域重排会产生大量 diff，单独成 commit 便于 code review。

---

### Phase 5: ProcessPageProvider 拆分

**目标**: 将数据加载逻辑从 UI 绑定逻辑中分离出来。

```
ProcessPageProvider.cs (262行)
        │
        ├── LoadAsync()    ──── 职责: 窗口枚举 + 分组 + 配置匹配
        │                    问题: 直接操作 _allSlots, 返回 void
        │
        └── RefreshVisuals() ──── 职责: UI 绑定
                              问题: 直接操作 ObservableCollection<SlotViewModel>
```

**新建文件**: `ViewModels/Strategies/ProcessWindowMatcher.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels.Strategies
{
    public class MatchedWindowGroup
    {
        public PluginSlot? Config { get; set; }
        public List<ProcessWindowInfo> Windows { get; set; } = new();
        public bool IsRunning => Windows.Count > 0;
        public bool IsConfigured => Config != null;
    }

    public class ProcessWindowMatcher
    {
        private readonly ProfilesConfig _config;

        public ProcessWindowMatcher(ProfilesConfig config)
        {
            _config = config;
        }

        public List<MatchedWindowGroup> BuildSlotList(List<ProcessWindowInfo> windows)
        {
            var groups = windows.GroupBy(w => w.ProcessName).ToList();

            // Load configured slots
            var allConfiguredSlots = new Dictionary<int, PluginSlot>();
            if (_config?.Profiles.TryGetValue("Global", out var globalProfile) == true 
                && globalProfile.SwitchMode != null)
            {
                foreach (var item in globalProfile.SwitchMode)
                {
                    if (item.PluginId == "com.pulsar.winswitcher" && item.Slot >= 1)
                    {
                        allConfiguredSlots[item.Slot] = item;
                    }
                }
            }

            // Build reverse lookup
            var configByAppName = new Dictionary<string, PluginSlot>(StringComparer.OrdinalIgnoreCase);
            foreach (var config in allConfiguredSlots.Values)
            {
                if (config.Args.TryGetValue("app", out var appName))
                    configByAppName[appName] = config;
                else if (!string.IsNullOrEmpty(config.Label))
                    configByAppName[config.Label] = config;
            }

            // Separate matched vs unconfigured
            var matchedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unconfiguredGroups = new List<List<ProcessWindowInfo>>();

            foreach (var g in groups)
            {
                if (configByAppName.ContainsKey(g.Key))
                    matchedProcesses.Add(g.Key);
                else
                    unconfiguredGroups.Add(g.ToList());
            }

            // Build slot list
            var slotsByPosition = new Dictionary<int, MatchedWindowGroup>();
            int maxConfiguredSlot = allConfiguredSlots.Keys.Any() ? allConfiguredSlots.Keys.Max() : 0;
            int totalSlotsNeeded = Math.Max(maxConfiguredSlot, unconfiguredGroups.Count);

            // Place configured items
            foreach (var kvp in allConfiguredSlots)
            {
                int position = kvp.Key;
                var config = kvp.Value;
                string? appName = config.Args.TryGetValue("app", out var app) 
                    ? app 
                    : (!string.IsNullOrEmpty(config.Label) ? config.Label : null);

                List<ProcessWindowInfo>? matchedWindows = null;
                if (appName != null)
                {
                    var matchingGroup = groups.FirstOrDefault(g => 
                        string.Equals(g.Key, appName, StringComparison.OrdinalIgnoreCase));
                    if (matchingGroup != null)
                        matchedWindows = matchingGroup.ToList();
                }

                slotsByPosition[position] = new MatchedWindowGroup
                {
                    Config = config,
                    Windows = matchedWindows ?? new List<ProcessWindowInfo>()
                };
            }

            // Fill gaps with unconfigured
            int unconfiguredIndex = 0;
            int currentPosition = 1;
            while (unconfiguredIndex < unconfiguredGroups.Count || slotsByPosition.ContainsKey(currentPosition))
            {
                if (!slotsByPosition.ContainsKey(currentPosition) && unconfiguredIndex < unconfiguredGroups.Count)
                {
                    slotsByPosition[currentPosition] = new MatchedWindowGroup
                    {
                        Config = null,
                        Windows = unconfiguredGroups[unconfiguredIndex]
                    };
                    unconfiguredIndex++;
                }
                currentPosition++;
            }

            return slotsByPosition.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList();
        }
    }
}
```

**修改后的 ProcessPageProvider**:

```csharp
public class ProcessPageProvider : BasePageProvider
{
    private readonly IWindowService _windowService;
    private readonly ProfilesConfig _config;
    private readonly ProcessWindowMatcher _matcher;
    private List<MatchedWindowGroup> _matchedSlots = new();

    public ProcessPageProvider(IWindowService windowService, ProfilesConfig config, 
        System.IServiceProvider serviceProvider)
        : base(serviceProvider.GetService(typeof(IConfigService)) as IConfigService)
    {
        _windowService = windowService;
        _config = config;
        _matcher = new ProcessWindowMatcher(config);
        _serviceProvider = serviceProvider;
        // ... analytics services ...
    }

    public override async Task LoadAsync()
    {
        var windows = await _windowService.GetActiveWindowsAsync();
        _matchedSlots = _matcher.BuildSlotList(windows);
        _currentPage = 0;
    }

    public override void RefreshVisuals(ObservableCollection<SlotViewModel> slots, SlotViewModel centerSlot)
    {
        // [原有逻辑不变, 但 _allSlots 改为从 _matchedSlots 读取]
        // 注意: 这里需要一个小适配层, 因为 MatchedWindowGroup 和原来的 SlotItem 字段名略有不同
        ClearSlots(slots);
        // ... rest of the method unchanged, but uses _matchedSlots instead of _allSlots ...
    }
}
```

**收益**:
- `ProcessWindowMatcher` 可独立单元测试
- 数据转换逻辑不再耦合 UI 绑定
- `ProcessPageProvider` 职责单一化

**验证**: `dotnet build` → 编译成功 → 功能验证。

---

## 验证清单

每阶段完成后执行:

```bash
dotnet build Pulsar/Pulsar/Pulsar.csproj
```

然后人工测试:

```
□ 径向菜单正常显示 (Action Mode + Switcher Mode)
□ QuickSwitch (Ctrl+Q, 快速释放) 正常工作
□ WinSwitcher 插件激活窗口正常
□ WinSwitcher 插件启动应用正常
□ WinSwitcher 插件智能切换 (activate then launch) 正常
□ 多窗口进程切换 — 选择最近激活的窗口
□ 窗口预览正常显示 (CaptureWindow)
□ 窗口图标正常提取
□ 黑名单过滤正常 (Settings 中配置)
□ 日志中无 Win32 错误
□ Tutorial 窗口定位功能正常 (Phase 2 修改后验证)
□ 图标提取功能正常 (Helper 模块)
```

---

## 执行顺序与风险

| Phase | 风险 | 依赖 | 预计改动量 |
|-------|------|------|-----------|
| Phase 0: 新建 PulsarNative | 低 | 无 | ~350 行 (新建) |
| Phase 1: WindowActivationMonitor | 低 | Phase 0 | ~30 行修改 |
| Phase 2: WindowService | **中** | Phase 0 | ~100 行修改 (替换调用) |
| Phase 3: PulsarContext | 低 | Phase 0 | ~10 行修改 |
| Phase 4: 区域分组 | 极低 | Phase 2 | 纯重构, 无行为变更 |
| Phase 5: ProcessPageProvider | 低 | 无 | ~100 行新建 + ~20 行修改 |

**最高风险点**: Phase 2 中 `WindowService` 的焦点切换逻辑 (`ForceForegroundWindow` -> `PulsarNative.SetForegroundWindow`)。需要确保 `PulsarNative.SetForegroundWindow` 的行为与原 `WindowHelper.SetForegroundWindow` 完全一致。

**回归测试重点**: 
- QuickSwitch 的 ping-pong 行为
- 焦点恢复 (`RestoreFocus`)
- 多窗口进程切换的选择逻辑

---

## 副作用: WindowHelper.cs 的未来

Phase 0-3 完成后，`WindowHelper.cs` 将成为**孤儿文件**。所有窗口相关的 Native 调用都迁移到 `PulsarNative`。

建议处理方式:
1. **删除** `Pulsar/Pulsar/Native/WindowHelper.cs`
2. **保留** `DwmHelper.cs`, `InputHelper.cs`, `GlobalKeyboardHook.cs` (它们各自独立, 无重复)

或者，如果未来有其他模块也依赖 `WindowHelper`，可以让 `PulsarNative` 作为 `WindowHelper` 的别名:

```csharp
// Pulsar.Native.WindowHelper.cs
namespace Pulsar.Native
{
    public static class WindowHelper
    {
        // Forward all calls to PulsarNative
        // [Deprecated: Use PulsarNative instead]
    }
}
```

但这增加了维护负担。建议直接删除。

---

## 变更日志

| 日期 | 版本 | 变更 |
|------|------|------|
| 2026-03-21 | 1.0.0 | 初始文档 |
