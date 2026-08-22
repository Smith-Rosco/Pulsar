# ADR-006: Plugin Runtime Execution Hardening

**Status**: Accepted
**Date**: 2026-08-16
**Deciders**: Pulsar Development Team

---

## Context

The plugin runtime documented several guarantees that were only partially implemented:

- "3 crashes within 1 minute" was implemented as an unbounded failure counter.
- Core plugin crashes were documented as fatal but were caught and converted into ordinary error results.
- Concurrent activation could create duplicate plugin instances.
- Nested `PluginExecutionContext` scopes destroyed the outer scope on dispose.
- External packages were installed under `%AppData%\Pulsar\Plugins` but the runtime scanned `BaseDirectory\Plugins`.

## Decision

1. **Single-instance activation** — `PluginRuntimeKernel.GetOrActivatePluginAsync` uses a per-plugin `SemaphoreSlim` gate.
2. **Default serial execution** — `PluginExecutionPipeline` allows one action per plugin at a time. A concurrent request receives `PluginErrorCode.TemporaryUnavailable` (`Blocked`).
3. **Sliding-window breaker** — failure timestamps are retained for one minute; only three failures inside that window open the circuit.
4. **Core fail-fast** — unhandled Core plugin exceptions and execution timeouts flow through `ICorePluginFailureHandler`. The WPF shell registers `AppShutdownCorePluginFailureHandler`, which shuts the application down. Handled `PluginResult` Critical outcomes are not fatal.
5. **Stack-scoped execution context** — `PluginExecutionContext.Dispose` restores the previous AsyncLocal scope.
6. **Lifecycle validation** — `PluginRuntimeStateStore.Transition` rejects illegal state transitions.
7. **External plugin directory unification** — package install, manifest scanning, and runtime discovery all use `%AppData%\Pulsar\Plugins`.
8. **External manifests are mandatory** — the loader skips external folders without a valid `plugin.manifest.json` or `manifest.json`, validates host-version compatibility, and verifies that the discovered plugin ID matches the manifest ID.
9. **Path traversal protection** — `PluginPackageManager` resolves every install/uninstall path against the plugin store root and rejects escapes.

## Consequences

- Extension plugin failures no longer accumulate across arbitrary time periods.
- A core infrastructure failure is visible and terminates the app as specified.
- Malformed or version-incompatible external packages fail closed at discovery time.
- Plugins that rely on concurrent reentrant execution need a future explicit concurrency capability; the current default is serial.

---

**Change History**:
- v1.0.0 (2026-08-16): Initial version
