## Why

The plugin execution pipeline has no timeout or cancellation mechanism. A hung plugin (e.g., VbaRunner COM call stalls, BookmarkletRunner UIA hangs) permanently blocks the pipeline — subsequent hotkey invocations silently fail until the process is killed. The Circuit Breaker only triggers on exceptions, not hangs, making this a completely undetected failure mode. Separately, `ConfigService._cachedConfig` lacks synchronization, risking silent data loss when concurrent save/load races occur. Both issues are latent in production and will manifest under load.

## What Changes

- **BREAKING**: Add `CancellationToken` parameter to `IPulsarPlugin.ExecuteAsync()` signature — all 6 plugin implementations must be updated
- **BREAKING**: Add `CancellationToken` to `IPluginExecutionPipeline.ExecuteAsync()` and `IPluginRuntimeKernel.ExecuteAsync()`
- Add 30-second hard timeout per plugin execution via `CancellationTokenSource.CancelAfter()` in the pipeline
- Treat execution timeout as a Circuit Breaker fault — classify as `Critical` severity, transition plugin to `Faulted`, record breaker failure
- Add `lock`-based synchronization to `ConfigService._cachedConfig` with disk-first write ordering (write file → update cache)
- Replace `GetAwaiter().GetResult()` sync-over-async in `PluginHost.Dispose()` and `PluginUsageTracker.Dispose()` with fire-and-forget patterns
- Fix dead code: identical ternary branches (`CanDisable ? Enabled : Enabled`) in `PluginRuntimeKernel` lines 387 and 665

## Capabilities

### New Capabilities
- `plugin-execution-timeout`: Hard timeout per plugin execution (30s default), timeout triggers circuit breaker fault, timed-out plugin transitions to Faulted state with user notification

### Modified Capabilities
- `plugin-runtime-kernel`: Plugin execution pipeline now accepts `CancellationToken`, enforces timeout, and reports timeout as a breaker-triggering fault
- `thread-safe-plugin-state`: ConfigService `_cachedConfig` access now synchronized with disk-first ordering

## Impact

- **Core Interfaces**: `IPulsarPlugin`, `IPluginExecutionPipeline`, `IPluginRuntimeKernel` — all gain `CancellationToken` parameter (breaking)
- **All 6 plugins**: `PkiPlugin`, `WinSwitcherPlugin`, `SystemCommandPlugin`, `VbaRunnerPlugin`, `SimpleCommandPlugin`, `BookmarkletRunnerPlugin` — must accept and forward `CancellationToken`
- **PluginRuntimeKernel.cs**: Pipeline timeout logic, breaker integration, dead code cleanup
- **ConfigService.cs**: `_cachedConfig` synchronization, disk-first ordering
- **PluginHost.cs**: `Dispose()` async pattern fix
- **PluginUsageTracker.cs**: `Dispose()` async pattern fix
- **Strategy layer**: `IActionStrategy.ExecuteAsync()` and all implementations must propagate cancellation token
