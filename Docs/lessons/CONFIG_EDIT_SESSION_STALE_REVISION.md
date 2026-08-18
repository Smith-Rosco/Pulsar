# Config Edit Session Stale Revision

**Status**: Published  
**Scope**: Lesson  
**Applies To**: `ConfigEditSession`, `ConfigService`, `SettingsViewModel.Save`  
**Last Updated**: 2026-08-18

---

## Rule (TL;DR)

**A long-lived `ConfigEditSession` must re-arm its captured revision after a successful commit.** On a `ConfigConcurrencyException` from a concurrent writer, the caller must **rebase** (fold the writer's changes into untouched regions of the draft) and **retry** — never hold one session across many commits expecting the `BeginAsync` revision to stay valid, and never surface a raw concurrency error to the user when a recoverable rebase exists.

---

## Symptom

- Saving settings shows "保存更改失败，请重试" (Failed to save changes, please retry).
- **Deterministic**: the *second* save in a row always fails; the first save succeeds.
- **Probabilistic**: any background writer that commits while the Settings window is open (plugin settings dialog, WinSwitcher blacklist sync, tutorial/onboarding progress) makes even the *next* save fail.
- "Switching tabs fixes it" — the navigation guard's Save/Don't-Save flow calls `LoadSettings()`, which starts a fresh session at the current revision.

---

## Root Cause

[ADR-009](./../decisions/009-config-snapshot-seam.md) introduced optimistic concurrency for config commits: `ConfigService.CurrentRevision` increments on every successful save, and `ConfigEditSession.CommitAsync` saves against the revision captured at `BeginAsync`.

`SettingsViewModel` reused **one** session for the whole window lifetime:

```csharp
if (_editSession == null)
    _editSession = await ConfigEditSession.BeginAsync(_configService);
await _editSession.CommitAsync();   // 1st commit bumps the store revision
```

After the first successful commit the captured revision was stale, so the second commit threw `ConfigConcurrencyException`. Additionally:

- Background writers (e.g. `PluginSettingsDialogViewModel` committing an **unchanged** draft, `ProcessRegistryService.SyncToProfilesConfigAsync`, `OnboardingState`/`TutorialOrchestrator`) bump the revision at arbitrary times, so even a fresh session could fail.
- `ConfigService.SaveAsync` checked the revision **outside** the write lock (TOCTOU): two sessions started at the same revision could both pass the check and overwrite each other.

---

## Fix

1. **Re-arm after commit** — `ConfigEditSession.CommitAsync` updates `_revisionAtBegin = _store.CurrentRevision` after a successful save, so repeated saves from one session work.
2. **Rebase on conflict** — `ConfigEditSession.RebaseAsync` retains the base snapshot from `BeginAsync`, and on conflict replaces draft regions that the user has **not** touched (still JSON-equal to the base) with the store's current values, then re-arms the revision. The editor keeps the user's edits; the external writer's changes survive.
3. **Retry in the caller** — `SettingsViewModel.Save` (and `DeleteProfile`) commit through `CommitWithRecoveryAsync`, which catches `ConfigConcurrencyException`, rebases, and retries (up to 3 attempts).
4. **Remove no-op commits** — `PluginSettingsDialogViewModel.CanCloseAsync` no longer `BeginAsync`+`CommitAsync` an unchanged draft (its real persistence flows through `PluginViewModel.OnSettingChanged` → its own session).
5. **Close the TOCTOU** — `ConfigService.SaveAsync` moved the revision check inside the write lock so the check and the revision bump are atomic.
6. **Re-point bound references** — `SettingsViewModel.Save` calls `ResyncSettingsReferences` after a successful commit so `GeneralSettings` points at the committed draft if a rebase replaced it.

---

## Related Documents

- [ADR-009: Enforce the Config Snapshot Seam](./../decisions/009-config-snapshot-seam.md) - the optimistic-concurrency design this lesson corrects
- [ADR-005: Config Persistence Uses a Single Writer](./../decisions/005-config-single-writer.md) - superseded in part by ADR-009
- [Hotkey Service Stale Config Overwrite](./HOTKEY_SERVICE_STALE_CONFIG_OVERWRITE.md) - the earlier stale-config class of bug

---

**Change History**:
- v1.0.0 (2026-08-18): Initial version
