# AGENTS.md - Operational Guide for AI Agents

This file provides context, conventions, and commands for AI agents (and human developers) working on the **Pulsar** codebase.

## 1. Project Overview

**Pulsar** is a high-performance productivity launcher for Windows featuring a radial menu interface.
- **Framework**: .NET 8.0 (Windows Desktop - WPF & WinForms)
- **Architecture**: Modular Monolith using MVVM (CommunityToolkit.Mvvm) and Dependency Injection.
- **Key Features**: Radial Menu (Window Switcher), Global Hotkeys, PKI/Secret Management, Extensible Plugin System.

### Architecture Highlights

**Plugin System (v4.0.0+)**:
- **Interface**: `IPulsarPlugin` - All plugins implement this standard interface
- **Context Passing**: `PulsarContext` - Lazy-loaded, immutable context snapshot captured when radial menu is invoked
- **Plugin Registry**: `PluginRegistry` - Manages plugin lifecycle and execution with **Circuit Breaker** protection
- **Plugin Loader**: `PluginLoader` - Loads plugins from both built-in assembly and external DLLs
- **Configuration**: `Profiles.json` - Single source of truth for all configuration (replaces legacy `appsettings.json`)

**Plugin Tier Architecture**:
- **Core Plugins** (`Plugins/Core/`): Essential infrastructure plugins that are always loaded, cannot be disabled, and have fail-fast behavior. Examples: PKI (credential management), Hotkey management.
- **Extension Plugins** (`Plugins/`): Optional feature plugins that can be dynamically loaded/unloaded and are protected by Circuit Breaker. Examples: WinSwitcher, VbaRunner.

**Key Distinction**:
- Core plugins: Loaded at startup, critical for basic functionality, no Circuit Breaker (crashes are fatal)
- Extension plugins: Optional features, isolated failures, automatic recovery via Circuit Breaker

**Core Principles**:
- **Focus Management**: All window context is captured at invocation time via `PulsarContext.Capture()`
- **Plugin Isolation**: Plugin exceptions do not crash the main application
- **Dependency Injection**: Plugins receive `IServiceProvider` during initialization
- **Async First**: All plugin actions are async (`Task<PluginResult>`)
- **Safety First**: Plugins that crash repeatedly (3 times) are automatically disabled for 60 seconds (Circuit Breaker).

## 2. Build & Test Commands

### Build
The solution contains a single project. Run commands from the repository root.

- **Restore dependencies**:
  ```bash
  dotnet restore Pulsar/Pulsar/Pulsar.csproj
  ```

- **Build (Debug)**:
  ```bash
  dotnet build Pulsar/Pulsar/Pulsar.csproj
  ```

- **Build (Release)**:
  ```bash
  dotnet build Pulsar/Pulsar/Pulsar.csproj -c Release
  ```

- **Run Application**:
  ```bash
  dotnet run --project Pulsar/Pulsar/Pulsar.csproj
  ```

### Tests
*Current Status: No test projects are currently configured.*

If adding tests in the future:
1. Create a new xUnit project: `dotnet new xunit -o Pulsar/Pulsar.Tests`
2. Add reference: `dotnet add Pulsar/Pulsar.Tests reference Pulsar/Pulsar/Pulsar.csproj`
3. Run tests: `dotnet test Pulsar/Pulsar.Tests`

## 3. Code Style & Conventions

Adhere strictly to these conventions to maintain codebase consistency.

### General
- **Language**: C# 12 / .NET 8.0
- **Nullable Reference Types**: Enabled (`<Nullable>enable</Nullable>`). Handle null warnings explicitly.
- **Formatting**:
  - Use Allman style braces (braces on new lines).
  - Indentation: 4 spaces (no tabs).
  - File Encoding: UTF-8.

### Naming Conventions
- **Classes/Structs/Enums**: `PascalCase` (e.g., `WindowService`, `AppConfig`).
- **Interfaces**: `I` prefix + `PascalCase` (e.g., `IWindowService`).
- **Methods**: `PascalCase` (e.g., `GetActiveWindowsAsync`).
- **Async Methods**: Suffix with `Async` (e.g., `LoadSettingsAsync`).
- **Properties**: `PascalCase` (e.g., `IsVisible`).
- **Private Fields**: `_camelCase` (e.g., `_previousWindowHandle`).
- **Parameters/Locals**: `camelCase` (e.g., `processName`).
- **Event Handlers**: `On[EventName]` (e.g., `OnSettingsClicked`).

### Project Structure
- `Core/`: Interfaces, Base types, Abstract handlers.
  - `Core/Plugin/`: Plugin system core interfaces and types
    - `IPulsarPlugin.cs`: Plugin interface definition
    - `PulsarContext.cs`: Immutable context snapshot with Lazy loading
    - `PluginResult.cs`: Plugin execution result types
    - `PluginLoader.cs`: Plugin loading infrastructure
- `Plugins/`: Built-in plugin implementations
  - `Core/`: **Core plugins** - Infrastructure plugins that are essential and always loaded
    - `Pki/`: PKI credentials management plugin (secrets, auto-fill)
  - `WinSwitcher/`: Window switching and application launching plugin
  - `BasicCommand/`: Simple command execution plugin
- `Helpers/`: Static utilities and extensions.
- `Models/`: Data transfer objects and configuration models.
  - `ProfilesConfig.cs`: New unified configuration model (v4.0.0+)
- `Services/`: Business logic implementations (singleton/transient services).
  - `PluginRegistry.cs`: Plugin registry and execution service with Circuit Breaker
- `ViewModels/`: MVVM ViewModels (inherit from `ObservableObject` or `ViewModelBase`).
- `Views/`: XAML Windows, Controls, and UserControls.

### Coding Patterns
- **Dependency Injection**: Use constructor injection. Register services in `App.xaml.cs`.
- **MVVM**: Use `CommunityToolkit.Mvvm`.
  - Use `[ObservableProperty]` for properties.
  - Use `[RelayCommand]` for commands.
- **Async/Await**: Use `async Task` for I/O bound operations. Avoid `async void` except for event handlers.
- **Native Interop**:
  - Place P/Invoke signatures in `NativeMethods` internal classes or private static externs.
  - Use `LibraryImport` or `DllImport` with appropriate attributes (`SetLastError`, `CharSet`).
- **Error Handling**:
  - Use `try/catch` blocks around volatile operations (File I/O, Native API calls).
  - Use `ILogger<T>` for logging errors. Avoid `Debug.WriteLine`.

## 4. Workflows

### Modifying UI (XAML)
1. Check `Views/` for the relevant `.xaml` file.
2. Ensure data binding matches properties in the corresponding `ViewModel`.
3. Use StaticResources for colors/brushes from `Themes/Theme.*.xaml`.
4. **Theme Isolation & WPF-UI**: The project uses a "Multi-Headed" UI architecture. `App.xaml` does NOT contain global styles.
   - **Radial Menu**: Uses lightweight custom themes (`Themes/Theme.Dark.xaml`). Background is strictly transparent.
   - **Settings/Dialogs**: Uses `Wpf.Ui` themes with Mica backdrop.
   - **Action**: When creating a new window, you MUST inject the theme manually via `IThemeService.ApplyTheme()` in the constructor.
   - **Pages/Frames**: Controls inside a `Page` hosted in a `Frame` do not inherit Window resources correctly. You MUST explicitly call `IThemeService.ApplyTheme()` on the `Page` instance itself.
   - **Critical Pitfall (Pages + XAML Resources)**: If a `Page` defines `<Page.Resources>`, WPF will create/assign the `Resources` dictionary during XAML load. If you call `ApplyTheme(page, ...)` *before* `InitializeComponent()`, the XAML load can replace the `Resources` dictionary instance, discarding injected dictionaries (e.g., `ThemesDictionary`, `ControlsDictionary`, and Pulsar `Themes/Theme.*.xaml`).
     - **Symptom**: Theme DynamicResources become missing (e.g. `Theme.Orb.*`), resulting in blank/unstyled visuals (e.g. `JellyOrb`'s `OrbFill` appears empty) and generally "ugly" fallback UI.
     - **Rule**: Prefer calling `ApplyTheme()` *after* `InitializeComponent()` for `Page`s, and/or re-apply theme once after load.
     - **Rule**: `SettingsWindow` should explicitly apply theme to each cached page (`General`, `Slots`, `Plugins`) to keep behavior consistent.
   - **Context Menus**: Do not inherit Window resources. Manually inject `ui:ControlsDictionary` into `ContextMenu.Resources`.
   - **Animations**: When switching themes, avoid clearing resources (`MergedDictionaries.Remove`) if animations are running (common in `Wpf.Ui`). Instead, update the existing `ThemesDictionary.Theme` property in place.

4.1 **Resources Hygiene (XAMLParseException Prevention)**
   - **Rule**: Each element can set `Resources` only once. Do not mix a top-level `<ResourceDictionary>...</ResourceDictionary>` followed by additional resources in the same `<Page.Resources>` block.
   - **Correct Pattern (Page)**:
     ```xml
     <Page.Resources>
         <ResourceDictionary>
             <ResourceDictionary.MergedDictionaries>
                 <ResourceDictionary Source="pack://application:,,,/Pulsar;component/Styles/ButtonStyles.xaml"/>
             </ResourceDictionary.MergedDictionaries>

             <!-- All converters/styles/templates go here (same dictionary) -->
             <BooleanToVisibilityConverter x:Key="BoolToVis"/>
             <DataTemplate x:Key="MyTemplate">...</DataTemplate>
         </ResourceDictionary>
     </Page.Resources>
     ```

5. **Button Styling - CRITICAL**: Do NOT use `Appearance="Primary"` on WPF-UI buttons!
   
   **Root Cause**: Wpf.Ui's `Appearance="Primary"` relies on dynamic resource inheritance that breaks when themes are injected at window-level (Multi-Headed UI architecture). This causes the foreground color to fallback to unexpected values (white/transparent) on hover, making text invisible.
   
   **Solution**: Always use explicit Pulsar button styles from `Styles/ButtonStyles.xaml`:
   
   ```xml
   <Window.Resources>
       <ResourceDictionary>
           <ResourceDictionary.MergedDictionaries>
               <ResourceDictionary Source="pack://application:,,,/Pulsar;component/Styles/ButtonStyles.xaml"/>
           </ResourceDictionary.MergedDictionaries>
       </ResourceDictionary>
   </Window.Resources>
   
   <!-- Primary action -->
   <ui:Button Content="Save" Style="{StaticResource PulsarPrimaryButtonStyle}"/>
   
   <!-- Secondary action -->
   <ui:Button Content="Cancel" Style="{StaticResource PulsarSecondaryButtonStyle}"/>
   
   <!-- Danger action -->
   <ui:Button Content="Delete" Style="{StaticResource PulsarDangerButtonStyle}"/>
   ```
   
   **Why This Works**: The Pulsar styles use explicit `ControlTemplate` with hardcoded `Trigger` definitions for each state (Normal/Hover/Pressed/Disabled), ensuring 100% predictable colors regardless of dynamic theme injection timing.

6. **Hidden Scrollbars**: If standard `ScrollViewer.VerticalScrollBarVisibility="Hidden"` fails to work (common in complex controls like `NavigationView` or `ListView`), use the **Code-Behind Visual Tree Helper** approach.
   
   **Pattern**:
   ```csharp
   // In Window/Control Code-Behind (e.g. SettingsWindow.xaml.cs)
   
   this.Loaded += (s, e) =>
   {
       // Force hide scrollbars after the control is loaded
       DisableScrollViewers(MyTargetControl);
   };
   
   private void DisableScrollViewers(DependencyObject depObj)
   {
       if (depObj == null) return;
   
       for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
       {
           var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
           if (child is ScrollViewer scrollViewer)
           {
               scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
               scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
           }
           DisableScrollViewers(child); // Recursive
       }
   }
   ```
   **Why**: Internal control templates often override implicit styles or set properties locally. Direct manipulation of the visual tree at runtime is the only 100% reliable way to force visibility.

7. **UserControl Data Context Passing - CRITICAL**: UserControls break the visual tree for `RelativeSource` bindings, causing command bindings to fail.

   **Problem**: When a UserControl exposes a property (e.g., `PageDataContext`) that needs to be passed to its content, standard XAML bindings like `RelativeSource AncestorType` or `ElementName` **will fail** because:
   - ContentPresenter displays content directly without wrapping it
   - The content is set before being added to the visual tree
   - UserControl creates a visual tree boundary that blocks ancestor lookups

   **Symptom**: Buttons inside UserControl content have `Command = NULL` even though the command exists in the parent ViewModel.

   **Solution**: Use **Code-Behind Loaded Event** to manually set the Tag property:

   **Step 1: Add Loaded event to the content root element**
   ```xml
   <controls:ExpandableCard PageDataContext="{Binding DataContext, RelativeSource={RelativeSource AncestorType=Page}}">
       <controls:ExpandableCard.ExpandedContent>
           <StackPanel Loaded="StackPanel_Loaded">
               <!-- Buttons can now bind to Tag.CommandName -->
               <ui:Button Command="{Binding Tag.PickIconCommand, RelativeSource={RelativeSource AncestorType=StackPanel}}"
                          CommandParameter="{Binding}"/>
           </StackPanel>
       </controls:ExpandableCard.ExpandedContent>
   </controls:ExpandableCard>
   ```

   **Step 2: Implement Loaded handler in Code-Behind**
   ```csharp
   /// <summary>
   /// Workaround for UserControl breaking visual tree binding.
   /// Manually sets StackPanel.Tag to ExpandableCard.PageDataContext when loaded.
   /// </summary>
   private void StackPanel_Loaded(object sender, RoutedEventArgs e)
   {
       if (sender is StackPanel stackPanel)
       {
           // Find the UserControl ancestor
           var expandableCard = FindVisualParent<ExpandableCard>(stackPanel);
           if (expandableCard != null && expandableCard.PageDataContext != null)
           {
               // Set the Tag so child buttons can bind to commands
               stackPanel.Tag = expandableCard.PageDataContext;
           }
       }
   }

   // Helper method (should already exist in Page code-behind)
   private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
   {
       var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
       if (parent == null) return null;
       if (parent is T typedParent) return typedParent;
       return FindVisualParent<T>(parent);
   }
   ```

   **Why This Works**:
   - At `Loaded` time, the element is in the visual tree, so `FindVisualParent` succeeds
   - We manually bridge the gap that XAML bindings cannot cross
   - Child elements can use standard `RelativeSource AncestorType` to find the StackPanel

   **Alternative (Not Recommended)**: Wrapping content in a Grid with Tag binding does NOT work because ContentPresenter bypasses the wrapper.

   **When to Use**: Any time you have a UserControl that needs to pass a ViewModel/DataContext to dynamically loaded content (e.g., ExpandableCard, custom dialogs, templated controls).

### Adding a New Service
1. Define the interface in `Services/Interfaces/`.
2. Implement the class in `Services/`.
3. Register the service in `App.xaml.cs` (`ConfigureServices` method).

### Adding a New Plugin

**Step 1: Choose Plugin Tier**
Decide whether your plugin is a **Core** or **Extension** plugin:

| Aspect | Core Plugin | Extension Plugin |
|--------|------------|------------------|
| Location | `Plugins/Core/[Name]/` | `Plugins/[Name]/` |
| Critical | Yes - app fails without it | No - app works without it |
| Circuit Breaker | No - crashes are fatal | Yes - isolated failures |
| Lifecycle | Always loaded, never disabled | Can be disabled/recovered |
| Examples | PKI, Hotkey management | WinSwitcher, VbaRunner |

**Step 2: Create Plugin Structure**
1. Create a new class implementing `IPulsarPlugin` in the appropriate location.
2. Implement required properties: `Id` (unique identifier), `DisplayName`.
3. Implement `Initialize(IServiceProvider)` for dependency injection.
4. Implement `ExecuteAsync(action, args, context)` for action handling.

**Step 3: Follow Best Practices**
- Use `PulsarContext` to access window information - NEVER query window state inside plugins.
- **Optimization**: `PulsarContext` is lazy-loaded. Access heavy properties (like `GetClipboardTextAsync`) only when necessary.
- See `PLUGIN_DEVELOPMENT.md` for detailed plugin development guide.

### Adding a New Dialog

**Pulsar uses a unified dialog architecture** (v4.1.0+) where all dialogs are managed through `DialogService` and displayed in `DialogHostWindow`.

**Architecture Overview**:
```
DialogService (IDialogService)
    ↓
DialogHostWindow (FluentWindow container)
    ↓
ContentPresenter (dynamic ViewModel loading)
    ↓
Your Content (UserControl)
```

**Step 1: Create ViewModel**
Create a ViewModel implementing `IDialogViewModel` in `ViewModels/Dialogs/`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.ViewModels.Base;

public partial class MyDialogViewModel : ObservableObject, IDialogViewModel
{
    [ObservableProperty]
    private string _myProperty = string.Empty;

    public Action<Pulsar.Models.Enums.DialogResult>? RequestClose { get; set; }
    public bool IsScrollable => false;

    public Task<bool> CanCloseAsync(Pulsar.Models.Enums.DialogResult result)
    {
        return Task.FromResult(true);
    }
}
```

**Step 2: Create Content XAML**
Create a UserControl in `Views/Dialogs/Contents/`:

```xml
<UserControl x:Class="Pulsar.Views.Dialogs.Contents.MyDialogContent"
             xmlns:vm="clr-namespace:Pulsar.ViewModels.Dialogs"
             d:DataContext="{d:DesignInstance Type=vm:MyDialogViewModel}">
    <StackPanel Margin="20">
        <TextBlock Text="{Binding MyProperty}"/>
    </StackPanel>
</UserControl>
```

**Step 3: Add DialogService Method** (Optional)
For frequently used dialogs, add a convenience method to `IDialogService`:

```csharp
Task<string?> ShowMyDialogAsync(string title, string initialValue = "");
```

**Built-in Dialogs**:
- `ShowInputAsync()` - Text input dialog (Small size)
- `ShowColorPickerAsync()` - Color picker with RGB sliders and presets (Medium size)
- `ShowMessageAsync()` - Simple message box (Small size)
- `ShowConfirmationAsync()` - Yes/No confirmation (Small size)
- `ShowCustomAsync<T>()` - Generic dialog for any `IDialogViewModel` (configurable size)

**Dialog Features** (Automatic):
- ✅ Theme inheritance (from SettingsWindow or global theme)
- ✅ Smart Owner detection (Active Window > SettingsWindow > MainWindow)
- ✅ Intelligent placement (CenterOwner/CenterScreen/NearMouse/CenterActiveWindow)
- ✅ Size presets (Small/Medium/Large/Auto/Default)
- ✅ Mica backdrop effect
- ✅ Keyboard navigation (Enter/Esc)

**Size Presets**:
```csharp
DialogSizeConstraints.Small   // 400x300 (input dialogs)
DialogSizeConstraints.Medium  // 600x450 (color picker, forms)
DialogSizeConstraints.Large   // 800x600 (complex content)
DialogSizeConstraints.Auto    // Size to content with min/max constraints
```

**Placement Strategies**:
```csharp
DialogPlacement.CenterOwner        // Relative to parent window (default)
DialogPlacement.CenterScreen       // Screen center
DialogPlacement.NearMouse          // Near cursor with boundary checks
DialogPlacement.CenterActiveWindow // Relative to active window
```

**Example Usage**:
```csharp
// Simple input
var name = await _dialogService.ShowInputAsync("Enter Name", "Please enter your name", "Default");

// Color picker
var color = await _dialogService.ShowColorPickerAsync("Pick Color", "#FF5733");

// Custom dialog
var myVm = new MyDialogViewModel();
var result = await _dialogService.ShowCustomAsync("My Dialog", myVm, DialogButtons.OkCancel);
```

**Migration Notes**:
- ❌ **Deprecated**: Creating standalone `Window` classes for dialogs
- ❌ **Deprecated**: Manual theme application with `IThemeService.ApplyTheme()`
- ❌ **Deprecated**: Manual Owner setting
- ✅ **Recommended**: Use `DialogService` for all dialogs

### Managing Secrets (PKI)
- Secrets are handled by `PkiPlugin` (`Plugins/Core/Pki/`) and `CredentialsManager`.
- Ensure sensitive data models use `[JsonIgnore]` to prevent accidental serialization to config files.

### Input Simulation & Text Injection
When injecting text into external applications (e.g., browsers, terminals), prioritize stability and performance.

**Hierarchy of Injection Methods:**
1.  **UI Automation (UIA) - Preferred**:
    - **Mechanism**: Uses Windows Automation API (`IUIAutomationValuePattern.SetValue`).
    - **Pros**: Instantaneous, invisible, does not touch clipboard, thread-safe (if marshaled correctly).
    - **Cons**: Requires target element to support `ValuePattern` (Modern Browsers do).
    - **Code**: See `Pulsar.Native.UiaHelper`.

2.  **Clipboard Paste (Ctrl+V) - Fallback**:
    - **Mechanism**: Set Clipboard text -> Send `Ctrl+V`.
    - **Pros**: Fast, universally supported.
    - **Cons**: overwrites user's clipboard (requires save/restore), prone to locking errors (`ExternalException`), requires STA thread affinity.

3.  **Simulated Typing (SendInput) - Last Resort**:
    - **Mechanism**: Sends array of `KEYBDINPUT` structures.
    - **Pros**: Works everywhere, no clipboard issues.
    - **Cons**: Slow (limited by target app's UI thread speed), visible "typing" animation.

**Best Practice**: Always attempt UIA first. If it fails (element not found or pattern not supported), fall back to Clipboard or Typing depending on the context (Typing is safer for small text, Clipboard for large blocks).

## 5. Agent Behavior Rules

- **Proactiveness**: If a missing null check is spotted, fix it.
- **Context**: Always read the file before editing to preserve local style conventions.
- **Safety**: Do not commit secrets or API keys. 
- **Validation**: After making changes, run `dotnet build` to ensure no compilation errors were introduced.

## 6. Error Handling & Logging (Pulsar Sentinel)

The **Pulsar Sentinel** architecture provides centralized logging and resilience feedback.

### Infrastructure
- **Framework**: **Serilog** (Structured Logging).
- **Log Path**: `%AppData%\Pulsar\Logs\pulsar-yyyyMMdd.log`.
- **DI Integration**: Logger is registered in `App.xaml.cs` via `.AddSerilog()`.

### Global Safety Net
`App.xaml.cs` implements a three-tier exception interceptor:
1.  **DispatcherUnhandledException**: Catches UI thread crashes. Logs Fatal and attempts to keep app alive.
2.  **UnobservedTaskException**: Catches background Task crashes. Logs Error and prevents process termination.
3.  **AppDomain.UnhandledException**: Last resort for catastrophic failures. Logs Fatal before crash.

### Usage Pattern
Use `ILogger<T>` constructor injection instead of `Debug.WriteLine`:

```csharp
public class MyService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
        _logger.LogInformation("Service initialized");
    }

    public void DoWork()
    {
        try 
        {
            // ... work ...
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to do work");
        }
    }
}
```

### Circuit Breaker Feedback
- **Mechanism**: If a plugin crashes 3 times within 1 minute, it is auto-disabled.
- **Feedback**: `PluginRegistry` invokes `ITrayService.ShowNotification` to alert the user via a Windows Toast/Balloon tip.
- **Recovery**: Plugin enters "Half-Open" state after 60 seconds, allowing a single retry.

---

## 7. UI Component Reusability & Best Practices

### Component-Based Architecture

To avoid "reinventing the wheel" and maintain consistency across Settings pages, Pulsar uses **reusable UserControl components**.

#### **Problem: Code Duplication**
Before componentization, similar card layouts were duplicated across multiple pages:
- Plugins page: ~220 lines of XAML for card layout
- Slots page: ~200 lines of similar XAML
- **Total duplication**: ~70% of card structure was identical

**Maintenance cost**: Any style change (e.g., corner radius, padding) required updating multiple files.

#### **Solution: ExpandableCard UserControl**

**Location**: `Views/Controls/ExpandableCard.xaml`

A parameterized, reusable card component that encapsulates common UI patterns:

```xml
<controls:ExpandableCard 
    IconKey="{Binding Icon}"
    Title="{Binding Name}"
    IsToggleEnabled="{Binding IsEnabled, Mode=TwoWay}"
    CanToggle="{Binding CanDisable}"
    PrimaryActionCommand="{Binding ConfigureCommand}"
    PrimaryActionIcon="Settings24"
    PrimaryActionVisibility="{Binding HasSettings, Converter={StaticResource BoolToVis}}"
    CardContextMenu="{StaticResource PluginContextMenu}">
    
    <!-- Custom header content (badges, progress bars) -->
    <controls:ExpandableCard.HeaderContent>
        <StackPanel Orientation="Horizontal">
            <ProgressBar Value="{Binding HealthScore}"/>
            <TextBlock Text="{Binding HealthBadge}"/>
        </StackPanel>
    </controls:ExpandableCard.HeaderContent>
    
    <!-- Custom expanded content -->
    <controls:ExpandableCard.ExpandedContent>
        <StackPanel>
            <TextBlock Text="{Binding Description}"/>
            <!-- Statistics, metrics, etc. -->
        </StackPanel>
    </controls:ExpandableCard.ExpandedContent>
</controls:ExpandableCard>
```

#### **Key Features**

1. **Parameterization via DependencyProperty**:
   - `IconKey`: JellyOrb icon identifier
   - `Title`, `Subtitle`: Card header text
   - `HeaderContent`, `HeaderContentTemplate`: Custom header content
   - `ExpandedContent`, `ExpandedContentTemplate`: Custom expanded content
   - `PrimaryActionCommand`, `SecondaryActionCommand`: Action buttons
   - `IsToggleEnabled`, `CanToggle`: Toggle switch control
   - `CardContextMenu`: Right-click menu

2. **Consistent Behavior**:
   - Uses WPF-UI `CardExpander` for native animations
   - JellyOrb icon system (supports Emoji, images, text)
   - Transparent action buttons in header
   - Optional toggle switch
   - Context menu support

3. **Flexibility**:
   - Header content can be customized via `DataTemplate`
   - Expanded content fully customizable
   - Action buttons conditionally visible
   - Toggle can be hidden or disabled

#### **Benefits**

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| **Code Lines** | ~420 lines (2 pages) | ~150 lines (control) + ~100 lines (usage) | **40% reduction** |
| **Maintenance** | Change 2+ files | Change 1 file | **50% faster** |
| **Consistency** | Manual sync required | Automatic | **100% consistent** |
| **Extensibility** | Copy-paste | Parameterize | **Scalable** |

#### **When to Create a Reusable Component**

**Create a UserControl when:**
- ✅ Similar UI pattern appears in 2+ places
- ✅ The pattern has 50%+ structural similarity
- ✅ You need to maintain visual consistency
- ✅ The component has clear parameters (icon, title, actions, etc.)

**Use DataTemplate when:**
- ✅ Simple list item rendering
- ✅ Data structure is identical across uses
- ✅ No complex interaction logic needed

**Use ControlTemplate when:**
- ✅ Need to completely customize control appearance
- ✅ Creating themeable controls
- ✅ Advanced styling scenarios

#### **Component Development Workflow**

1. **Identify Duplication**: Notice similar XAML across 2+ files
2. **Extract Common Structure**: Identify the 70%+ shared layout
3. **Define Parameters**: List all variable parts (icon, title, content, etc.)
4. **Create UserControl**:
   - Define `DependencyProperty` for each parameter
   - Build XAML template with `{Binding ElementName=Root}`
   - Use `ContentPresenter` for customizable areas
5. **Refactor Existing Pages**: Replace duplicated XAML with component usage
6. **Document in AGENTS.md**: Record the component's purpose and usage

#### **Experience Log: Settings Tabs + Componentization**

This repo has a recurring class of issues caused by *resource scope* and *theme injection timing* in WPF:
- Componentization (e.g. `ExpandableCard`) is recommended for repeating card patterns because it centralizes layout + action affordances and reduces UX drift.
- Do NOT over-componentize entire `Page`s/tabs; keep `Page` as the composition layer (filtering/grouping/sticky headers/navigation), and extract only the repeating UI primitives.
- When a reusable control depends on Pulsar styles (e.g. icon buttons), prefer making the control self-sufficient by merging the required style dictionaries in its own `UserControl.Resources`.

#### **Example: Refactoring to ExpandableCard**

**Before (Duplicated in Plugins + Slots pages):**
```xml
<ui:CardExpander>
    <ui:CardExpander.Header>
        <Grid>
            <controls:JellyOrb IconKey="{Binding Icon}"/>
            <TextBlock Text="{Binding Name}"/>
            <ui:Button Command="{Binding ConfigureCommand}"/>
            <ui:ToggleSwitch IsChecked="{Binding IsEnabled}"/>
        </Grid>
    </ui:CardExpander.Header>
    <StackPanel>
        <!-- Custom content -->
    </StackPanel>
</ui:CardExpander>
```

**After (Reusable component):**
```xml
<controls:ExpandableCard 
    IconKey="{Binding Icon}"
    Title="{Binding Name}"
    PrimaryActionCommand="{Binding ConfigureCommand}"
    IsToggleEnabled="{Binding IsEnabled}">
    <controls:ExpandableCard.ExpandedContent>
        <!-- Custom content -->
    </controls:ExpandableCard.ExpandedContent>
</controls:ExpandableCard>
```

**Result**: 15 lines → 8 lines (47% reduction per usage)

#### **Existing Reusable Components**

| Component | Location | Purpose | Used In |
|-----------|----------|---------|---------|
| `JellyOrb` | `Views/Controls/JellyOrb.xaml` | Unified icon display (Emoji/Image/Text) | Radial Menu, Settings pages |
| `ExpandableCard` | `Views/Controls/ExpandableCard.xaml` | Expandable card with icon, title, actions, toggle | Plugins, Slots (planned) |

#### **Future Componentization Opportunities**

- **Badge Component**: Reusable badge for "Core", "New", "Beta" labels
- **StatisticRow Component**: Icon + Label + Value pattern (used in analytics)
- **ActionButtonGroup**: Reusable group of transparent action buttons

---

### Key Takeaway

**"Don't Repeat Yourself (DRY)" applies to UI code too!**

When you notice similar XAML patterns across multiple pages:
1. Extract to a UserControl
2. Parameterize via DependencyProperty
3. Use ContentPresenter for customizable areas
4. Document in AGENTS.md

This approach mirrors web development's component-based architecture (React, Vue) and is the **industry standard** for WPF applications (see: Windows Terminal, Visual Studio, Microsoft Teams desktop client).

---
*Generated by Antigravity Agent*
