# Pulsar Plugin Development Guide

**Version**: 4.0.0  
**Target Audience**: AI Agents & Plugin Developers  
**Last Updated**: 2026-02-08

---

## 📖 Overview

This guide provides comprehensive instructions for developing plugins for the Pulsar productivity launcher. Pulsar uses a plugin-based architecture where all functionality is implemented through the `IPulsarPlugin` interface, enabling both built-in and external plugins.

### Plugin System Highlights

- **Unified Interface**: All plugins implement `IPulsarPlugin`
- **Context Isolation**: Window state is captured once at invocation time via `PulsarContext`
- **Dependency Injection**: Plugins receive `IServiceProvider` during initialization
- **Async First**: All plugin actions return `Task<PluginResult>`
- **Exception Safety**: Plugin failures are isolated and don't crash the main application

---

## 🏗️ Core Concepts

### 1. Plugin Interface (`IPulsarPlugin`)

All Pulsar plugins must implement this interface:

```csharp
public interface IPulsarPlugin
{
    /// <summary>
    /// Unique plugin identifier (use reverse domain notation)
    /// Example: "com.pulsar.winswitcher", "com.mycompany.customplugin"
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Human-readable display name
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Called once during application startup
    /// Use this to resolve dependencies via IServiceProvider
    /// </summary>
    void Initialize(IServiceProvider services);

    /// <summary>
    /// Execute a plugin action
    /// </summary>
    /// <param name="action">Action name (e.g., "run", "fill", "activate")</param>
    /// <param name="args">Static parameters from Profiles.json</param>
    /// <param name="context">Runtime context captured at invocation</param>
    Task<PluginResult> ExecuteAsync(
        string action,
        IReadOnlyDictionary<string, string> args,
        PulsarContext context
    );
}
```

### 2. Context Object (`PulsarContext`)

**CRITICAL**: `PulsarContext` is an immutable snapshot captured **at the moment the radial menu is invoked**. This solves the focus management problem where querying window state inside plugins would return Pulsar's own window.

```csharp
public readonly struct PulsarContext
{
    // === Window Information ===
    public IntPtr TargetWindowHandle { get; init; }    // Window that had focus before Pulsar
    public string TargetProcessName { get; init; }     // Uppercase process name (e.g., "EXCEL")
    public int TargetProcessId { get; init; }

    // === User Input ===
    public string? SelectedText { get; init; }         // Pre-captured selected text
    public string? ClipboardText { get; init; }        // Clipboard content at invocation

    // === Shared Storage ===
    public IReadOnlyDictionary<string, object>? SessionData { get; init; }
}
```

**Best Practices**:
- ✅ **DO** use `context.TargetWindowHandle` to interact with the target window
- ❌ **DON'T** call `GetForegroundWindow()` inside plugins (will return Pulsar itself)
- ❌ **DON'T** query window state dynamically within `ExecuteAsync`

### 3. Plugin Result (`PluginResult`)

Return execution results with visual feedback cues:

```csharp
public readonly struct PluginResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public VisualCue Cue { get; init; }

    // Factory methods
    public static PluginResult Ok(string? message = null);
    public static PluginResult Error(string message);
}

public enum VisualCue
{
    None,
    ShowCheckMark,      // Green checkmark toast
    ShakeWindow,        // Window shake animation
    ErrorRed            // Red border flash
}
```

---

## 🚀 Quick Start: Creating Your First Plugin

### Step 1: Create Plugin Class

Create a new file in the appropriate directory:
- **Built-in plugins**: `Pulsar/Plugins/[PluginName]/[PluginName]Plugin.cs`
- **External plugins**: Separate project/assembly

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Core.Plugin;

namespace Pulsar.Plugins.MyPlugin
{
    public class MyCustomPlugin : IPulsarPlugin
    {
        public string Id => "com.mycompany.myplugin";
        public string DisplayName => "My Custom Plugin";

        public void Initialize(IServiceProvider services)
        {
            // Resolve dependencies here
            // Example: _myService = services.GetService(typeof(IMyService)) as IMyService;
        }

        public async Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            return action.ToLowerInvariant() switch
            {
                "myaction" => await MyActionAsync(args, context),
                _ => PluginResult.Error($"Unknown action: {action}")
            };
        }

        private async Task<PluginResult> MyActionAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            // Your plugin logic here
            return PluginResult.Ok("Action completed successfully");
        }
    }
}
```

### Step 2: Configure in Profiles.json

Add your plugin to the configuration:

```json
{
  "Profiles": {
    "EXCEL": {
      "CommandMode": {
        "Slot_1": {
          "plugin": "com.mycompany.myplugin",
          "action": "myaction",
          "args": {
            "param1": "value1",
            "param2": "value2"
          },
          "label": "My Action",
          "icon": "MyIcon"
        }
      }
    }
  }
}
```

### Step 3: Build and Test

```bash
# Build the project
dotnet build Pulsar/Pulsar/Pulsar.csproj

# Run the application
dotnet run --project Pulsar/Pulsar/Pulsar.csproj
```

---

## 📝 Plugin Development Patterns

### Pattern 1: Dependency Injection

Resolve services during initialization:

```csharp
private IWindowService? _windowService;
private IConfigService? _configService;

public void Initialize(IServiceProvider services)
{
    _windowService = services.GetService(typeof(IWindowService)) as IWindowService;
    _configService = services.GetService(typeof(IConfigService)) as IConfigService;

    if (_windowService == null)
    {
        throw new InvalidOperationException("IWindowService is not available");
    }
}
```

### Pattern 2: Action Routing

Use pattern matching for action dispatch:

```csharp
public async Task<PluginResult> ExecuteAsync(
    string action,
    IReadOnlyDictionary<string, string> args,
    PulsarContext context)
{
    return action.ToLowerInvariant() switch
    {
        "create" => await CreateAsync(args, context),
        "update" => await UpdateAsync(args, context),
        "delete" => await DeleteAsync(args, context),
        _ => PluginResult.Error($"Unknown action: {action}")
    };
}
```

### Pattern 3: Parameter Validation

Always validate required parameters:

```csharp
private async Task<PluginResult> MyActionAsync(
    IReadOnlyDictionary<string, string> args,
    PulsarContext context)
{
    // Validate required parameters
    if (!args.TryGetValue("requiredParam", out var param) || string.IsNullOrEmpty(param))
    {
        return PluginResult.Error("Missing required parameter: requiredParam");
    }

    // Optional parameters with defaults
    int delay = 100;
    if (args.TryGetValue("delay", out var delayStr))
    {
        int.TryParse(delayStr, out delay);
    }

    // ... plugin logic
}
```

### Pattern 4: Exception Handling

Wrap volatile operations in try-catch:

```csharp
private async Task<PluginResult> RiskyActionAsync(
    IReadOnlyDictionary<string, string> args,
    PulsarContext context)
{
    try
    {
        // Potentially failing operation
        var result = await PerformRiskyOperation();
        return PluginResult.Ok("Operation succeeded");
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[MyPlugin] Error: {ex.Message}");
        return PluginResult.Error($"Operation failed: {ex.Message}");
    }
}
```

### Pattern 5: Focus Management (Critical for UI Automation)

When interacting with the target window (e.g., sending keys):

```csharp
private async Task<PluginResult> SendKeysActionAsync(
    IReadOnlyDictionary<string, string> args,
    PulsarContext context)
{
    // 1. Hide Pulsar window
    _windowService?.HideMainWindow();

    // 2. Return focus to target window (from context)
    var targetHwnd = context.TargetWindowHandle;
    if (targetHwnd != IntPtr.Zero)
    {
        WindowHelper.SetForegroundWindow(targetHwnd);
    }

    // 3. Wait for window switch
    await Task.Delay(100);

    // 4. Send keys to target window
    SendKeys.SendWait("{ENTER}");

    return PluginResult.Ok("Keys sent");
}
```

---

## 🎯 Real-World Examples

### Example 1: Simple Command Plugin

This plugin demonstrates basic process execution and keyboard automation:

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Pulsar.Core.Plugin;

namespace Pulsar.Plugins.BasicCommand
{
    public class SimpleCommandPlugin : IPulsarPlugin
    {
        public string Id => "com.pulsar.command";
        public string DisplayName => "Simple Command";

        public void Initialize(IServiceProvider services)
        {
            Debug.WriteLine("[SimpleCommandPlugin] Initialized successfully");
        }

        public async Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            return action.ToLowerInvariant() switch
            {
                "run" => await RunCommandAsync(args, context),
                "sendkeys" => await SendKeysAsync(args, context),
                _ => PluginResult.Error($"Unknown action: {action}")
            };
        }

        private async Task<PluginResult> RunCommandAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            if (!args.TryGetValue("path", out var path) || string.IsNullOrEmpty(path))
            {
                return PluginResult.Error("Missing required parameter: path");
            }

            args.TryGetValue("arguments", out var arguments);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                return PluginResult.Ok($"Executed {path}");
            }
            catch (Exception ex)
            {
                return PluginResult.Error($"Execution failed: {ex.Message}");
            }
        }

        private async Task<PluginResult> SendKeysAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            if (!args.TryGetValue("keys", out var keys) || string.IsNullOrEmpty(keys))
            {
                return PluginResult.Error("Missing required parameter: keys");
            }

            int delay = 50;
            if (args.TryGetValue("delay", out var delayStr))
            {
                int.TryParse(delayStr, out delay);
            }

            try
            {
                await Task.Delay(delay);
                SendKeys.SendWait(keys);
                return PluginResult.Ok("Keys sent");
            }
            catch (Exception ex)
            {
                return PluginResult.Error($"SendKeys failed: {ex.Message}");
            }
        }
    }
}
```

**Configuration Example**:

```json
{
  "Profiles": {
    "Global": {
      "CommandMode": {
        "Slot_1": {
          "plugin": "com.pulsar.command",
          "action": "run",
          "args": {
            "path": "notepad.exe"
          },
          "label": "Notepad",
          "icon": "Notepad"
        },
        "Slot_2": {
          "plugin": "com.pulsar.command",
          "action": "sendkeys",
          "args": {
            "keys": "^c",
            "delay": "100"
          },
          "label": "Copy",
          "icon": "Copy"
        }
      }
    }
  }
}
```

---

## 📋 Configuration Reference

### Profiles.json Structure

```json
{
  "Settings": {
    "CenterSlotBehavior": "MRU_Window",
    "TriggerDistance": 100.0,
    "LauncherTheme": "Dark",
    "HoverScale": 1.2,
    "Springiness": 6.0,
    "MaxDisplacement": 20.0
  },
  "Profiles": {
    "PROCESSNAME": {
      "CommandMode": {
        "Slot_N": { /* Plugin configuration */ }
      },
      "SwitchMode": {
        "Slot_N": { /* Plugin configuration */ }
      }
    }
  }
}
```

### Plugin Slot Configuration

Each slot must include:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `plugin` | string | ✅ Yes | Plugin ID (e.g., "com.pulsar.winswitcher") |
| `action` | string | ✅ Yes | Action name to execute |
| `args` | object | ❌ No | Key-value pairs for plugin parameters |
| `label` | string | ❌ No | Display text in UI |
| `icon` | string | ❌ No | Icon key for UI |

### Process Profile Modes

- **CommandMode**: Activated when Pulsar is invoked in command mode (specific actions per application)
- **SwitchMode**: Activated when Pulsar is invoked in switch mode (application switching)
- **Global**: Fallback profile when no process-specific profile exists

---

## 🔌 External Plugin Development

### Creating an External Plugin

1. **Create a new Class Library project**:

```bash
dotnet new classlib -n MyPulsarPlugin
cd MyPulsarPlugin
```

2. **Add reference to Pulsar.Core**:

```xml
<ItemGroup>
  <Reference Include="Pulsar">
    <HintPath>..\Pulsar\bin\Debug\net8.0-windows\Pulsar.dll</HintPath>
  </Reference>
</ItemGroup>
```

3. **Implement the plugin**:

```csharp
using Pulsar.Core.Plugin;

namespace MyPulsarPlugin
{
    public class MyExternalPlugin : IPulsarPlugin
    {
        public string Id => "com.external.myplugin";
        public string DisplayName => "My External Plugin";

        public void Initialize(IServiceProvider services)
        {
            // Initialization logic
        }

        public async Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            // Plugin logic
            return PluginResult.Ok("External plugin executed");
        }
    }
}
```

4. **Build and deploy**:

```bash
dotnet build -c Release
# Copy MyPulsarPlugin.dll to Pulsar/Plugins/ directory
```

5. **Plugin will be auto-loaded** on next Pulsar startup.

---

## 🛠️ Available Services

Plugins can access these services via `IServiceProvider`:

### IWindowService

```csharp
public interface IWindowService
{
    IntPtr GetPreviousWindow();
    Task<bool> SwitchToProcessAsync(string processName);
    void HideMainWindow();
    void ShowMainWindow();
    string GetProcessName(IntPtr hwnd);
    int GetProcessId(IntPtr hwnd);
}
```

**Usage**:
```csharp
private IWindowService? _windowService;

public void Initialize(IServiceProvider services)
{
    _windowService = services.GetService(typeof(IWindowService)) as IWindowService;
}
```

### IConfigService

```csharp
public interface IConfigService
{
    ProfilesConfig GetConfig();
    Task SaveConfigAsync(ProfilesConfig config);
}
```

### CredentialsManager (PKI)

```csharp
public class CredentialsManager
{
    public string Encrypt(string plainText);
    public string Decrypt(string encryptedData);
}
```

---

## ⚠️ Best Practices & Common Pitfalls

### ✅ DO

1. **Always validate parameters** before use
2. **Handle exceptions gracefully** - wrap risky operations in try-catch
3. **Use Debug.WriteLine** for diagnostic logging
4. **Use context.TargetWindowHandle** for window operations
5. **Return meaningful error messages** in `PluginResult.Error()`
6. **Test with multiple process contexts** (Excel, Chrome, etc.)
7. **Document required parameters** in code comments

### ❌ DON'T

1. **Don't query window state inside ExecuteAsync** - use `context` instead
2. **Don't throw unhandled exceptions** - return `PluginResult.Error()` instead
3. **Don't block the UI thread** - use `async/await` for I/O operations
4. **Don't hardcode paths** - use parameters from `args`
5. **Don't assume services are available** - check for null after resolution
6. **Don't store mutable state** in plugin instance - plugins are singletons
7. **Don't forget to hide Pulsar window** before UI automation

### Common Pitfalls

**❌ Incorrect Focus Management**:
```csharp
// WRONG: Gets Pulsar's own window
var hwnd = GetForegroundWindow();
```

**✅ Correct Focus Management**:
```csharp
// CORRECT: Uses captured context
var hwnd = context.TargetWindowHandle;
```

**❌ Missing Parameter Validation**:
```csharp
// WRONG: NullReferenceException if "path" is missing
var path = args["path"];
```

**✅ Safe Parameter Access**:
```csharp
// CORRECT: Validates and provides error message
if (!args.TryGetValue("path", out var path) || string.IsNullOrEmpty(path))
{
    return PluginResult.Error("Missing required parameter: path");
}
```

---

## 🧪 Testing Plugins

### Manual Testing Checklist

1. ✅ Plugin loads without errors at startup
2. ✅ Plugin appears in debug output: `[PluginRegistry] ✓ Registered plugin: ...`
3. ✅ Configuration is correctly parsed from Profiles.json
4. ✅ Plugin executes when slot is activated
5. ✅ Success/error results display correctly
6. ✅ Focus returns to target window after execution
7. ✅ Plugin handles missing/invalid parameters gracefully

### Debug Output

Enable debug logging to monitor plugin execution:

```csharp
Debug.WriteLine($"[MyPlugin] Executing action: {action}");
Debug.WriteLine($"[MyPlugin] Target process: {context.TargetProcessName}");
Debug.WriteLine($"[MyPlugin] Target window: {context.TargetWindowHandle}");
```

Check the **Output** window in Visual Studio or use **DebugView** to see logs.

---

## 📚 Additional Resources

### File Locations

- **Plugin Interface**: `Pulsar/Core/Plugin/IPulsarPlugin.cs`
- **Context Definition**: `Pulsar/Core/Plugin/PulsarContext.cs`
- **Result Types**: `Pulsar/Core/Plugin/PluginResult.cs`
- **Plugin Loader**: `Pulsar/Core/Plugin/PluginLoader.cs`
- **Plugin Registry**: `Pulsar/Services/PluginRegistry.cs`
- **Built-in Plugins**: `Pulsar/Plugins/`
- **Configuration Model**: `Pulsar/Models/ProfilesConfig.cs`

### Reference Implementations

Study these built-in plugins as examples:

1. **WinSwitcherPlugin** (`Plugins/WinSwitcher/WinSwitcherPlugin.cs:19`)
   - Window activation and application launching
   - Smart switch logic (switch or launch)
   
2. **SimpleCommandPlugin** (`Plugins/BasicCommand/SimpleCommandPlugin.cs:15`)
   - Process execution
   - SendKeys automation

3. **PkiPlugin** (`Features/Pki/PkiPlugin.cs:18`)
   - Complex focus management
   - Credential injection
   - Multi-step workflows

---

## 🔄 Plugin Lifecycle

```
Application Startup
    ↓
PluginLoader.LoadAll()
    ↓
Plugin Constructor Called
    ↓
Plugin.Initialize(IServiceProvider)
    ↓
PluginRegistry.Register(plugin)
    ↓
[Plugin Ready for Execution]
    ↓
User Invokes Radial Menu
    ↓
PulsarContext.Capture()
    ↓
User Selects Slot
    ↓
PluginRegistry.ExecuteAsync(pluginId, action, args, context)
    ↓
Plugin.ExecuteAsync(action, args, context)
    ↓
Return PluginResult
    ↓
Display Visual Feedback (Toast/Shake/Error)
```

