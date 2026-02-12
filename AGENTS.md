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
   - **Context Menus**: Do not inherit Window resources. Manually inject `ui:ControlsDictionary` into `ContextMenu.Resources`.
   - **Animations**: When switching themes, avoid clearing resources (`MergedDictionaries.Remove`) if animations are running (common in `Wpf.Ui`). Instead, update the existing `ThemesDictionary.Theme` property in place.

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
*Generated by Antigravity Agent*
