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
- **Context Passing**: `PulsarContext` - Immutable context snapshot captured when radial menu is invoked
- **Plugin Registry**: `PluginRegistry` - Manages plugin lifecycle and execution
- **Plugin Loader**: `PluginLoader` - Loads plugins from both built-in assembly and external DLLs
- **Configuration**: `Profiles.json` - Single source of truth for all configuration (replaces legacy `appsettings.json`)

**Core Principles**:
- **Focus Management**: All window context is captured at invocation time via `PulsarContext.Capture()`
- **Plugin Isolation**: Plugin exceptions do not crash the main application
- **Dependency Injection**: Plugins receive `IServiceProvider` during initialization
- **Async First**: All plugin actions are async (`Task<PluginResult>`)

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
    - `PulsarContext.cs`: Immutable context snapshot
    - `PluginResult.cs`: Plugin execution result types
    - `PluginLoader.cs`: Plugin loading infrastructure
- `Plugins/`: Built-in plugin implementations
  - `WinSwitcher/`: Window switching and application launching plugin
  - `BasicCommand/`: Simple command execution plugin
- `Features/`: Feature-specific modules (e.g., `Pki/` for Secret Management).
  - `Pki/PkiPlugin.cs`: PKI credentials management plugin
- `Helpers/`: Static utilities and extensions.
- `Models/`: Data transfer objects and configuration models.
  - `ProfilesConfig.cs`: New unified configuration model (v4.0.0+)
- `Services/`: Business logic implementations (singleton/transient services).
  - `PluginRegistry.cs`: Plugin registry and execution service
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
  - log errors via `Debug.WriteLine` or a logging service (if available).

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

### Adding a New Service
1. Define the interface in `Services/Interfaces/`.
2. Implement the class in `Services/`.
3. Register the service in `App.xaml.cs` (`ConfigureServices` method).

### Adding a New Plugin
1. Create a new class implementing `IPulsarPlugin` in `Plugins/[PluginName]/` (for built-in) or external project (for external plugins).
2. Implement required properties: `Id` (unique identifier), `DisplayName`.
3. Implement `Initialize(IServiceProvider)` for dependency injection.
4. Implement `ExecuteAsync(action, args, context)` for action handling.
5. Use `PulsarContext` to access window information - NEVER query window state inside plugins.
6. See `PLUGIN_DEVELOPMENT.md` for detailed plugin development guide.

### Managing Secrets (PKI)
- Secrets are handled by `PkiPlugin` and `CredentialsManager`.
- Ensure sensitive data models use `[JsonIgnore]` to prevent accidental serialization to config files.

## 5. Agent Behavior Rules

- **Proactiveness**: If a missing null check is spotted, fix it.
- **Context**: Always read the file before editing to preserve local style conventions.
- **Safety**: Do not commit secrets or API keys. 
- **Validation**: After making changes, run `dotnet build` to ensure no compilation errors were introduced.

---
*Generated by Antigravity Agent*
