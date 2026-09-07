# Pulsar Interactive Tutorial System Design

**Status**: Draft  
**Version**: v1.0.0  
**Last Updated**: 2026-03-15  
**Related Documents**: [DIALOG_SYSTEM.md](./DIALOG_SYSTEM.md), [UI_BEST_PRACTICES.md](../guides/UI_BEST_PRACTICES.md)

---

## 4. Tutorial Flow Design

### 4.1 Complete Flow Overview

```
[First Launch]
    ↓
[Welcome Dialog] ─────→ [Skip] ──→ [Mark: HasCompletedTutorial = true]
    ↓ [Start]
    │
    ├─→ Step 1: Welcome & Introduction
    ├─→ Step 2: Open Settings (via Tray Icon)
    ├─→ Step 3: Settings Overview
    ├─→ Step 4: Navigate to Slots Page
    ├─→ Step 5: Add "Launch Notepad" Slot (Switch Mode)
    ├─→ Step 6: Test Switch Mode (Open Notepad)
    ├─→ Step 7: Add Notepad Profile Slot (Command Mode)
    ├─→ Step 8: Test Command Mode (SendKeys Demo)
    └─→ Step 9: Completion & Summary
```

### 4.2 Detailed Step Definitions

---

#### **Step 1: Welcome & Introduction**

**Type**: `Instruction`  
**Target**: `None` (Center Screen)  
**Duration**: ~5 seconds

**UI Layout**:
```
┌─────────────────────────────────────────┐
│  🎉 欢迎使用 Pulsar！                    │
│                                         │
│  Pulsar 是一个基于肌肉记忆的快速启动器   │
│  让我们用 30 秒了解核心功能              │
│                                         │
│  核心特性：                              │
│  • 全局热键触发，无需鼠标                │
│  • 两种模式：切换窗口 & 执行命令         │
│  • 空间定位，盲操作友好                  │
│                                         │
│         [开始教程]    [稍后再说]         │
└─────────────────────────────────────────┘
```

**Actions**:
- [开始教程] → 进入 Step 2
- [稍后再说] → 标记 `HasCompletedTutorial = true`，关闭 Tutorial

**Completion Trigger**: 用户点击 [开始教程]

---

#### **Step 2: Open Settings (via Tray Icon)**

**Type**: `WaitForAction`  
**Target**: `TrayIcon` (System Tray)  
**Duration**: ~10 seconds

**UI Layout**:
```
┌─────────────────────────────────────────┐
│  📌 打开设置界面                         │
│                                         │
│  请左键单击任务栏托盘中的 Pulsar 图标    │
│  （或右键选择"设置"）                    │
│                                         │
│  💡 提示：托盘图标通常在屏幕右下角       │
│                                         │
│         [跳过教程]                       │
└─────────────────────────────────────────┘
          ↓ (Arrow pointing to Tray Icon)
```

**Spotlight**: 高亮托盘图标区域

**Completion Trigger**: `WindowOpened` → `SettingsWindow.IsVisible == true`

**Technical Notes**:
- 监听 `SettingsWindow.Loaded` 事件
- 托盘图标坐标通过 `NotifyIcon.GetBounds()` 获取

---

#### **Step 3: Settings Overview**

**Type**: `Instruction`  
**Target**: `Window` (SettingsWindow)  
**Duration**: ~8 seconds

**UI Layout**:
```
┌─────────────────────────────────────────┐
│  ⚙️ 设置界面导览                         │
│                                         │
│  这里可以配置 Pulsar 的所有功能：        │
│                                         │
│  • 常规 - 热键、主题、启动项             │
│  • 槽位配置 - 为不同应用配置快捷操作     │
│  • 插件 - 管理功能扩展                   │
│                                         │
│  接下来我们将配置一个实际案例            │
│                                         │
│              [下一步]                    │
└─────────────────────────────────────────┘
```

**Spotlight**: 高亮左侧导航栏

**Completion Trigger**: 用户点击 [下一步]

---

#### **Step 4: Navigate to Slots Page**

**Type**: `WaitForNavigation`  
**Target**: `UIElement` (Slots Navigation Item)  
**Duration**: ~10 seconds

**UI Layout**:
```
┌─────────────────────────────────────────┐
│  🎯 进入槽位配置                         │
│                                         │
│  请点击左侧导航栏的"槽位配置"            │
│                                         │
│  槽位配置是 Pulsar 的核心功能：          │
│  • 为不同应用配置专属快捷操作            │
│  • 支持两种模式：切换窗口 & 执行命令     │
│                                         │
│         [跳过教程]                       │
└─────────────────────────────────────────┘
          ↓ (Arrow pointing to "槽位配置")
```

**Spotlight**: 高亮 "槽位配置" 导航项

**Completion Trigger**: `PageNavigated` → `CurrentPage == SettingsSlotsPage`

**Technical Notes**:
- 监听 `SettingsViewModel.CurrentPage` 属性变化

---

#### **Step 5: Add "Launch Notepad" Slot (Switch Mode)**

**Type**: `WaitForAction`  
**Target**: `UIElement` (Add Slot Button)  
**Duration**: ~20 seconds

**UI Layout**:
```
┌─────────────────────────────────────────┐
│  ➕ 添加第一个槽位                       │
│                                         │
│  我们将添加一个"打开记事本"的槽位        │
│                                         │
│  步骤：                                  │
│  1. 点击右上角的 [+ 添加槽位] 按钮       │
│  2. 选择"切换模式"                       │
│  3. 插件类型选择"Window Switcher"        │
│  4. 进程名输入：notepad                  │
│  5. 标签输入：记事本                     │
│  6. 点击保存                             │
│                                         │
│  💡 提示：如果记事本未运行，Pulsar 会    │
│     自动启动它                           │
│                                         │
│         [跳过教程]                       │
└─────────────────────────────────────────┘
          ↓ (Arrow pointing to Add Button)
```

**Spotlight**: 高亮 [+ 添加槽位] 按钮

**Completion Trigger**: `SlotAdded` → 检测到新增 Slot，且 `plugin == "com.pulsar.winswitcher"` 且 `args["processName"] == "notepad"`

**Technical Notes**:
- 监听 `ProfilesConfig.Profiles["*"].SwitchMode` 集合变化
- 需要在 Slot 编辑对话框中添加 Tutorial 提示

---

#### **Step 6: Test Switch Mode (Open Notepad)**

**Type**: `WaitForAction`  
**Target**: `None` (Global)  
**Duration**: ~15 seconds

**UI Layout**:
```
┌─────────────────────────────────────────┐
│  🚀 测试切换模式                         │
│                                         │
│  现在让我们测试刚才配置的槽位！          │
│                                         │
│  步骤：                                  │
│  1. 按下 Ctrl+Shift+Q 触发切换模式       │
│  2. 移动鼠标到"记事本"槽位               │
│  3. 释放鼠标，Pulsar 会打开记事本        │
│                                         │
│  💡 提示：轮盘菜单会在鼠标位置显示       │
│                                         │
│         [跳过教程]                       │
└─────────────────────────────────────────┘
```

**Completion Trigger**: `RadialMenuShown` → `isCommandMode == false` 且检测到 Notepad 进程启动

**Technical Notes**:
- 监听 `RadialMenuViewModel.IsVisible` 和 `IsCommandMode` 属性
- 监听系统进程列表，检测 `notepad.exe` 启动

---

#### **Step 7: Add Notepad Profile Slot (Command Mode)**

**Type**: `WaitForAction`  
**Target**: `UIElement` (Notepad Profile in List)  
**Duration**: ~25 seconds

**UI Layout**:
```
┌─────────────────────────────────────────┐
│  📝 配置记事本专属命令                   │
│                                         │
│  现在记事本已经打开，我们为它配置一个    │
│  快捷命令：插入当前时间                  │
│                                         │
│  步骤：                                  │
│  1. 在左侧进程列表中选择"Notepad"        │
│  2. 切换到"命令模式"标签页               │
│  3. 点击 [+ 添加槽位]                    │
│  4. 插件类型选择"Simple Command"         │
│  5. 动作选择"sendkeys"                   │
│  6. 参数 keys 输入：                     │
│     Current Time: {ENTER}%date% %time%  │
│  7. 标签输入：插入时间                   │
│  8. 点击保存                             │
│                                         │
│         [跳过教程]                       │
└─────────────────────────────────────────┘
          ↓ (Arrow pointing to Notepad Profile)
```

**Spotlight**: 高亮 Notepad Profile 和 Command Mode 标签页

**Completion Trigger**: `SlotAdded` → 检测到 Notepad Profile 的 CommandMode 新增 Slot，且 `plugin == "com.pulsar.command"` 且 `action == "sendkeys"`

**Technical Notes**:
- 监听 `ProfilesConfig.Profiles["notepad"].CommandMode` 集合变化

---

#### **Step 8: Test Command Mode (SendKeys Demo)**

**Type**: `WaitForAction`  
**Target**: `None` (Global)  
**Duration**: ~15 seconds

**UI Layout**:
```
┌─────────────────────────────────────────┐
│  ⚡ 测试命令模式                         │
│                                         │
│  最后一步！让我们测试刚才配置的命令      │
│                                         │
│  步骤：                                  │
│  1. 确保记事本窗口处于激活状态           │
│  2. 按下 Ctrl+Q 触发命令模式             │
│  3. 移动鼠标到"插入时间"槽位             │
│  4. 释放鼠标，查看记事本中的变化         │
│                                         │
│  💡 提示：命令模式会根据当前激活的       │
│     应用显示不同的操作                   │
│                                         │
│         [跳过教程]                       │
└─────────────────────────────────────────┘
```

**Completion Trigger**: `RadialMenuShown` → `isCommandMode == true` 且 `context.TargetProcessName == "NOTEPAD"`

**Technical Notes**:
- 监听 `RadialMenuViewModel.IsVisible` 和 `IsCommandMode` 属性
- 检查 `PulsarContext.TargetProcessName`

---

#### **Step 9: Completion & Summary**

**Type**: `Instruction`  
**Target**: `None` (Center Screen)  
**Duration**: ~10 seconds

**UI Layout**:
```
┌─────────────────────────────────────────┐
│  🎉 恭喜！教程完成                       │
│                                         │
│  你已经掌握了 Pulsar 的核心功能：        │
│                                         │
│  ✅ 切换模式 (Ctrl+Shift+Q)              │
│     快速切换到其他应用                   │
│                                         │
│  ✅ 命令模式 (Ctrl+Q)                    │
│     为当前应用执行快捷操作               │
│                                         │
│  ✅ 槽位配置                             │
│     自定义你的工作流                     │
│                                         │
│  💡 提示：                               │
│  • 在设置中可以随时重新查看教程          │
│  • 支持为每个应用配置专属操作            │
│  • 更多插件请访问插件市场                │
│                                         │
│              [完成]                      │
└─────────────────────────────────────────┘
```

**Actions**:
- [完成] → 标记 `HasCompletedTutorial = true`，关闭 Tutorial

**Completion Trigger**: 用户点击 [完成]

---


### 1.1 Purpose

Pulsar Interactive Tutorial 是一个交互式引导系统，帮助新用户在 30 秒内理解并掌握 Pulsar 的核心功能。

### 1.2 Design Philosophy

参考主流软件（VS Code, Figma, Notion）的最佳实践，采用 **"Coach Marks + Interactive Overlay"** 模式：

- **非侵入式** - 半透明遮罩 + 聚光灯效果，不阻塞用户操作
- **上下文引导** - 箭头指向目标，卡片显示说明和操作提示
- **状态驱动** - 用户完成操作后自动进入下一步
- **可控性** - 用户随时可以跳过、暂停或重启

### 1.3 Key Requirements

1. ✅ 首次启动自动触发 tutorial
2. ✅ 设置中可手动重启
3. ✅ 交互式引导（非文档/视频）
4. ✅ 覆盖两种模式（Command Mode / Switch Mode）
5. ✅ 展示设置界面和轮盘界面
6. ✅ 零依赖示例（使用 Notepad）
7. ✅ 完整展示 Slot 配置流程

---

## 2. Architecture Design

### 2.1 System Architecture

```
ITutorialService (Service Layer)
    ↓
TutorialOrchestrator (State Machine)
    ↓
TutorialOverlayWindow (Transparent Window with Spotlight)
    ↓
TutorialStepCard (Instruction Card with Arrow)
```

### 2.2 Core Components

#### 2.2.1 ITutorialService

```csharp
public interface ITutorialService
{
    bool IsTutorialActive { get; }
    bool HasCompletedTutorial { get; }
    
    Task StartTutorialAsync();
    void PauseTutorial();
    void ResumeTutorial();
    void SkipTutorial();
    void CompleteTutorial();
    Task GoToStepAsync(string stepId);
    
    event EventHandler<TutorialStepChangedEventArgs>? StepChanged;
    event EventHandler? TutorialCompleted;
}
```

#### 2.2.2 TutorialOrchestrator

状态机，负责管理 Tutorial 流程：

- 维护当前步骤状态
- 监听用户操作触发器
- 协调 UI 组件显示/隐藏
- 持久化进度到配置文件

#### 2.2.3 TutorialOverlayWindow

全屏透明窗口，实现聚光灯效果：

- `WindowStyle = None`, `AllowsTransparency = true`
- `Topmost = true` 确保覆盖所有窗口
- 使用 `CombinedGeometry` 实现镂空效果
- 聚光灯区域设置 `IsHitTestVisible = false` 允许点击穿透

#### 2.2.4 TutorialStepCard

指令卡片，显示当前步骤说明：

- 箭头指向目标区域
- 标题 + 描述 + 操作按钮
- 支持 [下一步] / [跳过] / [暂停] 按钮

---

## 3. Data Models

### 3.1 ProfileSettings Extension

```csharp
public partial class ProfileSettings : ObservableObject
{
    // ... existing properties ...
    
    /// <summary>
    /// 是否已完成 Tutorial
    /// </summary>
    public bool HasCompletedTutorial { get; set; } = false;
    
    /// <summary>
    /// 最后完成的 Tutorial 步骤 ID（用于断点续传）
    /// </summary>
    public string? LastTutorialStep { get; set; } = null;
}
```

### 3.2 TutorialStep Model

```csharp
public class TutorialStep
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public TutorialStepType Type { get; set; }
    public TutorialTarget? Target { get; set; }
    public TutorialTrigger? CompletionTrigger { get; set; }
    public List<TutorialAction> Actions { get; set; } = new();
}

public enum TutorialStepType
{
    Instruction,      // 纯说明，点击"下一步"继续
    WaitForAction,    // 等待用户操作（如按下热键、点击按钮）
    WaitForNavigation // 等待导航到特定界面
}

public class TutorialTarget
{
    public TutorialTargetType Type { get; set; }
    public string? ElementName { get; set; }  // 目标元素名称（如 "TrayIcon", "SettingsButton"）
    public Rect? Bounds { get; set; }         // 目标区域坐标（用于聚光灯）
}

public enum TutorialTargetType
{
    None,           // 无目标（居中显示卡片）
    TrayIcon,       // 系统托盘图标
    Window,         // 特定窗口
    UIElement       // UI 元素
}

public class TutorialTrigger
{
    public TutorialTriggerType Type { get; set; }
    public string? TargetValue { get; set; }  // 触发条件的目标值
}

public enum TutorialTriggerType
{
    ButtonClick,        // 点击按钮
    WindowOpened,       // 窗口打开
    PageNavigated,      // 页面导航
    HotkeyPressed,      // 热键按下
    RadialMenuShown,    // 轮盘菜单显示
    SlotAdded           // Slot 添加
}
```

---


## 5. Technical Implementation

### 5.1 TutorialOverlayWindow Implementation

#### 5.1.1 Spotlight Effect with Click-Through

```csharp
public class TutorialOverlayWindow : Window
{
    private Rect _spotlightBounds;
    
    public TutorialOverlayWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        
        // Full screen
        WindowState = WindowState.Maximized;
        
        Loaded += OnLoaded;
    }
    
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Make entire window click-through initially
        SetClickThrough(true);
    }
    
    public void SetSpotlight(Rect bounds)
    {
        _spotlightBounds = bounds;
        InvalidateVisual();
        
        // Update hit test for spotlight region
        UpdateHitTestRegion();
    }
    
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        
        // Full screen semi-transparent overlay
        var fullRect = new Rect(0, 0, ActualWidth, ActualHeight);
        
        // Create spotlight hole
        var geometry = new CombinedGeometry(
            GeometryCombineMode.Exclude,
            new RectangleGeometry(fullRect),
            new RectangleGeometry(_spotlightBounds)
        );
        
        // Draw overlay with hole
        dc.DrawGeometry(
            new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), // Semi-transparent black
            null,
            geometry
        );
    }
    
    private void UpdateHitTestRegion()
    {
        // Make spotlight region click-through
        // Implementation using Win32 SetWindowRgn
    }
}
```

#### 5.1.2 Arrow Positioning

```csharp
public class TutorialStepCard : UserControl
{
    public void PositionRelativeTo(Rect targetBounds, ArrowDirection direction)
    {
        // Calculate card position based on target and arrow direction
        // Ensure card stays within screen bounds
    }
}

public enum ArrowDirection
{
    Top, Bottom, Left, Right
}
```

### 5.2 State Machine Implementation

```csharp
public class TutorialOrchestrator
{
    private readonly List<TutorialStep> _steps;
    private int _currentStepIndex = 0;
    private readonly ITutorialService _tutorialService;
    private readonly IConfigService _configService;
    
    public TutorialStep CurrentStep => _steps[_currentStepIndex];
    
    public async Task StartAsync()
    {
        _currentStepIndex = 0;
        await ShowStepAsync(CurrentStep);
    }
    
    public async Task NextStepAsync()
    {
        if (_currentStepIndex < _steps.Count - 1)
        {
            _currentStepIndex++;
            await ShowStepAsync(CurrentStep);
        }
        else
        {
            await CompleteAsync();
        }
    }
    
    private async Task ShowStepAsync(TutorialStep step)
    {
        // Show overlay and card
        // Setup event listeners based on trigger type
        // Update config with current step
        
        await _configService.UpdateSettingAsync(
            s => s.LastTutorialStep = step.Id
        );
    }
    
    private async Task CompleteAsync()
    {
        await _configService.UpdateSettingAsync(
            s => s.HasCompletedTutorial = true
        );
        
        _tutorialService.CompleteTutorial();
    }
}
```

### 5.3 Trigger Detection

#### 5.3.1 Window Opened Trigger

```csharp
public class WindowOpenedTriggerHandler : ITriggerHandler
{
    public void Setup(TutorialTrigger trigger, Action onTriggered)
    {
        // Subscribe to window events
        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window.GetType().Name == trigger.TargetValue)
                {
                    onTriggered();
                    return;
                }
            }
            
            // Listen for new windows
            EventManager.RegisterClassHandler(
                typeof(Window),
                Window.LoadedEvent,
                new RoutedEventHandler((s, e) =>
                {
                    if (s is Window w && w.GetType().Name == trigger.TargetValue)
                    {
                        onTriggered();
                    }
                })
            );
        });
    }
}
```

#### 5.3.2 Page Navigated Trigger

```csharp
public class PageNavigatedTriggerHandler : ITriggerHandler
{
    private readonly SettingsViewModel _settingsViewModel;
    
    public void Setup(TutorialTrigger trigger, Action onTriggered)
    {
        _settingsViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.CurrentPage))
            {
                var currentPageType = _settingsViewModel.CurrentPage?.GetType().Name;
                if (currentPageType == trigger.TargetValue)
                {
                    onTriggered();
                }
            }
        };
    }
}
```

#### 5.3.3 Slot Added Trigger

```csharp
public class SlotAddedTriggerHandler : ITriggerHandler
{
    private readonly IConfigService _configService;
    
    public void Setup(TutorialTrigger trigger, Action onTriggered)
    {
        _configService.ConfigUpdated += () =>
        {
            // Check if new slot matches criteria
            var config = _configService.Current;
            
            // Parse trigger.TargetValue as JSON criteria
            // Example: {"processName": "notepad", "mode": "command", "plugin": "com.pulsar.command"}
            
            if (SlotMatchesCriteria(config, trigger.TargetValue))
            {
                onTriggered();
            }
        };
    }
}
```

#### 5.3.4 Radial Menu Shown Trigger

```csharp
public class RadialMenuShownTriggerHandler : ITriggerHandler
{
    private readonly RadialMenuViewModel _radialMenuViewModel;
    
    public void Setup(TutorialTrigger trigger, Action onTriggered)
    {
        _radialMenuViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RadialMenuViewModel.IsVisible) 
                && _radialMenuViewModel.IsVisible)
            {
                // Check mode if specified in trigger
                var expectedMode = trigger.TargetValue; // "command" or "switch"
                var actualMode = _radialMenuViewModel.IsCommandMode ? "command" : "switch";
                
                if (string.IsNullOrEmpty(expectedMode) || expectedMode == actualMode)
                {
                    onTriggered();
                }
            }
        };
    }
}
```

---

## 6. Integration Points

### 6.1 App.xaml.cs Integration

```csharp
protected override async void OnStartup(StartupEventArgs e)
{
    // ... existing initialization ...
    
    // Register Tutorial Service
    serviceCollection.AddSingleton<ITutorialService, TutorialService>();
    
    Services = serviceCollection.BuildServiceProvider();
    
    // ... existing code ...
    
    // Check if tutorial should start
    var tutorialService = Services.GetRequiredService<ITutorialService>();
    var configService = Services.GetRequiredService<IConfigService>();
    
    var config = await configService.LoadAsync();
    
    if (!config.Settings.HasCompletedTutorial)
    {
        // Delay to avoid startup lag
        await Task.Delay(1000);
        await tutorialService.StartTutorialAsync();
    }
}
```

### 6.2 SettingsGeneralPage Integration

Add "Restart Tutorial" button:

```xml
<ui:CardControl Header="教程" Icon="{ui:SymbolIcon Lightbulb24}">
    <StackPanel Spacing="8">
        <TextBlock Text="重新查看 Pulsar 功能教程" 
                   Foreground="{DynamicResource TextFillColorSecondaryBrush}"/>
        <ui:Button Content="重新开始教程" 
                   Command="{Binding RestartTutorialCommand}"
                   Style="{StaticResource PulsarSecondaryButtonStyle}"
                   Icon="{ui:SymbolIcon Play24}"/>
    </StackPanel>
</ui:CardControl>
```

ViewModel:

```csharp
[
