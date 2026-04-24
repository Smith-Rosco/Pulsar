# Pulsar Architecture Design Document (PADD) v4.0.0

**Status**: Published | **Core**: Plugin System v4.0 with Metadata & Circuit Breaker | **Last Updated**: 2026-03-01  
**Related Documents**: [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md), [AGENTS.md](./AGENTS.md)

---

## 1. Project Vision

Pulsar is a high-performance desktop productivity tool built with C# WPF.

* **Core Form**: Hotkey-invoked **Radial Menu**
* **Core Philosophy**: **Muscle Memory** first. Abandons traditional Alt-Tab linear traversal, utilizing spatial positioning for "blind operation"
* **Technical Evolution**: From simple "Launcher" to "Application Container". Unified plugin system handles business logic, achieving millisecond-level response and unified feedback mechanism

---

## 2. System Core Architecture

### 2.1 Operational Modes

Pulsar triggers two independent modes via different hotkeys, with strictly isolated data structures:

#### 1. Command Mode - `Ctrl + Q`
* **Logic**: Based on currently active window (context), loads statically configured actions
* **Use Cases**: Execute VBA scripts, fill passwords (PKI), data transformation, etc.
* **Layout**: Strictly corresponds to `Profiles.json` configuration, position does not change with usage frequency

#### 2. Switch Mode - `Ctrl + Shift + Q`
* **Logic**: Quickly activate other applications
* **Layout**:
  * **Outer Ring**: Static configuration (e.g., Chrome fixed on left, VS Code fixed on right)
  * **Center**: Dynamically displays **MRU (Most Recently Used)** window (i.e., "previous window") for instant return

---

### 2.2 Plugin System v4.1

**Architecture**: Pulsar core no longer contains specific business logic, only responsible for: **Capture Context** → **Dispatch Tasks** → **Render Feedback**

#### Plugin Tier Architecture

| Tier | Description | Characteristics | Examples |
|------|-------------|-----------------|----------|
| **Core Plugin** | Essential infrastructure plugins | - Cannot be disabled<br>- Crash causes app exit<br>- No Circuit Breaker protection | PKI, WinSwitcher |
| **Extension Plugin** | Optional feature plugins | - Can be disabled<br>- Crash isolation<br>- Circuit Breaker protection | VbaRunner, BookmarkletRunner |

**Key Distinction**:
- **Core plugins**: Loaded at startup, critical for basic functionality, no Circuit Breaker (crashes are fatal)
- **Extension plugins**: Optional features, isolated failures, automatic recovery via Circuit Breaker

#### Runtime Kernel

The plugin runtime is now organized around an internal runtime kernel instead of concentrating all policy in `PluginRegistry`.

- `PluginCatalog`: owns descriptor discovery, metadata registration, and dependency ordering
- `PluginRuntimeStateStore`: owns authoritative lifecycle state for loaded plugin instances
- `PluginExecutionPipeline`: enforces deterministic execution ordering for availability checks, activation readiness, execution scope, outcome classification, and telemetry
- `PluginCircuitBreakerPolicy`: owns extension-plugin breaker counters, cooldown windows, and recovery transitions
- `PluginHost`: remains an instance-hosting primitive for isolated load/unload concerns and host-local state bridging
- `PluginRegistry`: remains the external compatibility facade used by the rest of the application

#### Lifecycle Model

The runtime kernel defines one shared lifecycle vocabulary across registry-managed and host-managed execution paths:

`Unloaded` -> `Loaded` -> `Enabled` / `Disabled` -> `Running` -> `Enabled`

Fault and recovery transitions are explicit:

- Unhandled activation or execution failures move the plugin to `Faulted`
- Extension-plugin cooldown expiry moves the plugin through `Recovering` before execution is retried
- Unload transitions return the plugin to `Unloaded`

#### Circuit Breaker Mechanism

Extension plugins are protected by Circuit Breaker:

- **Trigger Condition**: 3 crashes within 1 minute
- **Breaker Duration**: 60 seconds
- **Recovery Strategy**: Half-Open state, allows single retry

**Breaker State Transitions**:
```
Closed (Normal) → Open (Breaker) → Half-Open (Test) → Closed (Recovered)
     ↑                                                    ↓
     └──────────────── Successful Execution ─────────────┘
```

The breaker is implemented as a dedicated runtime policy service and is no longer stored as field-level dictionaries in `PluginRegistry`.

#### Supported Plugin Forms

1. **Native Plugins (C# DLL)**: Run within Pulsar process, access full WPF objects
2. **FFI Plugins (Rust/C++ DLL)**: Called via P/Invoke, pursuing extreme computational performance and system-level operations
3. **Adapters (Legacy EXE)**: Compatible with old standalone tools (e.g., VBA Runner), encapsulating process calls through plugin layer

---

## 3. Technical Specifications

### 3.1 Unified Context Object (PulsarContext)

Pulsar freezes system state at invocation moment and encapsulates it as an immutable object passed to plugins. This eliminates plugin overhead of searching for windows and race condition risks.

```csharp
public class PulsarContext
{
    // Lightweight properties (synchronous acquisition)
    public IntPtr TargetWindowHandle { get; }
    public string TargetProcessName { get; }  // Uppercase, e.g., "EXCEL"
    public int TargetProcessId { get; }
    public string TargetExePath { get; }
    
    // Heavyweight properties (lazy-loaded, on-demand async acquisition)
    public Task<IReadOnlyList<ProcessWindowInfo>> GetTargetProcessWindowsAsync();
    public Task<string?> GetClipboardTextAsync();
    public Task<string?> GetSelectedTextAsync();
}
```

**Performance Optimization**:
- **Lazy Loading**: Heavy properties (clipboard, window list) are only loaded when accessed
- **Context Capture**: Captured once at radial menu invocation, avoiding repeated queries
- **Immutability**: Context is read-only, preventing plugin side effects

---

### 3.2 Plugin Interface Contract

#### IPulsarPlugin (Required)

```csharp
public interface IPulsarPlugin
{
    // Metadata
    string Id { get; }                    // Unique identifier (reverse domain format)
    string DisplayName { get; }           // Display name
    string Version { get; }               // Semantic version (e.g., "1.0.0")
    string Author { get; }                // Author/maintainer
    string Description { get; }           // Brief description
    string Icon { get; }                  // Segoe Fluent Icons or Emoji
    bool CanDisable { get; }              // Whether can be disabled
    
    // Lifecycle
    void Initialize(IServiceProvider services);
    
    // Execution
    Task<PluginResult> ExecuteAsync(
        string action,
        IReadOnlyDictionary<string, string> args,
        PulsarContext context
    );
}
```

#### IPluginTiered (Recommended)

```csharp
public interface IPluginTiered
{
    PluginTier Tier { get; }
}

public enum PluginTier
{
    Core,       // Core plugin
    Extension   // Extension plugin
}
```

#### IPluginMetadataProvider (Optional)

Plugins can provide rich metadata for UI rendering and configuration validation:

```csharp
public interface IPluginMetadataProvider
{
    PluginMetadata GetMetadata();
}

public class PluginMetadata
{
    public DisplayInfo Display { get; set; }        // Name, icon, category
    public UIHints UI { get; set; }                 // Badge, color, sort order
    public PluginCapabilities Capabilities { get; set; }  // Actions, dependencies
    public ConfigSchema Schema { get; set; }        // Configuration schema
}
```

---

### 3.3 Focus Management (Focus Boomerang)

**Problem**: When PKI plugin injects credentials, focus must return to the original window.

**Solution**: Focus Boomerang pattern

1. **Capture**: `WindowService.SetPreviousWindow()` captures foreground window handle when radial menu is invoked
2. **Execute**: Plugin performs its action
3. **Hide**: Radial menu hides
4. **Return**: `SetForegroundWindow()` forcefully returns focus to captured window
5. **Buffer**: `await Task.Delay(100)` allows window to stabilize
6. **Inject**: Send keystrokes to target window

**Implementation**:
```csharp
// In RadialMenuViewModel.Show()
_windowService.SetPreviousWindow(WindowHelper.GetForegroundWindow());

// In PkiHandler.ExecuteAsync()
await _mainWindow.Dispatcher.InvokeAsync(() => _mainWindow.Hide());
var targetHandle = _windowService.GetPreviousWindow();
WindowHelper.SetForegroundWindow(targetHandle);
await Task.Delay(100);
SendKeys.SendWait(password);
```

---

### 3.4 Storage Strategy (PKI Module)

**Split Storage** for security:

- **Profiles.json**: Stores only UI layout structure (`SecretItem` ID, Label, Icon)
- **secrets.json**: Stores sensitive data (`SecretPayload` Account, EncryptedData via DPAPI), linked by ID

**Data Hydration**: `RadialMenuViewModel` loads both files in parallel and fills Payload back into Item in memory.

**Concurrency Safety**: Must save `secrets.json` first and release file lock, **then** save `Profiles.json` (triggers reload event).

---

## 4. Configuration System

Configuration is static to ensure muscle memory.

### 4.1 Business Config vs Local UI Preferences

Pulsar now separates business configuration from device-local shell preferences.

- `Profiles.json` remains the source of truth for business configuration such as profiles, slots, themes, hotkeys, and runtime behavior.
- `LocalUiPreferences.json` stores best-effort UI-only state such as the last-opened settings page.
- Missing or invalid local UI preference data must never block startup or settings navigation; the application falls back to safe defaults.

### Profiles.json Structure

```json
{
  "Settings": {
    "CenterSlotBehavior": "MRU_Window"
  },
  "Profiles": {
    "EXCEL": {
      "CommandMode": {
        "Slot_1": { "PluginId": "com.pulsar.vbarunner", "Action": "run", "Args": { "script": "format.vbs" } },
        "Slot_3": { "PluginId": "com.pulsar.pki", "Action": "fill", "Args": { "type": "login" } }
      },
      "SwitchMode": {}
    },
    "Global": {
      "SwitchMode": {
        "Slot_1": { "PluginId": "com.pulsar.winswitcher", "Action": "activate", "Args": { "app": "chrome" } },
        "Slot_2": { "PluginId": "com.pulsar.winswitcher", "Action": "activate", "Args": { "app": "code" } }
      }
    }
  }
}
```

---

## 5. Key Architectural Decisions

### ADR-003: Dedicated Settings Shell

**Decision**: Settings navigation is owned by a dedicated shell layer built from `SettingsPageCatalog`, `SettingsShellViewModel`, and `SettingsWindow`, while `SettingsViewModel` remains focused on configuration editing state and workflows.

**Rationale**:
- Removes page-selection responsibilities from the main editor ViewModel.
- Centralizes page identifiers and metadata in one registration source.
- Allows shell-driven restoration of the last-opened page without expanding `Profiles.json`.

**Consequences**:
- New settings pages must be added through the centralized page catalog.
- `SettingsWindow` navigates by stable page IDs instead of scattered XAML tags.
- Dirty-state enforcement is coordinated by the shell, but truth remains inside `SettingsViewModel`.

### ADR-004: Staged Startup Coordination

**Decision**: Application startup is coordinated through `IAppStartupCoordinator`, with conservative blocking initialization and isolated deferred warm-up work.

**Blocking startup responsibilities**:
- plugin loading
- configuration validation pipeline setup
- logging level application
- process registry initialization
- tray initialization
- radial menu window creation
- hotkey and mouse wheel service readiness
- keyboard hook input-mode configuration

**Deferred startup responsibilities**:
- tutorial resume/start checks after the core shell is already ready

**Rationale**:
- Makes startup policy explicit instead of leaving it implicit in `App.xaml.cs` ordering.
- Preserves readiness for plugins, tray services, hotkeys, and input hooks.
- Keeps deferred failures isolated to logging rather than startup failure.

**Consequences**:
- `App.xaml.cs` remains the composition root, but not the detailed startup sequencer.
- New startup work must be classified as blocking or deferred before being added.

### ADR-001: Plugin Metadata System

**Decision**: Plugins self-describe their capabilities via `IPluginMetadataProvider`

**Rationale**:
- Eliminates hardcoded UI properties in core code
- Enables dynamic UI generation
- Supports configuration validation at load time

**Consequences**:
- New plugins require no core code changes
- UI automatically adapts to plugin capabilities
- Configuration errors detected at startup instead of runtime

### ADR-002: Circuit Breaker for Extension Plugins

**Decision**: Extension plugins protected by Circuit Breaker, Core plugins are not

**Rationale**:
- Core plugins are essential; their failure should be immediately visible
- Extension plugins are optional; graceful degradation is acceptable
- Circuit Breaker prevents cascading failures

**Consequences**:
- Extension plugin crashes don't bring down the app
- Users notified via toast when plugin disabled
- Automatic recovery after cooldown period

---

## 6. Module Structure

```
Pulsar/
├── Core/
│   ├── Plugin/
│   │   ├── IPulsarPlugin.cs           # Core plugin interface
│   │   ├── PulsarContext.cs           # Context with lazy loading
│   │   ├── PluginResult.cs            # Execution result
│   │   ├── PluginLoader.cs            # Plugin discovery & loading
│   │   └── Metadata/                  # Metadata system
│   │       ├── IPluginMetadataProvider.cs
│   │       ├── PluginMetadata.cs
│   │       ├── ConfigSchema.cs
│   │       └── ValidationRule.cs
│   └── ...
├── Plugins/
│   ├── Core/                          # Core plugins (always loaded)
│   │   ├── Pki/                       # Credential injection
│   │   └── WinSwitcher/               # Window switching
│   └── [Extension plugins]            # Optional plugins
├── Services/
│   ├── PluginRegistry.cs              # Plugin lifecycle & Circuit Breaker
│   ├── PluginMetadataRegistry.cs      # Metadata storage
│   ├── ConfigService.cs               # Configuration management
│   └── Validation/
│       └── ConfigValidationPipeline.cs # 3-stage validation
└── ...
```

---

## 7. Related Documents

- **[PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md)** - Complete plugin development guide
- **[AGENTS.md](./AGENTS.md)** - AI Agent operational guide and coding conventions
- **[PKI Implementation Archive](./docs/archive/2026-01/PKI_IMPLEMENTATION.md)** - Historical PKI implementation details

---

**Version History**:
- v4.0.0 (2026-03-01): Added plugin tier architecture, Circuit Breaker, metadata system, Focus Boomerang
- v2.0 (2026-01-26): Initial plugin system design
