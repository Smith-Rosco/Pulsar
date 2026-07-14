# Pulsar Interactive Tutorial System Design (Part 3)

**This is a continuation of TUTORIAL_SYSTEM.md and TUTORIAL_SYSTEM_PART2.md**

---

## 8. Key Challenges & Solutions

### 8.1 Challenge: 进程选择器改进

**问题**: 当前添加 Slot 时，只能选择已打开的进程，如果用户没有打开目标应用，只能手动输入进程名。

**解决方案**: 增强进程选择器，支持三种输入方式

#### 8.1.1 Enhanced Process Picker

```
┌─────────────────────────────────────────────────────┐
│  选择应用程序                                        │
├─────────────────────────────────────────────────────┤
│                                                     │
│  [Tab: 正在运行] [Tab: 常用应用] [Tab: 手动输入]    │
│                                                     │
│  ┌─────────────────────────────────────────────┐   │
│  │ 正在运行的应用                               │   │
│  ├─────────────────────────────────────────────┤   │
│  │ 🌐 Chrome (chrome.exe)                      │   │
│  │ 📝 Notepad (notepad.exe)                    │   │
│  │ 💻 Visual Studio Code (Code.exe)           │   │
│  └─────────────────────────────────────────────┘   │
│                                                     │
│  ┌─────────────────────────────────────────────┐   │
│  │ 常用应用 (预定义列表)                        │   │
│  ├─────────────────────────────────────────────┤   │
│  │ 🌐 Google Chrome                            │   │
│  │ 🦊 Firefox                                  │   │
│  │ 📝 Notepad                                  │   │
│  │ 📊 Excel                                    │   │
│  │ 📄 Word                                     │   │
│  │ 💻 VS Code                                  │   │
│  │ 🎨 Photoshop                                │   │
│  └─────────────────────────────────────────────┘   │
│                                                     │
│  ┌─────────────────────────────────────────────┐   │
│  │ 手动输入                                     │   │
│  ├─────────────────────────────────────────────┤   │
│  │ 进程名: [notepad          ]                 │   │
│  │                                             │   │
│  │ 💡 提示：输入不带 .exe 后缀的进程名          │   │
│  │    例如：chrome, notepad, excel             │   │
│  └─────────────────────────────────────────────┘   │
│                                                     │
│                          [确定]  [取消]             │
└─────────────────────────────────────────────────────┘
```

#### 8.1.2 Common Applications Database

```csharp
public static class CommonApplications
{
    public static readonly List<ApplicationInfo> Database = new()
    {
        new("Google Chrome", "chrome", "\uE774", new[] { "chrome.exe" }),
        new("Microsoft Edge", "msedge", "\uE774", new[] { "msedge.exe" }),
        new("Firefox", "firefox", "\uE774", new[] { "firefox.exe" }),
        new("Notepad", "notepad", "\uE70F", new[] { "notepad.exe" }),
        new("Notepad++", "notepad++", "\uE70F", new[] { "notepad++.exe" }),
        new("Visual Studio Code", "code", "\uE70C", new[] { "code.exe" }),
        new("Visual Studio", "devenv", "\uE70C", new[] { "devenv.exe" }),
        new("Excel", "excel", "\uE71D", new[] { "excel.exe", "et.exe" }),
        new("Word", "winword", "\uE8A5", new[] { "winword.exe", "wps.exe" }),
        new("PowerPoint", "powerpnt", "\uE8A5", new[] { "powerpnt.exe", "wpp.exe" }),
        new("Outlook", "outlook", "\uE715", new[] { "outlook.exe" }),
        new("Photoshop", "photoshop", "\uE91B", new[] { "photoshop.exe" }),
        new("Illustrator", "illustrator", "\uE91B", new[] { "illustrator.exe" }),
        new("WeChat", "wechat", "\uE8BD", new[] { "wechat.exe", "wechatapp.exe" }),
        new("QQ", "qq", "\uE8BD", new[] { "qq.exe" }),
        new("Telegram", "telegram", "\uE8BD", new[] { "telegram.exe" }),
        new("Slack", "slack", "\uE8BD", new[] { "slack.exe" }),
        new("Discord", "discord", "\uE8BD", new[] { "discord.exe" }),
        new("Spotify", "spotify", "\uE8D6", new[] { "spotify.exe" }),
        new("Steam", "steam", "\uE7FC", new[] { "steam.exe" }),
        new("Terminal", "windowsterminal", "\uE756", new[] { "windowsterminal.exe", "wt.exe" }),
        new("PowerShell", "powershell", "\uE756", new[] { "powershell.exe", "pwsh.exe" }),
        new("CMD", "cmd", "\uE756", new[] { "cmd.exe" }),
    };
}

public record ApplicationInfo(
    string DisplayName,
    string ProcessName,
    string Icon,
    string[] ExecutableNames
);
```

#### 8.1.3 Implementation

```csharp
public class EnhancedProcessPickerViewModel : ObservableObject, IDialogViewModel
{
    [ObservableProperty]
    private int _selectedTabIndex = 0; // 0: Running, 1: Common, 2: Manual
    
    [ObservableProperty]
    private ObservableCollection<ProcessInfo> _runningProcesses = new();
    
    [ObservableProperty]
    private ObservableCollection<ApplicationInfo> _commonApplications = new();
    
    [ObservableProperty]
    private string _manualProcessName = string.Empty;
    
    [ObservableProperty]
    private ProcessInfo? _selectedProcess;
    
    public EnhancedProcessPickerViewModel(IWindowService windowService)
    {
        // Load running processes
        LoadRunningProcesses(windowService);
        
        // Load common applications
        _commonApplications = new(CommonApplications.Database);
    }
    
    private void LoadRunningProcesses(IWindowService windowService)
    {
        var processes = windowService.GetAllProcesses()
            .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
            .DistinctBy(p => p.ProcessName.ToLower())
            .OrderBy(p => p.ProcessName);
            
        _runningProcesses = new(processes);
    }
    
    public string GetSelectedProcessName()
    {
        return _selectedTabIndex switch
        {
            0 => _selectedProcess?.ProcessName ?? string.Empty,
            1 => _selectedProcess?.ProcessName ?? string.Empty,
            2 => _manualProcessName.Trim().ToLower().Replace(".exe", ""),
            _ => string.Empty
        };
    }
}
```

**优势**:
- ✅ 支持选择正在运行的进程
- ✅ 提供常用应用预定义列表（覆盖 90% 使用场景）
- ✅ 保留手动输入能力（处理特殊情况）
- ✅ 改善用户体验，减少输入错误

---

### 8.2 Challenge: Tutorial 与现有 UI 的集成

**问题**: Tutorial 需要高亮特定 UI 元素，但 WPF 的 Visual Tree 在运行时难以定位。

**解决方案**: 使用命名元素 + Attached Property

#### 8.2.1 Tutorial Target Marker

```csharp
public static class TutorialMarker
{
    public static readonly DependencyProperty IdProperty =
        DependencyProperty.RegisterAttached(
            "Id",
            typeof(string),
            typeof(TutorialMarker),
            new PropertyMetadata(null, OnIdChanged)
        );
    
    public static void SetId(UIElement element, string value)
        => element.SetValue(IdProperty, value);
    
    public static string GetId(UIElement element)
        => (string)element.GetValue(IdProperty);
    
    private static void OnIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element && e.NewValue is string id)
        {
            TutorialTargetRegistry.Register(id, element);
        }
    }
}

public static class TutorialTargetRegistry
{
    private static readonly Dictionary<string, WeakReference<UIElement>> _targets = new();
    
    public static void Register(string id, UIElement element)
    {
        _targets[id] = new WeakReference<UIElement>(element);
    }
    
    public static UIElement? GetTarget(string id)
    {
        if (_targets.TryGetValue(id, out var weakRef) 
            && weakRef.TryGetTarget(out var element))
        {
            return element;
        }
        return null;
    }
    
    public static Rect? GetTargetBounds(string id)
    {
        var element = GetTarget(id);
        if (element == null) return null;
        
        var point = element.PointToScreen(new Point(0, 0));
        return new Rect(point.X, point.Y, element.RenderSize.Width, element.RenderSize.Height);
    }
}
```

#### 8.2.2 Usage in XAML

```xml
<!-- SettingsSlotsPage.xaml -->
<ui:Button Content="+ 添加槽位"
           local:TutorialMarker.Id="AddSlotButton"
           Command="{Binding AddSlotCommand}"/>

<!-- SettingsWindow.xaml -->
<NavigationViewItem Content="槽位配置"
                    local:TutorialMarker.Id="SlotsNavigationItem"
                    Icon="{ui:SymbolIcon Grid24}"/>
```

#### 8.2.3 Tutorial Step Definition

```csharp
new TutorialStep
{
    Id = "step5_add_slot",
    Title = "添加第一个槽位",
    Type = TutorialStepType.WaitForAction,
    Target = new TutorialTarget
    {
        Type = TutorialTargetType.UIElement,
        ElementName = "AddSlotButton"
    },
    CompletionTrigger = new TutorialTrigger
    {
        Type = TutorialTriggerType.SlotAdded,
        TargetValue = "{\"plugin\":\"com.pulsar.winswitcher\",\"processName\":\"notepad\"}"
    }
}
```

---

### 8.3 Challenge: 托盘图标定位

**问题**: 系统托盘图标位置难以获取，且可能被隐藏在溢出区域。

**解决方案**: 使用 Win32 API 查找托盘图标位置

#### 8.3.1 Tray Icon Locator

```csharp
public static class TrayIconLocator
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, 
        string lpszClass, string lpszWindow);
    
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    
    public static Rect? GetTrayIconBounds(string tooltipText)
    {
        // Find taskbar
        var taskbarHandle = FindWindow("Shell_TrayWnd", null);
        if (taskbarHandle == IntPtr.Zero) return null;
        
        // Find notification area
        var trayHandle = FindWindowEx(taskbarHandle, IntPtr.Zero, "TrayNotifyWnd", null);
        if (trayHandle == IntPtr.Zero) return null;
        
        // Get bounds
        if (GetWindowRect(trayHandle, out var rect))
        {
            return new Rect(rect.Left, rect.Top, 
                rect.Right - rect.Left, rect.Bottom - rect.Top);
        }
        
        return null;
    }
}
```

#### 8.3.2 Fallback Strategy

如果无法精确定位托盘图标：

1. **方案 A**: 高亮整个托盘区域（右下角 200×40 区域）
2. **方案 B**: 在屏幕右下角显示箭头 + 提示卡片
3. **方案 C**: 跳过托盘图标步骤，直接引导用户按热键打开设置

```csharp
public async Task ShowTrayIconStepAsync()
{
    var bounds = TrayIconLocator.GetTrayIconBounds("Pulsar");
    
    if (bounds.HasValue)
    {
        // Precise spotlight
        _overlayWindow.SetSpotlight(bounds.Value);
    }
    else
    {
        // Fallback: Highlight entire tray area
        var screenBounds = SystemParameters.WorkArea;
        var trayArea = new Rect(
            screenBounds.Right - 200,
            screenBounds.Bottom - 40,
            200,
            40
        );
        _overlayWindow.SetSpotlight(trayArea);
    }
}
```

---

### 8.4 Challenge: SendKeys 参数复杂性

**问题**: Step 7 要求用户输入 SendKeys 参数 `Current Time: {ENTER}%date% %time%`，对新手不友好。

**解决方案**: 简化示例 + 提供模板

#### 8.4.1 Simplified Example

修改 Step 7 的示例为更简单的文本：

```
参数 keys 输入：Hello from Pulsar!{ENTER}
```

**优势**:
- ✅ 更直观，用户能立即看到效果
- ✅ 避免 Windows 环境变量差异（%date% 格式因地区而异）
- ✅ 降低学习曲线

#### 8.4.2 Template Library (Future Enhancement)

在 Slot 编辑对话框中添加"模板"按钮：

```
┌─────────────────────────────────────────┐
│  配置 SendKeys 参数                      │
├─────────────────────────────────────────┤
│  keys: [Hello from Pulsar!{ENTER}    ]  │
│                                         │
│  [📋 使用模板 ▼]                         │
│    ├─ 插入当前时间                       │
│    ├─ 插入当前日期                       │
│    ├─ 插入问候语                         │
│    ├─ 插入签名                           │
│    └─ 自定义...                          │
│                                         │
│  💡 特殊键：                             │
│     {ENTER} - 回车                       │
│     {TAB} - Tab 键                       │
│     {BACKSPACE} - 退格                   │
│     ^C - Ctrl+C                          │
│                                         │
│              [确定]  [取消]              │
└─────────────────────────────────────────┘
```

---

### 8.5 Challenge: Tutorial 中断与恢复

**问题**: 用户可能在 Tutorial 中途关闭应用，需要支持断点续传。

**解决方案**: 持久化当前步骤 + 恢复提示

#### 8.5.1 Resume Dialog

```csharp
public async Task CheckResumeAsync()
{
    var config = await _configService.LoadAsync();
    
    if (!config.Settings.HasCompletedTutorial 
        && !string.IsNullOrEmpty(config.Settings.LastTutorialStep))
    {
        var result = await _dialogService.ShowMessageAsync(
            "继续教程",
            $"检测到未完成的教程（上次进度：{GetStepTitle(config.Settings.LastTutorialStep)}）\n\n是否继续？",
            DialogType.Info,
            DialogButtons.YesNo
        );
        
        if (result == DialogResult.Yes)
        {
            await _tutorialService.GoToStepAsync(config.Settings.LastTutorialStep);
        }
        else
        {
            // Reset tutorial state
            await _configService.UpdateSettingAsync(s =>
            {
                s.LastTutorialStep = null;
            });
        }
    }
}
```

---

