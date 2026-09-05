---
type: architecture concept
title: Configuration & Persistence System
description: How Profiles.json acts as the single source of truth — the ProfilesConfig model, deep-copy snapshot reads, revision-guarded ConfigEditSession writes, the validation pipeline, and versioned ZIP backup/restore with portable password-sealed secrets.
tags: [configuration, persistence, profiles.json, concurrency, backup, validation, secrets]
verified:
  - by: openwiki/0.5.0
    at: 2026-09-05T05:46:24.085Z
sources:
  - id: openwiki-source-529acfecd446f581590f0431
    resource: repo://Docs/decisions/005-config-single-writer.md
  - id: openwiki-source-050bd1106ddab18e5bef6c0a
    resource: repo://Docs/decisions/009-config-snapshot-seam.md
  - id: openwiki-source-3bb776e1d0560b54db002434
    resource: repo://Pulsar/Pulsar.Tests/Config/ConfigEditSessionTests.cs
  - id: openwiki-source-36f585d13dc6dc078aefe8f1
    resource: repo://Pulsar/Pulsar.Tests/Config/ConfigServiceConcurrencyTests.cs
  - id: openwiki-source-70fb2fc50ec231aae557400c
    resource: repo://Pulsar/Pulsar.Tests/Config/ConfigServiceLoadTests.cs
  - id: openwiki-source-07309818a3c0b1d795a20462
    resource: repo://Pulsar/Pulsar.Tests/Services/ConfigBackupServiceTests.cs
  - id: openwiki-source-72c8eb4e7c12f074f41c22b0
    resource: repo://Pulsar/Pulsar/Core/Plugin/Runtime/PluginRuntimeKernel.cs
  - id: openwiki-source-decb7f017ef4907d4a07025f
    resource: repo://Pulsar/Pulsar/Models/ConfigBackupModels.cs
  - id: openwiki-source-0ed24cd15d922a8606e36508
    resource: repo://Pulsar/Pulsar/Models/InstalledPresetPack.cs
  - id: openwiki-source-518d8749f2742b670216c408
    resource: repo://Pulsar/Pulsar/Models/ProfilesConfig.cs
  - id: openwiki-source-919c86fc2517614071ce6c73
    resource: repo://Pulsar/Pulsar/Plugins/Core/Pki/Services/CredentialsManager.cs
  - id: openwiki-source-72e33040550b349789a6d47b
    resource: repo://Pulsar/Pulsar/Services/AppStartupCoordinator.cs
  - id: openwiki-source-508925bbe84f143f3d2a3721
    resource: repo://Pulsar/Pulsar/Services/ConfigBackupService.cs
  - id: openwiki-source-7e574f5c2086ce914c66fa80
    resource: repo://Pulsar/Pulsar/Services/ConfigConcurrencyException.cs
  - id: openwiki-source-c640d149a347994c32ccf705
    resource: repo://Pulsar/Pulsar/Services/ConfigEditSession.cs
  - id: openwiki-source-840ea3f3954ba24f259ff1e6
    resource: repo://Pulsar/Pulsar/Services/ConfigService.cs
  - id: openwiki-source-90979b97248aca783bd898b9
    resource: repo://Pulsar/Pulsar/Services/HotkeyService.cs
  - id: openwiki-source-5bc994c5ebe1bde2086f4cf4
    resource: repo://Pulsar/Pulsar/Services/Interfaces/IConfigService.cs
  - id: openwiki-source-1dca30bdf62346b0f4682216
    resource: repo://Pulsar/Pulsar/Services/Validation/ConfigValidationPipeline.cs
  - id: openwiki-source-c20d4835bdf354f19d3547f8
    resource: repo://Pulsar/Pulsar/ViewModels/Settings/SettingsEditorSession.cs
generated: { by: "openwiki/0.5.0", at: "2026-09-05T05:46:24.085Z" }
---

# Configuration & Persistence System

Pulsar persists all user and plugin configuration in a single JSON file, `Profiles.json`, living at `%AppData%\Pulsar\Profiles.json` (or an injected path in tests and ui-debug mode). Every read goes through a deep-copy snapshot; every write goes through a revision-guarded edit session. The configuration system therefore provides four properties the rest of the app depends on:

1. **Single source of truth** — the persisted `Profiles.json` is the only durable representation of settings, plugin profiles, process profiles, and preset-pack install state.
2. **Read-only by behavior** — `GetSnapshot()` returns a JSON round-trip deep copy, so no caller can mutate shared state by accident.
3. **No lost updates** — commits carry an optimistic-concurrency revision; a stale commit is rejected and rebased, never silently overwritten.
4. **Crash-safe persistence** — saves are atomic (unique temp file + `File.Move` overwrite), keep a rolling `.bak`, and validation blocks invalid writes.

## Configuration Model (`ProfilesConfig`)

The root object `Pulsar.Models.ProfilesConfig` (in `Pulsar/Pulsar/Models/ProfilesConfig.cs`) has four top-level regions:

| Region | Type | Meaning |
|---|---|---|
| `Settings` | `ProfileSettings` | Global app settings: theme, radial-menu geometry, slots-per-page, global hotkeys, right-drag gesture options, input mode, logging, tutorial/onboarding state, `ConfigCreatedAt` |
| `Plugins` | `Dictionary<string, PluginProfile>` (case-insensitive) | Per-plugin profile: `Enabled`, a free-form `Config` key-value store, and `GrantedPermissions` approved by the user for external plugins |
| `Profiles` | `Dictionary<string, ProcessProfile>` (case-insensitive) | Per-process profile: `CommandMode` and `SwitchMode` slot lists (`PluginSlot`), optional alias/icon |
| `InstalledPresetPacks` | `List<InstalledPresetPack>` | Office-action preset pack install state: pack id + version + granted permission tokens + the exact CommandMode slot numbers the pack appended (used for precise uninstall) |

Key model-level facts:

- The `Profiles` and `Plugins` dictionaries are **case-insensitive** (`StringComparer.OrdinalIgnoreCase`). `System.Text.Json` deserializes dictionaries as case-sensitive by default, so `ConfigService` and the backup service rebuild them with the comparer after every load — this is what makes `Profiles["Global"]` and `Profiles["global"]` hit the same profile.
- `PluginProfile.Config` is `Dictionary<string, object>`. JSON round-trips materialize values as `JsonElement`, so both load and save paths run `NormalizeConfigDictionary` to convert `JsonElement` values to concrete types (`string`/`int`/`long`/`double`/`bool`, arrays and objects to their JSON string form).
- `PluginSlot` is an `ObservableObject` whose persisted fields are `plugin`, `action`, `args`, `label`, `icon`, `color`, `slot` (the only ordering key; the legacy `order` field is `[Obsolete]` and kept only for migration), plus optional `subActions` and `layoutStyle` that are omitted from JSON when null so legacy files stay byte-compatible. UI-only fields (`AvailableActions`, `ValidationSeverity`, presentation) are `[JsonIgnore]`.
- `Settings` carries onboarding/tutorial lifecycle fields whose combinations are checked by `ProfileSettings.ValidateOnboardingInvariants` (non-blocking: it logs warnings for illegal combinations such as `OnboardingState="Complete"` with `HasCompletedTutorial=false`). Services that write narrow changes MUST preserve these fields.
- `ConfigCreatedAt` is set only when a new config is generated; narrow writes must never overwrite it.

## The Read Seam: Deep-Copy Snapshots

`IConfigService.GetSnapshot()` (implemented by `ConfigService`) returns `CloneConfig(_cachedConfig ??= CreateDefaultConfig())` under `_cacheLock`. `CloneConfig` serializes to JSON and deserializes a fresh `ProfilesConfig`, then rebuilds the case-insensitive `Profiles` dictionary so snapshot readers see the same lookup semantics as the live cache. Mutating the returned object cannot affect the shared cache — the "read-only" contract is **enforced by behavior, not caller discipline** (ADR-009).

`LoadSnapshotAsync(forceReload)` returns the cached object (same reference) when not forcing — this is the one path that yields the live object, so it must only be used by code that does not mutate. `HotkeyService` caches from `LoadSnapshotAsync` and rebuilds its effective hotkey cache on every `ConfigUpdated` event, so every commit path stays in sync.

## The Write Seam: `ConfigEditSession`

All persisted changes go through `ConfigEditSession` (ADR-005/009). There are no other commit paths in app code; plugin activation and other runtime side effects are explicitly read-only (`PluginRuntimeKernel.ApplyProfileAsync` applies the persisted profile but never writes back to `Profiles.json`).

### Session lifecycle

- `BeginAsync(store)` — loads a snapshot, deep-clones it twice: once as the editable `Draft`, once as the `_base` baseline used to detect untouched regions. Captures `store.CurrentRevision` as `_revisionAtBegin`.
- Mutate `Draft` via helpers: `UpdateSettings`, `UpdatePluginProfile` (creates the profile if missing, preserving existing values), `EnsureProcessProfileAsync` (only seeds missing profiles — an existing profile is left untouched so "ensure" operations never clobber configured slots), and `ReplaceAll` (whole-config bootstrap, e.g. the first-launch wizard starting from a fresh template).
- `CommitAsync()` — saves the draft with the captured revision. If `SaveAsync` throws `ConfigConcurrencyException`, it rebases and retries exactly once.
- `RunAsync(store, mutate)` — one-shot convenience: begin → mutate → skip commit when the draft is JSON-equal to the baseline (so "ensure" operations never produce redundant writes) → commit.

After a successful commit the session **re-arms** to the store's new revision, which is what lets a long-lived editor (the Settings window, via `SettingsEditorSession`) save repeatedly without failing its own prior commits.

### Rebase semantics

On a concurrency conflict, `RebaseAsync` reloads the current snapshot and folds the concurrent writer's changes into the **untouched regions** of the draft:

- `Settings` is replaced if the session never changed it (compared against `_base`).
- `InstalledPresetPacks` is a root-level region treated like `Settings` — a preset-pack install that landed while an edit was in flight is preserved instead of silently dropped.
- Each `Profiles` and `Plugins` entry is replaced when the draft still equals the baseline for that key (or when the key is absent from the draft).

Regions the user actually edited keep the user's version; the revision is re-armed and the commit retried once.

## The Write Path (end to end)

<!-- openwiki: mermaid parse failed and this diagram was converted to a text fence so it does not break rendering. Fix the diagram source and restore the mermaid fence. Parser error: Heuristic: a semicolon inside a label breaks rendering; rephrase the label. -->
```text
flowchart TD
    Caller["Caller: SettingsEditorSession, PresetInstallService, PluginRuntimeKernel, tutorial, wizard"] --> Begin["ConfigEditSession.BeginAsync: snapshot + deep-clone draft + capture revision"]
    Begin --> Mutate["Mutate Draft via UpdateSettings / UpdatePluginProfile / EnsureProcessProfile / ReplaceAll"]
    Mutate --> Changed{"Draft unchanged vs baseline?"}
    Changed -->|yes| Skip["Skip commit: no redundant write"]
    Changed -->|no| Commit["CommitAsync: SaveAsync Draft with expected revision"]
    Commit --> RevCheck{"Revision still current? checked inside write lock"}
    RevCheck -->|no| Rebase["RebaseAsync: fold concurrent writer changes into untouched regions, re-arm revision"]
    Rebase --> Commit2["Retry SaveAsync once"]
    RevCheck -->|yes| Save["ConfigService.SaveAsync under SemaphoreSlim"]
    Save --> Normalize["Normalize plugin config values + switch launch paths"]
    Normalize --> Validate["ConfigValidationPipeline.ValidateAsync: schema, plugin custom, slot args, dependencies, hotkeys"]
    Validate --> Blocked{"Validation errors?"}
    Blocked -->|yes| Throw["Throw InvalidOperationException: save blocked"]
    Blocked -->|no| Persist["Write unique temp file + File.Move overwrite + TryWriteBackup .bak"]
    Persist --> Cache["Update in-memory cache, bump CurrentRevision"]
    Cache --> Event["Raise ConfigUpdated outside the write lock"]
    Event --> Subscribers["Subscribers: HotkeyService rebuilds effective cache; others may save again without deadlock"]
```

Caption: the revision-guarded write path from `ConfigEditSession` through `ConfigService`, with validation and the backup hook.

### `ConfigService.SaveAsync` details

- Guarded by a `SemaphoreSlim(1,1)` single-writer lock; the optimistic-concurrency check (`expectedRevision` vs `CurrentRevision`) runs **inside** the write lock, so check and revision bump are atomic — two sessions started from the same revision can never both commit.
- Writes to a unique temp file (`Profiles.json.<guid>.tmp`) then `File.Move(tempPath, _configPath, overwrite: true)` — an atomic replace, with up to 3 retries (100 ms apart) on `IOException` and best-effort temp cleanup in a `finally`.
- After each successful save, `TryWriteBackup` copies the new file to `Profiles.json.bak` (best-effort; a failed backup must never fail the save).
- The in-memory cache is replaced and `CurrentRevision` incremented while holding `_cacheLock`, then `ConfigUpdated` is raised **outside** the write lock — a subscriber may synchronously trigger another save without deadlocking (ADR-005, covered by a dedicated test).
- The save path re-runs `NormalizeConfigDictionary` and `NormalizeSwitchLaunchPaths` (relative `com.pulsar.winswitcher` switch launch paths are resolved to absolute paths idempotently) so broken values never reach disk.

## Revision and Conflict Semantics

- `CurrentRevision` is a monotonically increasing `long`, bumped on every successful save and on `ResetToFirstLaunchAsync` (which also invalidates any in-flight session, since its revision no longer corresponds to a config that survived the reset).
- A stale commit surfaces as `ConfigConcurrencyException : InvalidOperationException` from `ConfigService.SaveAsync`; the caller decides how to reconcile. `ConfigEditSession` chooses rebase-and-retry; a caller committing directly through `SaveAsync` with a stale revision gets the exception.
- `ConfigEditSession.CommitAsync` retries exactly once after a rebase. If the retry fails again, the exception propagates.
- Because `SaveAsync` also rejects invalid configs with `InvalidOperationException` (validation), `ConfigBackupService.ImportAsync` treats that as a failed import and rolls back staged secrets.

## Load, Migration, and Recovery

`LoadInternalAsync` performs the first-load and recovery logic:

1. If the file is missing and this is **not** an explicit reset reload, `TryRestoreBackup` copies `Profiles.json.bak` back first — a missing file is treated as external loss/corruption, never as a factory reset (a bare first-launch would destroy settings and re-trigger the wizard).
2. A genuine first launch creates the default config (`CreateDefaultConfig` → `CreateFallbackConfig`: light theme, `Global` profile with Notepad/Explorer/Calculator switch slots and a Command Prompt command slot, onboarding `NotStarted`) and persists it immediately.
3. Reads capture `revisionBeforeRead` and only replace the cache if the revision did not change during the read — a concurrent successful save invalidates the stale read instead of letting old disk data overwrite the newer cache.
4. Load-time normalization: case-insensitive dictionaries, `JsonElement` → concrete types in plugin configs, missing `slot.Action` filled from plugin metadata, relative switch launch paths resolved to absolute (persisted once so the file heals on disk).
5. If the file parse fails, the last successfully loaded cache is preserved in memory (no fallback overwrite on disk); only when no cache exists is an in-memory fallback created without saving.

`ResetToFirstLaunchAsync` clears the cache, increments the revision, deletes `Profiles.json` **and** the rolling `.bak` (otherwise the next launch would resurrect the pre-reset config), then reloads through the first-launch path. The Settings view model additionally copies a `.bak` snapshot before resetting.

`ScheduleSmartDetection` runs background app detection once after first launch, applying results only over known fallback slot signatures, preserving onboarding/tutorial fields, and refusing to persist when the config was loaded from a failed-read fallback.

## Validation Pipeline

`ConfigValidationPipeline` (registered as a singleton; injected into `ConfigService` after startup by `AppStartupCoordinator.ConfigureValidationPipeline`, because the service is constructed before the plugin registry exists) runs five stages in order and produces a `ValidationResult` (errors, warnings, infos):

1. **Schema validation** — plugin profiles are checked against the plugin's metadata `ConfigSchema`: required properties, unknown properties, type checks (`string`/`int`/`bool`/`enum`/`object`/`multiselect`), and custom `ValidationRule` validators.
2. **Plugin custom validation** — each plugin implementing `IPluginConfigurable.ValidateSettings` runs its own validation; exceptions are caught and recorded as errors.
3. **Slot argument validation** — every `PluginSlot` in every profile's `SwitchMode`/`CommandMode` is checked against the action's parameter metadata: required parameters, type matching (`guid`/`int`/`bool`), and per-parameter validators, using alias fallbacks.
4. **Dependency check** — an enabled plugin that depends on a disabled plugin is an error.
5. **Hotkey validation** — duplicate normalized hotkey signatures across actions are warnings.

On load, validation failures are logged but do not block loading. On save, any error **blocks the save** (`InvalidOperationException` with the joined error messages); warnings are logged and saved. Validation failures inside `SaveAsync` are re-thrown, but an exception *thrown by the pipeline itself* is logged and the save continues.

## Backup & Restore: Versioned ZIP with Portable Secrets

`ConfigBackupService` (singleton `IConfigBackupService`) exports/imports the whole configuration as a versioned ZIP. Format version 1 package layout:

```
manifest.json              — format version, app version, creation time, ContainsSecrets, SecretsProtected, KDF metadata (algorithm, iterations, salt)
Profiles.json              — the full config snapshot (same shape as the live file)
secrets.json               — raw secret-store shape, ONLY when the package is NOT password protected
secrets.protected.json     — per-secret AES-256-GCM sealed blobs, ONLY when it IS password protected
```

### Why password protection exists

Live secrets (`secrets.json` beside `Profiles.json` in `%AppData%\Pulsar`) are DPAPI-sealed per blob by `CredentialsManager` with `DataProtectionScope.CurrentUser` — decryptable only by the same Windows user on the same machine. A raw backup is therefore bound to that machine+user. With a password:

- **Export** decrypts each blob via the local DPAPI protector, derives a key with PBKDF2-SHA256 (210,000 iterations, random 16-byte salt) and re-seals each secret with AES-256-GCM (random 12-byte nonce, 16-byte tag). Only the ciphertext is protected — `Label`/`Account` stay plaintext, exactly like the live store. KDF metadata (salt + iteration count) is written into the manifest; the password itself is never stored.
- **Import** derives the same key from the password + manifest salt (iteration count floored at 10,000), unseals each secret, and **re-seals with the target machine's DPAPI protector**, making the package portable across machines/users. A wrong password or tampered ciphertext surfaces as `ConfigBackupError.WrongPassword` / `InvalidSecrets` and leaves the target untouched.

### Import semantics

- Validation is defensive: missing manifest → `InvalidPackage`; `FormatVersion` newer than the current one → `UnsupportedVersion`; unparseable `Profiles.json` → `InvalidConfig`; malformed secrets entry → `InvalidSecrets`. All of these fail before anything is written.
- Import is **replace-all** for `Profiles.json` (via `_configService.SaveAsync(config, expectedRevision: null)`), and only touches the secret store when the package contains secrets — a package without secrets leaves current secrets untouched.
- The pre-import secret map is staged so a failed config commit (e.g. validation failure) rolls the secret store back.
- `InspectAsync` reads a package without applying it (for confirmation dialogs and password prompts), returning a `ConfigBackupSummary` (profile count, slot count, secret count, protected flag, creation time, source app version).
- ZIP writing is itself atomic: temp file + `File.Move` overwrite, with best-effort cleanup.

## Lifecycle and Wiring

- `ConfigService` is registered in `App.xaml.cs` with a `configPath` override in ui-debug mode, redirecting `Profiles.json` to the isolated debug directory so a debug run never touches production config.
- `ConfigEditSession` is not a service — callers construct it over the injected `IConfigService`. Its main consumers: `SettingsEditorSession` (the Settings window's persistence seam: begin/lazy-begin/commit plus the secret-store pipeline), `PresetInstallService`, `PluginRuntimeKernel` (permission grants, enable/disable), `ProcessRegistryService` (blacklist sync), `TutorialService`/`TutorialOrchestrator`/`OnboardingState` (tutorial progress and skip flags), `FirstLaunchSetupWizardViewModel`, `WindowInspectorViewModel`, and `CreateProfileStrategy`.
- `SettingsEditorSession.CommitAsync` also saves the merged secret store (`SecretRepository` → `secrets.json`, with IO retry) before committing the config draft — the Settings window commits config and secrets as one logical save.
- The secret stack is registered in `AddPluginFoundation`: `ISecretProtector` → `CredentialsManager` (DPAPI) and `IPkiSecretStore` → `SecretRepository` (`secrets.json` in the same AppData folder, read/write with 3 IO retries).
- `AppStartupCoordinator` wires the validation pipeline into the concrete `ConfigService` during deferred startup (after plugin discovery), and `AboutViewModel` is the UI entry point for backup/restore via `IConfigBackupService`.

## Failure Modes and Invariants

- **Lost updates are unrepresentable.** No caller can mutate shared config (deep-copy snapshots), and a stale session cannot commit silently (revision guard inside the write lock; ADR-009). A stale commit is either rebased-and-retried by `ConfigEditSession` or surfaces as `ConfigConcurrencyException` to a direct `SaveAsync` caller.
- **A missing config file is never silently factory-reset.** Recovery prefers the rolling `.bak`; only a genuine first launch (no file, no backup) generates defaults. An explicit reset deletes both file and backup so the pre-reset config cannot resurrect.
- **A failed parse preserves the last good cache** in memory and never overwrites the on-disk file with a fallback.
- **A failed backup must not fail the save** (`TryWriteBackup` is best-effort); a leftover unique temp file is harmless because names are never reused.
- **Validation errors block persistence**; warnings do not. On import, a rejected config rolls back already-written secrets.
- **Reset invalidates in-flight sessions** by bumping the revision, so a session begun before the reset cannot commit into the regenerated config.

## Focused Tests

The concurrency and persistence contracts are covered by dedicated suites under `Pulsar/Pulsar.Tests`:

- `Config/ConfigEditSessionTests.cs` — mutations are isolated until commit; stale-revision commits rebase and retry once, preserving both the user's edited region and the concurrent writer's changes; `EnsureProcessProfileAsync` never clobbers existing profiles; `RunAsync` skips the commit for unchanged drafts; `ReplaceAll` works from a template.
- `Config/ConfigServiceConcurrencyTests.cs` — injected config path is honored; 12 concurrent writers neither fail nor leave temp files; a `ConfigUpdated` subscriber may save again synchronously without deadlocking.
- `Config/ConfigServiceLoadTests.cs` — first-launch defaults are persisted; missing fields fall back to defaults; case-insensitive profile lookup; `JsonElement` normalization; backup restoration when the file is missing; reset regenerates fallback content and replaces the stale backup; relative switch launch paths migrate to absolute (idempotently, and unresolvable paths are left untouched).
- `Config/ConfigServiceSaveTests.cs` — camelCase indented JSON output; plugin-config normalization before saving; `ConfigUpdated` raised; round-trip without data loss.
- `Services/ConfigBackupServiceTests.cs` — package layout (manifest + Profiles.json + secrets.json vs secrets.protected.json); password export never contains plaintext; wrong password fails and leaves state untouched; cross-"machine" protected restore re-seals under the target protector; packages without secrets leave current secrets untouched; missing manifest / future format / corrupt config fail without side effects.

## Related Pages

- Settings editor and slot editing: `/openwiki/architecture/settings-and-slot-editor.md`
- Build, test, and run: `/openwiki/operations/build-test-and-run.md`
- Quickstart: `/openwiki/quickstart.md`
- Edit-and-save walkthrough: `/openwiki/workflows/config-edit-and-save.md`
