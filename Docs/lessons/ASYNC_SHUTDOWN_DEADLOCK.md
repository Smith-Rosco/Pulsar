# Async Shutdown Deadlock

**Status**: Published
**Applies To**: Application shutdown, persistence services
**Last Updated**: 2026-08-16

---

## Symptom

Selecting **Exit** from the taskbar tray context menu leaves the process running
indefinitely. The log contains `=== Pulsar Application Exiting ===` but no
subsequent shutdown-phase logs, and window-activation hooks keep recording
events after the exit request.

## Root Cause

`App.OnExit` runs on the WPF Dispatcher thread. The original shutdown path
invoked async persistence methods inline:

```csharp
usageTracker.FlushAsync().GetAwaiter().GetResult();
```

`FlushAsync` awaited file I/O without `ConfigureAwait(false)`. Because it was
called on the UI thread, every continuation captured the Dispatcher
`SynchronizationContext`. The UI thread then blocked on `GetResult()`, so the
continuation could never run. This is the classic async-over-sync deadlock.

## Fix

1. Run every async shutdown phase from the thread pool:
   `Task.Run(phaseFactory).GetAwaiter().GetResult()`.
2. Use `ConfigureAwait(false)` in persistence services whose continuations never
   need WPF objects.
3. Log start/completion for each shutdown phase so future hangs are immediately
   localizable from `pulsar-*.log`.

## Regression Test

`PluginUsageTrackerTests.FlushAsync_DoesNotCaptureSynchronizationContext`
installs a non-pumping `SynchronizationContext` and verifies that flush
completes while the current thread is blocked.

---

**Change History**:
- v1.0.0 (2026-08-16): Initial version
