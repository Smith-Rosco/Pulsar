# ADR-016: Keep the Runtime State Store Deep — Make Reads Pure Instead of Splitting It

**Status**: Accepted (2026-09-04)
**Date**: 2026-09-04
**Deciders**: Pulsar Development Team
**Related**: architecture review 2026-09-04 (candidate G, "speculative"); ADR-012 (narrow runtime seams — same review); ADR-013 (circuit breaker observation seam — same review)
**Implementation**: `Core/Plugin/Runtime/PluginRuntimeKernel.cs` (`PluginRuntimeStateStore.GetSnapshot`), tests in `Pulsar.Tests/Plugin/PluginRuntimeHardeningTests.cs`

---

## Context

The architecture review (candidate G, speculative) claimed that `PluginRuntimeStateStore` holds two dictionaries — `_plugins` (live `IPulsarPlugin` instances) and `_snapshots` (per-plugin `PluginRuntimeSnapshot` lifecycle records) — and that the coupling between them forces manual synchronization, with read operations carrying write side effects:

- `GetSnapshot()` internally calls `_snapshots.TryAdd(...)` to materialize a fallback snapshot on a cache miss.
- `Transition()` reads the current snapshot and overwrites it.
- Instance registration and lifecycle state must be kept in sync by hand.

The review's suggested "After" shape was to split the store into two modules: a `PluginRegistry` (instance dictionary, add/remove only) and a `LifecycleStateMachine` (pure state transitions, single authority).

### Verification result

Reading the actual code (`Pulsar/Pulsar/Core/Plugin/Runtime/PluginRuntimeKernel.cs`, `PluginRuntimeStateStore`, lines 100-187) confirms the claims with one stronger finding:

1. **Dual dictionaries are real** — `_plugins` and `_snapshots` both live in `PluginRuntimeStateStore`.
2. **Reads carry a write side effect** — `GetSnapshot()` on a miss built a default snapshot (`Loaded` when an instance is registered, else `Unloaded`) and **inserted it** into `_snapshots` via `TryAdd`.
3. **Stronger than reported: rejected transitions left a trace.** `Transition()` began with `GetSnapshot(pluginId)` — so transitioning an *unknown* plugin into an illegal state (e.g. `Transition("missing.plugin", Running)`) first *materialized a `Unloaded` snapshot* and only then threw `InvalidOperationException`. A failed write polluted state.
4. **Manual sync is real but encapsulated.** `SetPlugin` writes `_plugins` then `Transition`s; `RemovePlugin` removes the instance then transitions to `Unloaded`. Both dictionaries are private; the two-step sync lives inside the store, not at call sites.

Every consumer (kernel, execution pipeline, 7+ test files, DI registration in `PluginRuntimeServiceCollectionExtensions`) reaches the store through its 6-method public API; no external code touches either dictionary directly.

### Why not the proposed split

`PluginRuntimeStateStore` is a textbook **deep module**: a small, stable interface (`GetState`, `GetSnapshot`, `SetPlugin`, `Transition`, `TryGetPlugin`, `RemovePlugin`, `Plugins`) hiding a nontrivial invariant — *a registered instance and its lifecycle record must change together*. Splitting it into two public classes would:

- **Export the coordination problem.** `SetPlugin`'s "write instance + transition state" atomicity would become the caller's job, or require a third orchestrating module. Call sites in the kernel (activation, enable/disable, breaker, shutdown) and a dozen test constructors would each need to remember the pairing.
- **Widen the interface surface** (two modules × their methods) with no new behavior.
- **Churn 7+ test files and DI wiring** for a pure structural rearrangement.

The review's *symptom* was real; its *prescription* (split) treats a shallow-module smell that the store does not actually have. The correct fix keeps the deep module and removes the side effect.

## Decision

1. **`GetSnapshot` becomes a pure read.** On a cache miss it returns a *derived* fallback snapshot (`Loaded` when the plugin has a registered instance, else `Unloaded`) **without inserting it** into `_snapshots`. The snapshot dictionary is written only by a validated `Transition()`.
2. **Rejected transitions no longer leave a trace.** Because `Transition()` obtains its "current" snapshot through the now-pure `GetSnapshot`, transitioning an unknown/illegal plugin throws without first materializing a default record.
3. **The store stays a single deep module.** Instance registry and lifecycle state remain co-located so that paired updates (`SetPlugin`, `RemovePlugin`) keep their invariant inside one encapsulation boundary.
4. **Behavior is unchanged for all legal flows.** Registered plugins already have a snapshot (every `SetPlugin` transitions), so the pure-read fallback path only ever fires for unknown ids — where callers already treated the derived `Unloaded`/`Loaded` result as the answer.

## Considered Options

- **Split into `PluginRegistry` + `LifecycleStateMachine`** (the review's "After") — rejected. It exports the instance/state pairing invariant to callers, doubles the interface surface, and rewrites kernel wiring + 7 test files for zero behavioral gain. The store already is the deep module the split was meant to create.
- **Make `GetSnapshot` throw for unknown plugins** — rejected. Callers legitimately observe a not-yet-registered plugin as `Unloaded` (e.g. `GetState` used to decide default enablement); making the read throw would force try/catch at every call site for no safety benefit.
- **Remove the snapshot entry on `RemovePlugin`** (drop to zero state for uninstalled plugins) — rejected as scope creep. Terminal `Unloaded` snapshots keep unload timestamps, which the kernel's shutdown/audit paths and existing hardening tests rely on. Candidate G did not claim this.
- **Do nothing** — rejected: the read-with-write-side-effect and failed-write-pollutes-state defects are real, cheap to remove (~4 lines), and their removal makes `_snapshots` contents exactly "validated lifecycle history" — a much easier invariant to reason about and test.

## Consequences

- `_snapshots` contains **only records written by validated transitions**. Its size no longer grows from query traffic or failed transitions; a plugin that was only ever *asked about* never appears.
- Reads are side-effect free and re-entrant safe; `GetSnapshot`/`GetState` can be called freely from telemetry, logging, or concurrent queries without mutating shared state.
- Locked by two new tests in `PluginRuntimeHardeningTests`: reads do not materialize snapshots, and a rejected transition leaves no snapshot behind.
- Full suite green at 0 warnings / 0 errors.

---

**Change History**:
- v1.0.0 (2026-09-04): Initial version — verifies architecture-review candidate G (dual-dictionary state store, read side effects), rejects the proposed module split as shallow-module churn, and instead makes `PluginRuntimeStateStore.GetSnapshot` a pure read so only validated transitions write lifecycle state.
