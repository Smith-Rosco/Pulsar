# ADR-009: Enforce the Config Snapshot Seam (Deep Copy + Optimistic Concurrency)

**Status**: Accepted (implemented 2026-08-18)
**Date**: 2026-08-18
**Deciders**: Pulsar Development Team

---

## Context

`IConfigService.GetSnapshot()` returned the live cached `ProfilesConfig`. The read-only contract was documented but not enforced: `PluginRuntimeKernel.ApplyProfileAsync` inserted a `PluginProfile` into the shared graph without persisting, `ConfigService.SetSlotsPerPage` mutated the cached object then re-saved, and `PluginViewModel.GetCurrentConfig` handed a live reference outward. `ConfigEditSession` deep-copied on begin but committed unconditionally — two concurrent sessions could silently drop each other's changes. `HotkeyService` cached config outside the `ConfigUpdated` event, so any hotkey change outside the Settings save path left the effective cache stale (the half-implemented fix from `HOTKEY_SERVICE_STALE_CONFIG_OVERWRITE.md`).

## Decision

1. **`GetSnapshot()` returns a deep copy** (JSON round-trip). Mutating the result cannot affect the shared cache — the read-only contract is enforced by behavior, not by caller discipline. `CloneConfig` rebuilds the case-insensitive Profiles dictionary so snapshot readers see the same lookup semantics as the live cache.
2. **Optimistic concurrency on commit** — `ConfigService` exposes `CurrentRevision` (a monotonically increasing write counter bumped on every save and on reset). `ConfigEditSession.BeginAsync` captures the revision; `CommitAsync` saves with the expected revision; `SaveAsync(config, expectedRevision)` throws `ConfigConcurrencyException` on mismatch instead of overwriting a newer writer. A stale commit can no longer lose data silently.
3. **`HotkeyService` subscribes to `ConfigUpdated`** and rebuilds its effective cache from the fresh snapshot. Every commit path (Settings save, `ConfigEditSession`, tutorial, future plugins) stays in sync.
4. **`PluginRuntimeKernel.ApplyProfileAsync` is read-only** — it applies the user's persisted profile (or defaults when absent) but never writes back to `Profiles.json`. Activating a plugin is not a user configuration change; the runtime no longer pollutes the single source of truth.
5. **`IConfigStore` merged into `IConfigService`** — the store was a strict subset used only to narrow `ConfigEditSession`'s parameter. With revision checking the session needs more surface anyway; the hypothetical seam (single implementation, near-identical interface) is deleted.
6. **`SetSlotsPerPage` mutates the internal cache directly** (it owns it) instead of mutating-and-resaving the snapshot, which the deep copy would have silently discarded.

## Considered Options

- **Return live object + document** — rejected: the `ApplyProfileAsync` mutation bug already demonstrated documentation does not stop code.
- **Immutable `ProfilesConfig` model** — rejected: would require rewriting every reader; the deep copy achieves the same guarantee with a smaller blast radius.
- **Check-then-save in `CommitAsync`** — rejected: non-atomic with the revision bump inside `SaveAsync`'s write lock; the guard must live in the store's save path.

## Consequences

- The stale-overwrite bug class (`HOTKEY_SERVICE_STALE_CONFIG_OVERWRITE.md`) becomes unrepresentable: no caller can mutate shared config, and a stale session cannot commit.
- Concurrent editors now get a deterministic, catchable conflict instead of silent data loss.
- `Profiles.json` is written only by user/configuration actions, never as a side effect of plugin activation.
- Two new tests cover the concurrency contract: stale-commit rejection and current-revision success.
- `ConfigEditSession.BeginAsync` still performs a JSON round-trip (now shared conceptually with `GetSnapshot` deep copy); callers that hot-loop on snapshots should cache rather than call repeatedly.

---

**Change History**:
- v1.0.0 (2026-08-18): Initial version
