## Context

Pulsar's plugin execution pipeline (`PluginExecutionPipeline` in `PluginRuntimeKernel.cs:327`) processes every plugin invocation through a 7-stage deterministic pipeline. The pipeline uses `ConfigureAwait(false)` throughout and delegates breaker policy, state transitions, and telemetry to dedicated singleton services. However, there is no timeout enforcement at any layer — the pipeline awaits `plugin.ExecuteAsync()` unconditionally. A hung plugin (COM stall, UIA hang, stuck process) permanently occupies the pipeline's attention without throwing an exception, so the Circuit Breaker never activates.

Separately, `ConfigService` (`ConfigService.cs:19`) maintains a `_cachedConfig` field that is read in `LoadInternalAsync()` (line 90) and written in `SaveAsync()` (line 257) without any synchronization primitive. Multiple concurrent paths (hotkey handler, settings save, tutorial orchestration, background smart detection) can race on this field.

Two `Dispose()` methods use `GetAwaiter().GetResult()` to synchronously wait on async work: `PluginHost.Dispose()` (line 272) and `PluginUsageTracker.Dispose()` (line 285).

## Goals / Non-Goals

**Goals:**
- Enforce a hard 30-second timeout per plugin execution via `CancellationTokenSource.CancelAfter`
- Treat execution timeout as a Circuit Breaker fault — transition plugin to `Faulted`, record breaker failure, notify user
- Propagate `CancellationToken` through the full call stack: kernel → pipeline → plugin interface → plugin implementation
- Synchronize `ConfigService._cachedConfig` access with disk-first write ordering
- Eliminate sync-over-async in `Dispose()` methods

**Non-Goals:**
- User-initiated cancellation (e.g., "cancel" button during execution) — out of scope; hardware timeout only
- Per-plugin configurable timeout values — all plugins share the same 30s ceiling
- Changing `PluginHost` architectural role — this is legacy code not on the main execution path; only the `Dispose()` anti-pattern is fixed
- Adding timeout to non-plugin async work (e.g., `ConfigService.SaveAsync`, `PluginLoader.DiscoverDescriptors`)

## Decisions

### D1: Add CancellationToken to IPulsarPlugin.ExecuteAsync (Breaking)

**Choice**: Append `CancellationToken cancellationToken = default` to the interface method signature.

**Alternatives considered**:
- *Separate `ICancellablePlugin` interface* — adds complexity, plugins that forget to implement it silently miss the timeout. Rejected.
- *`Task.WhenAny` with no token propagation* — timeout fires but plugin keeps running orphaned in the background, potentially corrupting state. Rejected.
- *Thread abort (deprecated in .NET Core)* — not viable. Rejected.

**Rationale**: The interface-level token is the only way to give plugins the opportunity to clean up (close COM handles, cancel P/Invoke waits). The `= default` default value makes this a source-compatible change for any future external plugins that don't use the token.

### D2: 30-Second Hard Timeout in Pipeline Layer

**Choice**: `CancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(30))` created inside `PluginExecutionPipeline.ExecuteAsync()`, linked to an incoming cancellation token.

**Rationale**: 30 seconds is generous enough for legitimate heavy operations (VBA script execution, large process launch) but tight enough to prevent indefinite hangs from degrading UX. The timeout is enforced at the pipeline boundary, so no individual plugin can opt out.

### D3: Disk-First Write Ordering for ConfigService Cache

**Choice**: In `SaveAsync()`, write to disk (with existing retry logic) first, then update `_cachedConfig` inside a `lock`. In `LoadInternalAsync()`, read `_cachedConfig` inside the same `lock`.

**Alternatives considered**:
- *`ReaderWriterLockSlim`* — overengineered for a field that is read on startup and written occasionally. Rejected.
- *`ConcurrentDictionary` wrapper* — doesn't solve the ordering problem (cache-before-disk). Rejected.

**Rationale**: A simple `lock` is correct and sufficient. The disk-first ordering ensures that if `SaveAsync` fails after updating the cache, the in-memory state is not stale relative to disk. The lock eliminates the read/write race between concurrent `LoadAsync` and `SaveAsync` calls.

### D4: Fire-and-Forget with Error Logging for Dispose Paths

**Choice**: Replace `GetAwaiter().GetResult()` with `Task.Run(() => UnloadAsync()).ContinueWith(t => { if (t.IsFaulted) _logger?.LogError(...); })` in `PluginHost.Dispose()`. Same pattern for `PluginUsageTracker.Dispose()`.

**Alternatives considered**:
- *`IAsyncDisposable`* — correct but requires all callers to change. Pulsar's DI container resolves and disposes these as transient services during shutdown; the shutdown path is synchronous. Rejected for this change.
- *`SynchronizationContext` capture via `ConfigureAwait(false)`* — addresses the deadlock risk but not the fundamental problem of blocking a thread for GC-heavy unload work. Rejected.

**Rationale**: Fire-and-forget with error logging is the least disruptive fix. The unload/save work is best-effort during dispose; losing it is non-fatal. The pattern eliminates the deadlock risk entirely.

## Risks / Trade-offs

- **[Breaking Change] All 6 plugins must be updated**: The `IPulsarPlugin.ExecuteAsync` signature change is a mechanical update — each plugin adds the parameter and passes it to any internal async calls (e.g., `Task.Delay(..., token)`). Estimated effort: ~2 lines per plugin on average.
- **[Timeout Too Short] 30s may interrupt legitimate long VBA scripts**: Mitigation — the timeout is a config-level constant that can be adjusted. Future work can add per-plugin timeout configuration via `IPluginMetadataProvider`.
- **[Fire-and-Forget Dispose] Unobserved task exceptions during shutdown**: Mitigation — `ContinueWith` error handler logs to Serilog. The app is already shutting down; lost save/unload work is acceptable.
- **[Lock Contention] `_cachedConfig` lock in ConfigService**: The lock is held only for field assignment, never across I/O. Contention is near-zero in practice.

## Migration Plan

1. Update `IPulsarPlugin` interface → all 6 plugin implementations get the new parameter (mechanical, no logic change in most)
2. Update `IPluginExecutionPipeline` and `IPluginRuntimeKernel` interfaces → update `PluginExecutionPipeline` and `PluginRuntimeKernel` to create linked CTS with timeout
3. Update `IActionStrategy` and all strategy implementations to propagate token
4. Fix `ConfigService._cachedConfig` locking
5. Fix `PluginHost.Dispose()` and `PluginUsageTracker.Dispose()`
6. Fix dead code in `PluginRuntimeKernel`
7. Build → run tests → verify no regression

Rollback: The `CancellationToken` parameter has a default value of `default`, so rolling back the pipeline changes while keeping the interface change is safe — plugins will compile with the extra unused parameter.
