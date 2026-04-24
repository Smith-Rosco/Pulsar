# Plugin System Architecture

**Status**: Published  
**Scope**: Architecture  
**Applies To**: All Pulsar plugins  
**Last Updated**: 2026-04-24

---

## Overview

Pulsar uses a plugin-based architecture where the core application is responsible only for: **Capture Context** → **Dispatch Tasks** → **Render Feedback**. All business logic is implemented as plugins.

---

## Plugin Tier Architecture

| Tier | Description | Characteristics | Examples |
|------|-------------|-----------------|----------|
| **Core Plugin** | Essential infrastructure plugins | - Cannot be disabled<br>- Crash causes app exit<br>- No Circuit Breaker protection<br>- Located in `Plugins/Core/` | PKI, Hotkey management |
| **Extension Plugin** | Optional feature plugins | - Can be disabled<br>- Crash isolation<br>- Circuit Breaker protection<br>- Located in `Plugins/` | WinSwitcher, VbaRunner, BookmarkletRunner |

---

## Key Distinction

- **Core plugins**: Loaded at startup, critical for basic functionality, no Circuit Breaker (crashes are fatal)
- **Extension plugins**: Optional features, isolated failures, automatic recovery via Circuit Breaker

---

## Circuit Breaker Mechanism

Extension plugins are protected by Circuit Breaker to prevent cascading failures:

- **Trigger Condition**: 3 crashes within 1 minute
- **Breaker Duration**: 60 seconds
- **Recovery Strategy**: Half-Open state, allows single retry

**State Transitions**:
```
Closed (Normal) → Open (Breaker) → Half-Open (Test) → Closed (Recovered)
     ↑                                                    ↓
     └──────────────── Successful Execution ─────────────┘
```

**User Feedback**: When a plugin is disabled, the runtime breaker policy invokes `ITrayService.ShowNotification` to alert the user via a Windows Toast/Balloon tip.

---

## Runtime Kernel

Pulsar now separates plugin runtime responsibilities into explicit internal services:

- `PluginCatalog`: discovery, descriptor registration, metadata, dependency ordering
- `PluginRuntimeStateStore`: authoritative lifecycle state and runtime snapshots for loaded plugins
- `PluginExecutionPipeline`: deterministic execution ordering for enablement, breaker availability, activation, execution scope, outcome classification, and telemetry
- `PluginCircuitBreakerPolicy`: crash counters, cooldown windows, and recovery signaling for extension plugins
- `PluginHost`: isolated instance hosting, unload behavior, and host-local state bridging

`PluginRegistry` remains the compatibility-facing entry point used by the application, but it now delegates orchestration to the runtime kernel instead of owning all runtime policy itself.

---

## Plugin Interface Contract

### IPulsarPlugin (Required)

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

### IPluginTiered (Recommended)

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

### IPluginMetadataProvider (Optional)

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

## PulsarContext (Unified Context Object)

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

**Critical Rule**: Never query live window state inside plugins; always use `PulsarContext`.

---

## Supported Plugin Forms

1. **Native Plugins (C# DLL)**: Run within Pulsar process, access full WPF objects
2. **FFI Plugins (Rust/C++ DLL)**: Called via P/Invoke, pursuing extreme computational performance and system-level operations
3. **Adapters (Legacy EXE)**: Compatible with old standalone tools (e.g., VBA Runner), encapsulating process calls through plugin layer

---

## Plugin Lifecycle

### Lifecycle States

- `Unloaded`: no live plugin instance is attached to runtime state
- `Loaded`: instance exists and settings have been applied, but enablement is not yet assumed implicitly by the host
- `Enabled`: plugin is ready to execute
- `Disabled`: plugin instance may remain loaded, but execution is blocked by user state
- `Running`: plugin action is actively executing through the runtime pipeline
- `Faulted`: activation or execution failed with an unhandled exception or a critical outcome
- `Recovering`: extension plugin cooldown has expired and the runtime is allowing a retry path

### Lifecycle Flow

1. **Discovery**: `PluginLoader` scans built-in assemblies and external plugin folders.
2. **Catalog Registration**: `PluginCatalog` stores descriptors and metadata ordering independently from activation.
3. **Activation**: `PluginRuntimeKernel` activates instances on demand and applies persisted settings.
4. **Enablement**: lifecycle hooks such as `OnEnableAsync()` and `OnDisableAsync()` are triggered by runtime state transitions rather than by `PluginHost` load alone.
5. **Execution**: `PluginExecutionPipeline` evaluates enablement and breaker availability before invoking plugin business logic.
6. **Outcome Handling**: success, handled failure, and exception outcomes are classified in one place and drive telemetry plus breaker updates.
7. **Unload**: `PluginHost` and registry shutdown flows call `OnUnloadAsync()` and return the plugin to `Unloaded`.

### Execution Ordering

Every plugin execution follows the same ordered stages:

1. Resolve descriptor and current runtime state.
2. Evaluate user enablement and extension breaker availability.
3. Ensure an activated plugin instance exists.
4. Open `PluginExecutionContext` scope for logging correlation.
5. Invoke plugin business logic.
6. Classify the outcome as success, handled failure, exception, or blocked.
7. Update breaker policy, usage tracking, and health monitoring from that unified outcome.

---

## Configuration

Plugins are configured via `Profiles.json`:

```json
{
  "Profiles": {
    "EXCEL": {
      "CommandMode": {
        "Slot_1": { 
          "PluginId": "com.pulsar.vbarunner", 
          "Action": "run", 
          "Args": { "script": "format.vbs" } 
        }
      }
    }
  }
}
```

---

## Related Documents

- [PLUGIN_DEVELOPMENT.md](../../PLUGIN_DEVELOPMENT.md) - Plugin development guide
- [ARCHITECTURE.md](../../ARCHITECTURE.md) - System architecture overview
- [ADR-001: Plugin Metadata System](../decisions/001-plugin-metadata-system.md) - Metadata design decision
- [ADR-002: Circuit Breaker Pattern](../decisions/002-circuit-breaker-for-extension-plugins.md) - Circuit breaker design decision

---

**Change History**:
- v1.0.0 (2026-03-03): Initial extraction from AGENTS.md and ARCHITECTURE.md
