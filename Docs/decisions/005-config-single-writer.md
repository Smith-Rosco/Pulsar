# ADR-005: Config Persistence Uses a Single Writer and Injectable Paths

**Status**: Accepted
**Date**: 2026-08-16
**Deciders**: Pulsar Development Team

---

## Context

`ConfigService` exposed the in-memory `ProfilesConfig` through `Current` and allowed any component to mutate it without a commit boundary. `SaveAsync` also used a fixed `Profiles.json.tmp` name without serializing writers. Concurrent saves could collide on the temp file, and a stale in-memory reference could overwrite a newer save. Several tests worked around the hard-coded `%AppData%` path by using reflection to replace a private field.

## Decision

1. `ConfigService.SaveAsync` is a single-writer operation guarded by a `SemaphoreSlim`.
2. Every save uses a unique temp file (`Profiles.json.<guid>.tmp`) followed by `File.Move(..., overwrite: true)`.
3. The in-memory cache update is revision-checked: a force reload that started before a concurrent save must not overwrite the newer cached value.
4. `ConfigUpdated` is raised outside the write lock so subscribers may synchronously trigger another save without deadlocking.
5. `ConfigService` and `PluginUsageTracker` accept an optional filesystem path for deterministic tests. Production defaults remain `%AppData%\Pulsar`.
6. `HotkeyService.ApplyHotkey` updates only the effective hotkey cache. It never mutates `IConfigService.Current`; persistence remains the responsibility of the settings editor commit path.

## Consequences

- Concurrent config saves are serialized and cannot corrupt each other's temp files.
- Test suites can use isolated temp directories instead of the real AppData profile.
- `Current` still returns a mutable object for backward compatibility; the long-term move to immutable snapshots and an edit-session commit API is tracked separately.

---

**Change History**:
- v1.0.0 (2026-08-16): Initial version
