# Remote Desktop Fake Fullscreen Feature

## 概述

**功能名称**：Remote Desktop Fake Fullscreen（远程桌面伪全屏）

**问题背景**：
在远程桌面全屏模式下，Windows 会拦截几乎所有全局热键（包括 Win 键），导致 Pulsar 的热键（如 `Ctrl+Q`）无法正常工作，PKI 插件也无法在远程桌面锁屏时使用。

**解决方案**：
通过 Windows 事件钩子（`SetWinEventHook`）监听远程桌面窗口的状态变化，当检测到远程桌面进入真全屏时，自动将其转换为"伪全屏"（无边框窗口化），从而允许 Pulsar 的热键正常工作。

**核心优势**：
- ✅ **事件驱动**：零持续开销，只在窗口状态变化时触发
- ✅ **即时响应**：窗口进入全屏的瞬间即可捕获（<10ms）
- ✅ **用户无感**：视觉上仍然是全屏，但系统认为是窗口化
- ✅ **热键穿透**：Pulsar 的所有热键和插件功能正常工作

---

## 技术原理

### 1. 真全屏 vs 伪全屏

| 特性 | 真全屏（Exclusive Fullscreen） | 伪全屏（Borderless Windowed） |
|------|-------------------------------|------------------------------|
| **窗口样式** | `WS_POPUP` | `WS_OVERLAPPED` |
| **置顶标志** | `WS_EX_TOPMOST` | 无 |
| **窗口尺寸** | 覆盖整个屏幕（包括任务栏） | 覆盖工作区（不包括任务栏） |
| **热键拦截** | 拦截所有热键 | 不拦截热键 |
| **视觉效果** | 完全全屏 | 看起来全屏（任务栏可见） |

### 2. 事件驱动机制

使用 Windows 的 `SetWinEventHook` API 监听窗口事件：

```csharp
SetWinEventHook(
    EVENT_OBJECT_LOCATIONCHANGE,  // 窗口位置/大小变化
    EVENT_OBJECT_LOCATIONCHANGE,
    IntPtr.Zero,
    WinEventProc,                 // 回调函数
    0, 0,
    WINEVENT_OUTOFCONTEXT
);
```

**关键事件**：
- `EVENT_OBJECT_LOCATIONCHANGE (0x800B)`：窗口位置或大小变化时触发

**为什么不用轮询？**
- ❌ 轮询（Timer）：持续占用 CPU，500ms 延迟，不优雅
- ✅ 事件驱动：零开销，即时响应，符合 Windows 编程最佳实践

### 3. 远程桌面窗口检测

**方法 1：窗口类名检测**
```csharp
StringBuilder className = new StringBuilder(256);
GetClassName(hwnd, className, className.Capacity);
if (className.ToString() == "TscShellContainerClass")
    return true;
```

**方法 2：进程名检测**
```csharp
var proc = Process.GetProcessById(processId);
if (proc.ProcessName.Equals("mstsc", StringComparison.OrdinalIgnoreCase))
    return true;
```

### 4. 全屏状态检测

```csharp
bool IsWindowFullscreen(IntPtr hwnd)
{
    RECT rect;
    GetWindowRect(hwnd, out rect);
    
    var screen = Screen.FromHandle(hwnd);
    
    // 窗口尺寸是否覆盖整个屏幕
    return rect.Left == screen.Bounds.Left &&
           rect.Top == screen.Bounds.Top &&
           rect.Right == screen.Bounds.Right &&
           rect.Bottom == screen.Bounds.Bottom;
}
```

### 5. 伪全屏转换

```csharp
void ConvertToFakeFullscreen(IntPtr hwnd)
{
    // 1. 移除 WS_POPUP 样式（真全屏标志）
    long style = GetWindowLong(hwnd, GWL_STYLE);
    style &= ~WS_POPUP;
    style |= WS_OVERLAPPED;
    SetWindowLong(hwnd, GWL_STYLE, style);
    
    // 2. 移除 WS_EX_TOPMOST（置顶标志）
    long exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
    exStyle &= ~WS_EX_TOPMOST;
    SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    
    // 3. 调整窗口尺寸（留出任务栏空间）
    var screen = Screen.FromHandle(hwnd);
    SetWindowPos(hwnd, HWND_NOTOPMOST,
        screen.WorkingArea.Left,
        screen.WorkingArea.Top,
        screen.WorkingArea.Width,
        screen.WorkingArea.Height,
        SWP_FRAMECHANGED | SWP_SHOWWINDOW);
}
```

---

## 架构设计

### 文件结构

```
Pulsar/
├── Native/
│   └── WindowHelper.cs              # 新增 Win32 API 定义
├── Services/
│   ├── Interfaces/
│   │   └── IRemoteDesktopService.cs # 新增接口
│   └── RemoteDesktopService.cs      # 新增服务
├── Models/
│   └── ProfilesConfig.cs            # 新增配置模型
└── Views/
    └── Pages/
        └── GeneralPage.xaml         # 新增 GUI 开关
```

### 核心组件

#### 1. IRemoteDesktopService 接口

```csharp
public interface IRemoteDesktopService : IDisposable
{
    /// <summary>
    /// 启用远程桌面伪全屏功能
    /// </summary>
    void EnableFakeFullscreen();
    
    /// <summary>
    /// 禁用远程桌面伪全屏功能
    /// </summary>
    void DisableFakeFullscreen();
    
    /// <summary>
    /// 检测当前是否在远程桌面会话中
    /// </summary>
    bool IsInRemoteDesktopSession();
}
```

#### 2. RemoteDesktopService 实现

**职责**：
- 注册/注销 Windows 事件钩子
- 检测远程桌面窗口
- 执行伪全屏转换
- 防止重复转换（使用 `HashSet<IntPtr>` 缓存已处理窗口）

**生命周期**：
- 单例服务（`AddSingleton`）
- 在 `App.OnStartup` 中启动
- 在 `App.OnExit` 中释放

#### 3. 配置模型

```csharp
public class RemoteDesktopSettings
{
    /// <summary>
    /// 是否启用远程桌面伪全屏功能
    /// </summary>
    public bool EnableFakeFullscreen { get; set; } = false;
}
```

**配置路径**：`Profiles.json` → `Settings` → `RemoteDesktop`

---

## 实现步骤

### Phase 1：核心功能（2-3 小时）

#### Step 1.1：添加 Native API（30 分钟）

**文件**：`Native/WindowHelper.cs`

**新增 API**：
```csharp
// SetWinEventHook API
[DllImport("user32.dll")]
public static extern IntPtr SetWinEventHook(
    uint eventMin, uint eventMax,
    IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
    uint idProcess, uint idThread, uint dwFlags);

[DllImport("user32.dll")]
public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
    IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

// 事件常量
public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
public const uint WINEVENT_OUTOFCONTEXT = 0x0000;

// 窗口样式常量
public const int GWL_STYLE = -16;
public const int GWL_EXSTYLE = -20;
public const long WS_POPUP = 0x80000000L;
public const long WS_OVERLAPPED = 0x00000000L;
public const long WS_EX_TOPMOST = 0x00000008L;

// GetClassName API
[DllImport("user32.dll", CharSet = CharSet.Auto)]
public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

// GetWindowRect API
[DllImport("user32.dll")]
public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

// SetWindowPos API
[DllImport("user32.dll")]
public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
    int X, int Y, int cx, int cy, uint uFlags);

public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
public const uint SWP_FRAMECHANGED = 0x0020;
public const uint SWP_SHOWWINDOW = 0x0040;

// RECT 结构
[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}
```

#### Step 1.2：实现 RemoteDesktopService（1.5 小时）

**文件**：`Services/RemoteDesktopService.cs`

**核心逻辑**：
1. 注册 `SetWinEventHook` 监听 `EVENT_OBJECT_LOCATIONCHANGE`
2. 在回调函数中检测远程桌面窗口
3. 检测是否全屏
4. 执行伪全屏转换
5. 缓存已处理窗口（防止重复转换）

**关键点**：
- 保持 `WinEventDelegate` 委托引用（防止 GC 回收）
- 使用 `HashSet<IntPtr>` 缓存已处理窗口
- 异常处理和日志记录

#### Step 1.3：注册服务（10 分钟）

**文件**：`App.xaml.cs`

```csharp
// ConfigureServices 方法
services.AddSingleton<IRemoteDesktopService, RemoteDesktopService>();

// OnStartup 方法
var config = await configService.LoadAsync();
var rdpService = serviceProvider.GetRequiredService<IRemoteDesktopService>();

if (config.Settings.RemoteDesktop.EnableFakeFullscreen)
{
    rdpService.EnableFakeFullscreen();
}
```

#### Step 1.4：添加配置模型（20 分钟）

**文件**：`Models/ProfilesConfig.cs`

```csharp
public class SettingsConfig
{
    // ... 现有属性 ...
    
    public RemoteDesktopSettings RemoteDesktop { get; set; } = new();
}

public class RemoteDesktopSettings
{
    public bool EnableFakeFullscreen { get; set; } = false;
}
```

### Phase 2：GUI 界面（1 小时）

#### Step 2.1：添加设置区域

**文件**：`Views/Pages/GeneralPage.xaml`

**位置**：在现有设置项之后添加

```xml
<!-- Remote Desktop Settings -->
<ui:CardExpander Header="远程桌面" Icon="Desktop24" Margin="0,0,0,16">
    <StackPanel Spacing="12">
        <ui:ToggleSwitch 
            Content="启用伪全屏模式"
            IsChecked="{Binding EnableRemoteDesktopFakeFullscreen, Mode=TwoWay}"/>
        
        <TextBlock 
            Text="自动将远程桌面从真全屏转换为无边框窗口化，允许 Pulsar 热键正常工作"
            Foreground="{DynamicResource TextFillColorSecondaryBrush}"
            TextWrapping="Wrap"
            FontSize="12"/>
        
        <Border 
            Background="{DynamicResource CardBackgroundFillColorSecondaryBrush}"
            CornerRadius="4"
            Padding="12">
            <StackPanel Spacing="8">
                <TextBlock 
                    Text="💡 使用场景"
                    FontWeight="SemiBold"
                    FontSize="12"/>
                <TextBlock 
                    Text="• 在远程桌面全屏时使用 Ctrl+Q 唤起 Pulsar 轮盘"
                    FontSize="11"
                    Foreground="{DynamicResource TextFillColorSecondaryBrush}"/>
                <TextBlock 
                    Text="• 在远程桌面锁屏时使用 PKI 插件自动填充密码"
                    FontSize="11"
                    Foreground="{DynamicResource TextFillColorSecondaryBrush}"/>
                <TextBlock 
                    Text="• 快速切换到其他窗口（Alt+Tab）"
                    FontSize="11"
                    Foreground="{DynamicResource TextFillColorSecondaryBrush}"/>
            </StackPanel>
        </Border>
        
        <TextBlock 
            Text="⚠️ 注意：启用后任务栏将保持可见"
            Foreground="{DynamicResource SystemFillColorAttentionBrush}"
            FontSize="11"/>
    </StackPanel>
</ui:CardExpander>
```

#### Step 2.2：添加 ViewModel 属性

**文件**：`ViewModels/Pages/GeneralViewModel.cs`

```csharp
[ObservableProperty]
private bool _enableRemoteDesktopFakeFullscreen;

partial void OnEnableRemoteDesktopFakeFullscreenChanged(bool value)
{
    if (_config != null)
    {
        _config.Settings.RemoteDesktop.EnableFakeFullscreen = value;
        _ = _configService.SaveAsync(_config);
        
        // 动态启用/禁用服务
        var rdpService = App.ServiceProvider.GetRequiredService<IRemoteDesktopService>();
        if (value)
            rdpService.EnableFakeFullscreen();
        else
            rdpService.DisableFakeFullscreen();
    }
}
```

---

## 测试计划

### 测试环境要求

由于开发者没有远程桌面测试环境，需要用户协助测试。

### 测试步骤

#### 1. 基础功能测试

**步骤**：
1. 打开 Pulsar 设置 → General 页面
2. 找到"远程桌面"设置区域
3. 启用"启用伪全屏模式"开关
4. 打开远程桌面连接（`mstsc.exe`）
5. 连接到远程机器
6. 按 `Ctrl+Alt+Enter` 进入全屏模式

**预期结果**：
- ✅ 远程桌面窗口自动转换为伪全屏（任务栏可见）
- ✅ 按 `Ctrl+Q` 能够唤起 Pulsar 轮盘
- ✅ 所有 Pulsar 热键正常工作

#### 2. PKI 插件测试

**步骤**：
1. 在远程桌面中锁定屏幕（`Win+L`）
2. 按 `Ctrl+Q` 唤起 Pulsar 轮盘
3. 选择 PKI 插件的"填充密码"动作

**预期结果**：
- ✅ PKI 插件能够正常填充密码到远程桌面锁屏界面

#### 3. 多显示器测试

**步骤**：
1. 在多显示器环境中测试
2. 远程桌面在不同显示器上进入全屏

**预期结果**：
- ✅ 在任意显示器上都能正确转换为伪全屏

#### 4. 性能测试

**步骤**：
1. 打开任务管理器
2. 观察 Pulsar 的 CPU 占用
3. 多次进入/退出远程桌面全屏

**预期结果**：
- ✅ Pulsar CPU 占用保持在 0-1%（无持续开销）

#### 5. 边界测试

**场景 1：快速切换**
- 快速进入/退出全屏多次
- 预期：不会崩溃，转换正常

**场景 2：多个远程桌面窗口**
- 同时打开多个远程桌面连接
- 预期：每个窗口都能正确转换

**场景 3：禁用功能**
- 在设置中禁用"启用伪全屏模式"
- 预期：远程桌面恢复真全屏行为

---

## 已知限制

### 1. 任务栏可见

**现象**：伪全屏模式下，任务栏保持可见。

**原因**：伪全屏的窗口尺寸是 `screen.WorkingArea`（不包括任务栏）。

**解决方案（可选）**：
- 添加"自动隐藏任务栏"选项
- 使用 `ShowWindow(taskbar, SW_HIDE)` 隐藏任务栏

### 2. 仅支持 Windows 远程桌面

**现象**：仅支持 `mstsc.exe`（Windows 远程桌面客户端）。

**原因**：检测逻辑基于进程名和窗口类名。

**解决方案（未来）**：
- 添加对其他远程桌面工具的支持（VNC、TeamViewer、AnyDesk）

### 3. 远程桌面可能自动恢复全屏

**现象**：某些远程桌面配置可能检测到窗口被修改，自动恢复真全屏。

**原因**：远程桌面客户端的内部逻辑。

**解决方案**：
- 事件钩子会持续监听，自动重新转换

---

## 故障排查

### 问题 1：热键仍然不工作

**可能原因**：
1. 功能未启用
2. 远程桌面窗口未被检测到
3. 转换失败

**排查步骤**：
1. 检查设置中"启用伪全屏模式"是否开启
2. 查看日志文件（`%AppData%\Pulsar\Logs\pulsar-yyyyMMdd.log`）
3. 搜索关键字：`"Remote Desktop"`, `"fake fullscreen"`

**日志示例**：
```
[INFO] Remote Desktop fake fullscreen enabled
[INFO] Detected RDP fullscreen, converting to fake fullscreen
[INFO] Successfully converted RDP to fake fullscreen
```

### 问题 2：Pulsar 崩溃

**可能原因**：
1. Win32 API 调用失败
2. 窗口句柄无效

**排查步骤**：
1. 查看日志中的异常堆栈
2. 检查是否有 `AccessViolationException`

### 问题 3：性能问题

**可能原因**：
1. 事件钩子回调函数执行时间过长
2. 内存泄漏

**排查步骤**：
1. 使用任务管理器监控 CPU 和内存占用
2. 查看日志中是否有大量重复的转换操作

---

## 未来扩展

### 1. 支持其他远程桌面工具

**目标**：支持 VNC、TeamViewer、AnyDesk 等。

**实现**：
- 添加进程名和窗口类名的检测规则
- 使用规则引擎（见下文）

### 2. 规则引擎

**目标**：允许用户自定义哪些窗口需要转换为伪全屏。

**配置示例**：
```json
{
  "WindowManagement": {
    "Rules": [
      {
        "Name": "Remote Desktop",
        "ProcessName": "mstsc",
        "WindowClass": "TscShellContainerClass",
        "Action": "ConvertToFakeFullscreen"
      },
      {
        "Name": "VNC Viewer",
        "ProcessName": "vncviewer",
        "Action": "ConvertToFakeFullscreen"
      }
    ]
  }
}
```

### 3. 游戏窗口化

**目标**：将游戏从真全屏转换为无边框窗口化。

**用户价值**：
- 快速 Alt+Tab 切换
- 多显示器无缝切换
- Pulsar 热键正常工作

### 4. 自动隐藏任务栏

**目标**：在伪全屏模式下自动隐藏任务栏。

**实现**：
```csharp
IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
ShowWindow(taskbar, SW_HIDE);
```

---

## 参考资料

### Windows API 文档

- [SetWinEventHook](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook)
- [GetWindowLong](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowlonga)
- [SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos)
- [Window Styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-styles)

### 相关项目

- [WindowTop](https://github.com/WindowTop/WindowTop-App)：窗口管理工具，提供了类似的窗口操作能力

### 技术文章

- [Understanding Window Styles in Win32](https://devblogs.microsoft.com/oldnewthing/20050505-04/?p=35703)
- [Borderless Windowed Mode in Games](https://www.pcgamingwiki.com/wiki/Glossary:Borderless_fullscreen_windowed)

---

## 总结

**核心价值**：
- 解决了远程桌面全屏时 Pulsar 热键无法工作的问题
- 使用事件驱动机制，零持续开销，优雅高效
- 用户无感知，视觉上仍然是全屏体验

**技术亮点**：
- 使用 `SetWinEventHook` 而非轮询，符合 Windows 编程最佳实践
- 一次性转换，无需持续维护
- 完善的异常处理和日志记录

**未来方向**：
- 支持更多远程桌面工具
- 扩展到游戏窗口化等场景
- 提供规则引擎，允许用户自定义

---

**文档版本**：v1.0  
**创建日期**：2026-03-03  
**作者**：Kiro (AI Assistant)  
**状态**：设计阶段（未实现）
