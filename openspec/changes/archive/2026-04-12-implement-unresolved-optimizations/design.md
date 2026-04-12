## Context

Pulsar already distinguishes blocking startup from deferred warm-up, but the boundary is still coarse. `AppStartupCoordinator` directly invokes deferred work on the dispatcher, `ConfigService` and other services still launch untracked `Task.Run` work, and plugin runtime responsibilities remain concentrated in `PluginRegistry` and `PluginLoader`. The codebase also captures `process.MainModule.FileName` during synchronous `PulsarContext.Capture`, which adds unnecessary cost to menu invocation.

The change should improve startup and steady-state behavior without breaking the existing plugin model, profile format, or user-visible startup semantics. Existing saved plugin slots must continue to execute, and any new runtime layering must fit the current modular-monolith DI pattern.

## Goals / Non-Goals

**Goals:**
- Move deferred startup execution behind an explicit scheduler instead of ad hoc dispatcher work.
- Separate plugin descriptor discovery from live plugin activation so extension plugins are instantiated only when needed.
- Allow descriptor discovery and plugin metadata availability without forcing live plugin residency for every extension plugin.
- Remove heavyweight executable-path lookup from the synchronous context capture path while preserving on-demand access for plugins that need it.
- Provide observable, cancellable infrastructure for background startup and post-startup work.

**Non-Goals:**
- Rebuild the full plugin system into every long-term interface proposed in the optimization review.
- Introduce source generators or manifest-based plugin discovery in this change.
- Rewrite `RadialMenuViewModel`, `SettingsViewModel`, or the dialog system as part of this optimization phase.
- Change persisted `Profiles.json` structure beyond what is necessary to support runtime behavior.

## Decisions

### 1. Add a dedicated startup/background scheduler

Pulsar will introduce lightweight scheduler services for deferred startup and background work, registered through DI and used by `AppStartupCoordinator` and services that currently fire-and-forget tasks.

Rationale:
- Centralizes task lifetime, logging, and cancellation.
- Preserves the current staged startup behavior while making deferred work explicit and testable.
- Avoids growing more direct `Task.Run` usage across services.

Alternatives considered:
- Keep using direct `Dispatcher.InvokeAsync` and `Task.Run`: rejected because failures, cancellation, and deduplication remain fragmented.
- Introduce a heavy job framework: rejected because the app only needs a small in-process scheduler.

### 2. Split plugin runtime into catalog plus activation path without a full multi-service rewrite

This change will keep `PluginRegistry` as the main integration surface for callers, but internally it will rely on a descriptor catalog path and an activation path so descriptors can be discovered and cached without creating plugin instances.

Rationale:
- Delivers the main memory and startup benefits now.
- Keeps the implementation smaller than a full interface-by-interface registry rewrite.
- Preserves current consumers while enabling future extraction into dedicated services later.

Alternatives considered:
- Full `IPluginCatalog`/`IPluginActivator`/`IPluginExecutor` split now: rejected for this phase because it is larger than needed to unlock the first performance wins.
- Leave the current `PluginRegistry` design unchanged: rejected because eager runtime residency continues to block meaningful startup improvement.

### 3. Keep runtime reflection discovery for now, but cache descriptor results in-process

`PluginLoader` will continue using the current discovery mechanism for builtin and external plugins, but discovery results will be cached and reused within the app lifetime so deferred scans do not repeatedly rescan the same assemblies and directories.

Rationale:
- Avoids a larger manifest/source-generator project while still reducing repeated discovery cost.
- Fits the current plugin packaging model.

Alternatives considered:
- Build-time manifests now: rejected because it requires a broader packaging and compatibility change.
- No caching: rejected because startup and deferred discovery continue to repeat expensive reflection work.

### 4. Make target executable path lazy in `PulsarContext`

`PulsarContext` will synchronously capture only window handle, process id, and process name. Executable path resolution will move to a lazy async accessor that resolves on first demand and handles access failures safely.

Rationale:
- Reduces synchronous cost when the radial menu opens.
- Preserves functionality for plugins that truly need the executable path.

Alternatives considered:
- Remove executable path entirely: rejected because some plugin flows still need it.
- Keep eager path capture: rejected because it keeps heavyweight process access on the hot path.

## Risks / Trade-offs

- [Risk] Deferred startup work may execute later than some secondary features expect -> Mitigation: define explicit startup priorities and ensure only readiness-critical dependencies remain on the blocking path.
- [Risk] Lazy plugin activation can surface activation failures later, during first use instead of startup -> Mitigation: keep clear activation logging, preserve circuit-breaker behavior, and add tests around first-use activation.
- [Risk] Background scheduler adoption may miss some existing fire-and-forget paths -> Mitigation: audit current `Task.Run` usages and migrate startup/configuration-related cases in this change.
- [Risk] Plugins may assume `TargetExePath` is immediately available as a string -> Mitigation: keep compatibility through a safe accessor strategy and update affected call sites in the same change.

## Migration Plan

1. Introduce the scheduler abstractions and wire `AppStartupCoordinator` deferred work through them.
2. Refactor plugin runtime so descriptor discovery is cached and plugin activation remains on demand.
3. Move startup/configuration fire-and-forget work onto the scheduler.
4. Change `PulsarContext` to defer executable path resolution and update dependent code.
5. Validate startup, plugin execution, and simulator/test flows.

Rollback strategy:
- Revert the scheduler integration and restore the previous direct deferred startup path.
- Restore eager executable-path capture if plugin compatibility issues appear.
- Keep plugin registry public surface stable so rollback stays localized to runtime internals.

## Open Questions

- Whether `TargetExePath` should become an async API only, or retain a compatibility shim backed by lazy resolution.
- Which existing startup tasks beyond config discovery and onboarding should be explicitly scheduled in this first optimization phase.
- Whether in-process descriptor caching is sufficient for external plugins, or if a persisted cache should be added in a later change.
