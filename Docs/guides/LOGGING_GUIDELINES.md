# Logging Guidelines

## Overview

Pulsar uses Serilog for structured logging with runtime-configurable log levels. This document defines logging standards to maintain clean, useful logs.

---

## Log Levels

### Verbose (Trace)
**When to use:** Extremely detailed diagnostic information (disabled by default)
- Internal state dumps
- Loop iterations
- Raw data inspection

**Example:**
```csharp
_logger.LogTrace("Processing item {Index}/{Total}: {Data}", i, count, item);
```

---

### Debug
**When to use:** Development and troubleshooting information
- Method entry/exit (for complex flows)
- Intermediate calculation results
- Cache hits/misses

**Important:** Debug logs should be **sampled** for high-frequency operations (see Sampling section)

**Example:**
```csharp
_logger.LogDebug("Cache miss for key {Key}, fetching from source", cacheKey);
```

---

### Information
**When to use:** Key business events and milestones
- Application startup/shutdown
- Plugin load/unload
- User actions (window switch, plugin execution)
- Configuration changes

**Example:**
```csharp
_logger.LogInformation("Plugin {PluginId} executed successfully in {ElapsedMs}ms", pluginId, elapsed);
```

---

### Warning
**When to use:** Recoverable issues that don't prevent operation
- Retry attempts (first occurrence only)
- Fallback to default values
- Deprecated API usage

**Example:**
```csharp
_logger.LogWarning("Failed to load icon for {ProcessName}, using default", processName);
```

---

### Error
**When to use:** Operation failures that require attention
- Plugin execution failures
- File I/O errors (after retries exhausted)
- Invalid configuration

**Example:**
```csharp
_logger.LogError(ex, "Failed to execute plugin {PluginId}", pluginId);
```

---

### Fatal
**When to use:** Critical failures that may crash the application
- Unhandled exceptions
- Resource exhaustion
- Corrupted critical data

**Example:**
```csharp
_logger.LogFatal(ex, "Unhandled exception in UI thread");
```

---

## Best Practices

### 1. Sampling High-Frequency Logs

For operations that occur frequently (>10 times/second), use the `LogSampler` helper class:

**Recommended Approach (using LogSampler):**
```csharp
using Pulsar.Helpers;

private readonly LogSampler _captureLogSampler = new LogSampler(20); // Sample 1 in 20

public void CaptureWindow(IntPtr hWnd)
{
    if (hWnd == IntPtr.Zero)
    {
        // Sample failures to reduce log volume
        if (_captureLogSampler.ShouldLog())
        {
            _logger.LogDebug("[CaptureWindow] Invalid handle (sampled 1/{Rate})", _captureLogSampler.Rate);
        }
        return;
    }
    // ... rest of logic
}
```

**Alternative (manual counter):**
```csharp
private int _logSampleCounter = 0;
private const int LOG_SAMPLE_RATE = 10;

private void OnKeyPress(Key key)
{
    if (++_logSampleCounter % LOG_SAMPLE_RATE == 0)
    {
        _logger.LogDebug("Key pressed: {Key} (sampled 1/{Rate})", key, LOG_SAMPLE_RATE);
    }
    // ... rest of logic
}
```

**When to use sampling:**
- Window capture failures (can happen 10+ times/second)
- Process registration (happens on every window enumeration)
- Icon caching operations
- Focus management state changes
- Quick switch debug logs

**Recommended sample rates:**
- Very high frequency (>50/sec): 1 in 20-50
- High frequency (10-50/sec): 1 in 10-20
- Medium frequency (5-10/sec): 1 in 5-10

---

### 2. Error Deduplication

For errors that may repeat rapidly, use cooldown periods:

```csharp
private DateTime _lastErrorLogTime = DateTime.MinValue;
private const int ERROR_LOG_COOLDOWN_MS = 60000; // 1 minute

private void SaveFile()
{
    try
    {
        // ... save logic
    }
    catch (IOException ex)
    {
        var now = DateTime.Now;
        if ((now - _lastErrorLogTime).TotalMilliseconds > ERROR_LOG_COOLDOWN_MS)
        {
            _logger.LogWarning("File access conflict, retrying");
            _lastErrorLogTime = now;
        }
    }
}
```

---

### 3. Structured Logging

Use structured properties instead of string interpolation:

**Good:**
```csharp
_logger.LogInformation("User switched to window {WindowTitle} (PID: {ProcessId})", title, pid);
```

**Bad:**
```csharp
_logger.LogInformation($"User switched to window {title} (PID: {pid})");
```

---

### 4. Avoid Logging Sensitive Data

Never log:
- Passwords or API keys
- Full file paths (use filename only)
- Personal identifiable information (PII)

**Good:**
```csharp
_logger.LogInformation("Loaded config from {FileName}", Path.GetFileName(configPath));
```

**Bad:**
```csharp
_logger.LogInformation("Loaded config from {FullPath}", configPath);
```

---

### 5. Exception Logging

Only include exception details for Error/Fatal levels:

**Good:**
```csharp
_logger.LogError(ex, "Failed to load plugin {PluginId}", pluginId);
```

**Bad (for Warning):**
```csharp
_logger.LogWarning(ex, "Retrying operation"); // Don't include full stack trace for warnings
```

---

## Runtime Log Level Control

Users can change log levels at runtime via Settings UI:

1. Navigate to Settings > Advanced > Logging
2. Select desired log level (Verbose, Debug, Information, Warning, Error, Fatal)
3. Changes apply immediately without restart

**Default:** Information (production), Debug (development)

---

## Log File Locations

- **Main Application:** `%AppData%\Pulsar\Logs\pulsar-yyyyMMdd.log`
- **Plugins:** `%AppData%\Pulsar\Logs\Plugins\{PluginId}-yyyyMMdd.log`

**Retention:** 7 days for main logs, 30 days for plugin logs

---

## Examples from Codebase

### Good: Sampled Debug Logs (using LogSampler)
```csharp
// WindowService.cs - High-frequency window capture failures
private readonly LogSampler _captureLogSampler = new LogSampler(20);

if (_captureLogSampler.ShouldLog())
{
    _logger.LogDebug("[CaptureWindow] Invalid Handle: {Hwnd} (sampled 1/{Rate})", 
        hWnd, _captureLogSampler.Rate);
}
```

### Good: Deduplicated Warnings
```csharp
// ProcessRegistryService.cs - File access conflicts
if ((now - _lastFileConflictLogTime).TotalMilliseconds > FILE_CONFLICT_LOG_COOLDOWN_MS)
{
    _logger.LogWarning("[ProcessRegistry] File access conflict, retrying (attempt {Attempt}/{MaxRetries})", 
        attempt + 1, maxRetries);
    _lastFileConflictLogTime = now;
}
```

### Good: Consolidated Startup Logs
```csharp
// PluginRegistry.cs - Single summary instead of per-plugin logs
var loadedPluginNames = new List<string>();
foreach (var plugin in plugins)
{
    // ... load plugin
    loadedPluginNames.Add(plugin.DisplayName);
}
_logger.LogInformation("[PluginRegistry] Loaded {Count} plugins: {PluginList}", 
    plugins.Count, string.Join(", ", loadedPluginNames));
```

### Good: Information for User Actions
```csharp
// WindowService.cs - Important state changes
_logger.LogInformation("[SwitchToPreviousWindow] Established Switch Pair: '{Source}' <-> '{Target}'", 
    GetWindowTitle(sourceWindow), GetWindowTitle(targetWindow));
```

---

## Optimization History

### 2026-03-08: Log Volume Reduction
**Objective:** Reduce log volume by 40-50% while maintaining debuggability

**Changes:**
1. **Created `LogSampler` helper class** (`Helpers/LogSampler.cs`)
   - Simplifies sampling logic for high-frequency operations
   - Thread-unsafe by design for performance (minor inaccuracies acceptable)

2. **WindowService optimizations:**
   - Added sampling to window capture failures (1 in 20)
   - Added sampling to window history recording (1 in 5)
   - Added sampling to quick switch debug logs (1 in 3)
   - Downgraded focus restoration logs from Information to Debug
   - Removed redundant Debug logs in hot paths

3. **ProcessRegistryService optimizations:**
   - Added sampling to process registration (1 in 10)
   - Added sampling to icon caching (1 in 10)
   - Kept error deduplication for file conflicts (1 per minute)

4. **PluginRegistry optimizations:**
   - Consolidated plugin loading logs into single summary
   - Downgraded lifecycle hooks (OnEnable/OnDisable/OnUnload) to Debug
   - Downgraded plugin execution success from Information to Debug
   - Kept state changes and errors at Information/Warning/Error

**Impact:**
- Startup logs reduced from ~50 to ~15 lines
- Runtime Debug logs reduced by ~80-90% (via sampling)
- All Error/Warning/Critical Information logs preserved
- Expected log file size reduction: 40-50%

**Verification:**
- All changes compile successfully
- Logging behavior verified in development environment
- No functional changes to application logic

---

*Last Updated: 2026-03-08 (v2.0 - Log Optimization)*
