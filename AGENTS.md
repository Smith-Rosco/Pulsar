# AGENTS.md - Operational Guide for AI Agents

This file provides essential context, conventions, and routing for AI agents working on the **Pulsar** codebase.

---

## 1. Project Snapshot (30 seconds)

**Pulsar** is a high-performance productivity launcher for Windows featuring a radial menu interface.

- **Framework**: .NET 8.0 (WPF + WinForms)
- **Architecture**: Modular Monolith using MVVM (CommunityToolkit.Mvvm) and Dependency Injection
- **Core Features**: Radial Menu, Global Hotkeys, PKI/Secret Management, Extensible Plugin System

**Key Architectural Primitives**:
- **PulsarContext**: Immutable context snapshot captured at radial menu invocation (lazy-loaded)
- **Plugin Tiers**: Core (essential, fail-fast) vs Extension (optional, Circuit Breaker protected)
- **Configuration**: `Profiles.json` - Single source of truth

**Deep Dive**: See [ARCHITECTURE.md](./ARCHITECTURE.md) and [Docs/architecture/](./Docs/architecture/)

---

## 2. Non-Negotiable Invariants

### Plugin Rules

**Rule**: Never query live window state inside plugins; always use `PulsarContext`.

**Rule**: Respect plugin tier semantics:
- **Core plugins** (`Plugins/Core/`): Essential, cannot be disabled, crashes are fatal
- **Extension plugins** (`Plugins/`): Optional, Circuit Breaker protected (3 crashes in 1 min = 60s disable)

**Deep Dive**: [Docs/architecture/PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md), [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md)

### UI Rules

**Rule**: Do NOT use `Appearance="Primary"` on Wpf.Ui buttons. Always use explicit Pulsar button styles:
- `PulsarPrimaryButtonStyle`
- `PulsarSecondaryButtonStyle`
- `PulsarDangerButtonStyle`

**Why**: Dynamic resource inheritance breaks with Multi-Headed UI architecture, causing invisible text on hover.

**Deep Dive**: [Docs/lessons/WPFUI_BUTTON_PRIMARY_BUG.md](./Docs/lessons/WPFUI_BUTTON_PRIMARY_BUG.md)

**Rule**: Multi-Headed UI architecture - `App.xaml` does NOT contain global styles. Manually inject themes via `IThemeService.ApplyTheme()` for all Windows/Pages.

**Deep Dive**: [Docs/lessons/WPF_THEME_INJECTION_PITFALLS.md](./Docs/lessons/WPF_THEME_INJECTION_PITFALLS.md)

---

## 3. Critical Pitfalls (Blood & Tears Archive)

### Theme Injection Timing (Pages)

**Symptom**: Theme DynamicResources missing (e.g., `Theme.Orb.*`), blank/unstyled visuals.

**Root Cause**: If a `Page` defines `<Page.Resources>`, calling `ApplyTheme()` **before** `InitializeComponent()` causes XAML load to replace the Resources dictionary, discarding injected themes.

**Fix**: Call `ApplyTheme()` **after** `InitializeComponent()` for Pages.

**Deep Dive**: [Docs/lessons/WPF_THEME_INJECTION_PITFALLS.md](./Docs/lessons/WPF_THEME_INJECTION_PITFALLS.md)

---

### Resources Hygiene (XAMLParseException)

**Symptom**: `XAMLParseException` - "Resources property can only be set once".

**Root Cause**: Mixing `<ResourceDictionary>` wrapper with additional resources outside of it.

**Fix**: All resources must be inside the same `<ResourceDictionary>` block.

**Deep Dive**: [Docs/lessons/WPF_RESOURCES_HYGIENE.md](./Docs/lessons/WPF_RESOURCES_HYGIENE.md)

---

### UserControl DataContext Binding Breaks

**Symptom**: Buttons inside UserControl content have `Command = NULL`.

**Root Cause**: UserControls break visual tree for `RelativeSource` bindings; ContentPresenter bypasses wrappers.

**Fix**: Use Code-Behind `Loaded` event to manually set `Tag` property as bridge.

**Deep Dive**: [Docs/lessons/WPF_USERCONTROL_BINDING_BREAKS.md](./Docs/lessons/WPF_USERCONTROL_BINDING_BREAKS.md)

---

### ContextMenu Resource Inheritance

**Symptom**: ContextMenu items appear unstyled.

**Root Cause**: ContextMenus render in separate visual tree (Popup) and do not inherit Window resources.

**Fix**: Manually inject `ui:ControlsDictionary` into `ContextMenu.Resources`.

**Deep Dive**: [Docs/lessons/CONTEXTMENU_RESOURCE_INHERITANCE.md](./Docs/lessons/CONTEXTMENU_RESOURCE_INHERITANCE.md)

---

### Hidden Scrollbars

**Symptom**: Scrollbars remain visible despite `ScrollViewer.VerticalScrollBarVisibility="Hidden"`.

**Root Cause**: Internal control templates override implicit styles.

**Fix**: Use Code-Behind Visual Tree Helper to recursively find and hide ScrollViewers at runtime.

**Deep Dive**: [Docs/lessons/WPF_SCROLLVIEWER_VISIBILITY.md](./Docs/lessons/WPF_SCROLLVIEWER_VISIBILITY.md)

---

## 4. Task Router (Where to Look)

| Task | Document |
|------|----------|
| **Build/Run commands** | [Docs/ops/BUILD_AND_RUN.md](./Docs/ops/BUILD_AND_RUN.md) |
| **Add/modify plugin** | [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md), [Docs/architecture/PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md) |
| **Add dialog** | [Docs/architecture/DIALOG_SYSTEM.md](./Docs/architecture/DIALOG_SYSTEM.md) |
| **Modify UI (XAML)** | [Docs/guides/UI_BEST_PRACTICES.md](./Docs/guides/UI_BEST_PRACTICES.md) |
| **Use reusable components** | [Docs/guides/COMPONENT_LIBRARY.md](./Docs/guides/COMPONENT_LIBRARY.md) |
| **Understand architecture** | [ARCHITECTURE.md](./ARCHITECTURE.md), [Docs/architecture/](./Docs/architecture/) |
| **Input injection (PKI)** | [Docs/architecture/INPUT_INJECTION.md](./Docs/architecture/INPUT_INJECTION.md) |
| **WPF UI issues** | [Docs/lessons/](./Docs/lessons/) |
| **Architectural decisions** | [Docs/decisions/](./Docs/decisions/) |
| **Documentation standards** | [Docs/CONTRIBUTING.md](./Docs/CONTRIBUTING.md) |

---

## 5. Code Style & Conventions

### General
- **Language**: C# 12 / .NET 8.0
- **Nullable Reference Types**: Enabled. Handle null warnings explicitly.
- **Formatting**: Allman style braces, 4 spaces indentation, UTF-8 encoding.

### Naming Conventions
- **Classes/Structs/Enums**: `PascalCase`
- **Interfaces**: `I` prefix + `PascalCase`
- **Methods**: `PascalCase`
- **Async Methods**: Suffix with `Async`
- **Properties**: `PascalCase`
- **Private Fields**: `_camelCase`
- **Parameters/Locals**: `camelCase`
- **Event Handlers**: `On[EventName]`

### Project Structure
- `Core/`: Interfaces, base types, plugin system core
- `Plugins/Core/`: Essential infrastructure plugins (PKI, Hotkey)
- `Plugins/`: Extension plugins (WinSwitcher, VbaRunner, etc.)
- `Services/`: Business logic (PluginRegistry, ConfigService, etc.)
- `ViewModels/`: MVVM ViewModels (use `CommunityToolkit.Mvvm`)
- `Views/`: XAML Windows, Controls, UserControls
- `Helpers/`: Static utilities and extensions
- `Models/`: DTOs and configuration models

### Coding Patterns
- **Dependency Injection**: Constructor injection, register in `App.xaml.cs`
- **MVVM**: Use `[ObservableProperty]` and `[RelayCommand]` from CommunityToolkit
- **Async/Await**: Use `async Task` for I/O operations. Avoid `async void` except event handlers.
- **Error Handling**: Use `try/catch` around volatile operations. Use `ILogger<T>` for logging (not `Debug.WriteLine`).
- **Native Interop**: Use `LibraryImport` or `DllImport` in `NativeMethods` classes.

---

## 6. Common Workflows

### Adding a New Service
1. Define interface in `Services/Interfaces/`
2. Implement class in `Services/`
3. Register in `App.xaml.cs` (`ConfigureServices` method)

### Adding a New Plugin
1. Choose tier (Core vs Extension)
2. Create class implementing `IPulsarPlugin` in appropriate location
3. Implement `Initialize()` and `ExecuteAsync()`
4. Use `PulsarContext` for window information (never query live state)

**Deep Dive**: [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md)

### Adding a New Dialog
1. Create ViewModel implementing `IDialogViewModel` in `ViewModels/Dialogs/`
2. Create UserControl in `Views/Dialogs/Contents/`
3. Use `DialogService.ShowCustomAsync<T>()` to display

**Deep Dive**: [Docs/architecture/DIALOG_SYSTEM.md](./Docs/architecture/DIALOG_SYSTEM.md)

### Modifying UI (XAML)
1. Check `Views/` for relevant `.xaml` file
2. Ensure data binding matches ViewModel properties
3. Use StaticResources from `Themes/Theme.*.xaml`
4. Apply theme via `IThemeService.ApplyTheme()` (after `InitializeComponent()` for Pages)
5. Use Pulsar button styles (not `Appearance="Primary"`)

**Deep Dive**: [Docs/guides/UI_BEST_PRACTICES.md](./Docs/guides/UI_BEST_PRACTICES.md), [Docs/lessons/](./Docs/lessons/)

### Managing Secrets (PKI)
- Secrets handled by `PkiPlugin` (`Plugins/Core/Pki/`) and `CredentialsManager`
- Use `[JsonIgnore]` on sensitive data models to prevent serialization

---

## 7. Error Handling & Logging (Pulsar Sentinel)

### Infrastructure
- **Framework**: Serilog (Structured Logging)
- **Log Path**: `%AppData%\Pulsar\Logs\pulsar-yyyyMMdd.log`
- **DI Integration**: Logger registered in `App.xaml.cs` via `.AddSerilog()`

### Global Safety Net
`App.xaml.cs` implements three-tier exception interceptor:
1. **DispatcherUnhandledException**: UI thread crashes (logs Fatal, keeps app alive)
2. **UnobservedTaskException**: Background Task crashes (logs Error, prevents termination)
3. **AppDomain.UnhandledException**: Catastrophic failures (logs Fatal before crash)

### Usage Pattern
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
        try { /* work */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to do work");
        }
    }
}
```

### Circuit Breaker Feedback
- Extension plugins that crash 3 times in 1 minute are auto-disabled for 60 seconds
- User notified via `ITrayService.ShowNotification` (Windows Toast)
- Plugin enters "Half-Open" state after cooldown, allowing single retry

---

## 8. Agent Behavior Rules

- **Proactiveness**: Fix obvious issues (missing null checks, etc.) when spotted
- **Context**: Always read files before editing to preserve local conventions
- **Safety**: Never commit secrets or API keys
- **Validation**: Run `dotnet build` after changes to ensure no compilation errors
- **Documentation**: Update relevant docs when making architectural changes

---

## 9. Quick Commands

```bash
# Build
dotnet build Pulsar/Pulsar/Pulsar.csproj

# Run
dotnet run --project Pulsar/Pulsar/Pulsar.csproj

# Restore dependencies
dotnet restore Pulsar/Pulsar/Pulsar.csproj
```

**Full command reference**: [Docs/ops/BUILD_AND_RUN.md](./Docs/ops/BUILD_AND_RUN.md)

---

## 10. Documentation Index

- **[Docs/README.md](./Docs/README.md)** - Documentation center with task-oriented navigation
- **[ARCHITECTURE.md](./ARCHITECTURE.md)** - System architecture overview
- **[PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md)** - Plugin development guide
- **[Docs/architecture/](./Docs/architecture/)** - Detailed architecture docs (Plugin System, Dialog System, Input Injection)
- **[Docs/guides/](./Docs/guides/)** - How-to guides (UI Best Practices, Component Library)
- **[Docs/lessons/](./Docs/lessons/)** - Pain archive (WPF pitfalls, known issues)
- **[Docs/decisions/](./Docs/decisions/)** - Architecture Decision Records (ADRs)
- **[Docs/ops/](./Docs/ops/)** - Operational docs (Build/Run, Release)
- **[Docs/archive/](./Docs/archive/)** - Historical documents (not current truth)

---

*Last Updated: 2026-03-03*  
*Version: 2.0.0 (Refactored for AI-first documentation architecture)*
