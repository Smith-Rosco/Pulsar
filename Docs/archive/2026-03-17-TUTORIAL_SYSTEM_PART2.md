# Pulsar Interactive Tutorial System Design (Part 2)

**This is a continuation of TUTORIAL_SYSTEM.md**

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
            new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), 
            null,
            geometry
        );
    }
    
    private void UpdateHitTestRegion()
    {
        // Make spotlight region click-through using Win32 SetWindowRgn
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
            var config = _configService.Current;
            
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
                var expectedMode = trigger.TargetValue;
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
    
    serviceCollection.AddSingleton<ITutorialService, TutorialService>();
    
    Services = serviceCollection.BuildServiceProvider();
    
    // ... existing code ...
    
    var tutorialService = Services.GetRequiredService<ITutorialService>();
    var configService = Services.GetRequiredService<IConfigService>();
    
    var config = await configService.LoadAsync();
    
    if (!config.Settings.HasCompletedTutorial)
    {
        await Task.Delay(1000);
        await tutorialService.StartTutorialAsync();
    }
}
```

### 6.2 SettingsGeneralPage Integration

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
[RelayCommand]
private async Task RestartTutorialAsync()
{
    await _configService.UpdateSettingAsync(s =>
    {
        s.HasCompletedTutorial = false;
        s.LastTutorialStep = null;
    });
    
    await _tutorialService.StartTutorialAsync();
}
```

---

## 7. UI/UX Specifications

### 7.1 Visual Design

#### Overlay
- Background: rgba(0, 0, 0, 0.7)
- Spotlight: Fully transparent with 8px rounded corners
- Transition: 300ms ease-in-out

#### Instruction Card
- Size: 400px × Auto (max 600px)
- Background: #FFFFFF (Light) / #2D2D2D (Dark)
- Border Radius: 12px
- Shadow: 0 8px 32px rgba(0, 0, 0, 0.3)
- Padding: 24px

#### Typography
- Title: 18px, SemiBold
- Description: 14px, Regular
- Step Counter: 12px, Medium

#### Buttons
- Primary: Blue accent
- Secondary: Gray
- Spacing: 12px

#### Arrow
- Size: 24px × 24px
- Drop Shadow: 0 2px 8px rgba(0, 0, 0, 0.2)

### 7.2 Animation
- Card entrance: Fade + Scale (300ms)
- Spotlight transition: Morph (400ms)
- Arrow pulse: Scale 1.0 → 1.1 → 1.0 (2s loop)

---

