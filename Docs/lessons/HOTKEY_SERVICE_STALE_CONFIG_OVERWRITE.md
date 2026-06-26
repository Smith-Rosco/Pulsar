# HotkeyService Stale Config Reference Overwrites User Changes

**Status**: Published  
**Scope**: Lesson  
**Applies To**: HotkeyService, SettingsViewModel Save flow  
**Last Updated**: 2026-06-26

---

## Rule (TL;DR)

**Never hold a long-lived `_config` reference from `LoadAsync()` in services that outlive a single save cycle.** Always use `_configService.Current` or subscribe to `ConfigUpdated` to stay in sync. If a service saves config in response to a user action, ensure its internal reference matches the **latest** cached config, not the one from initialization.

---

## Symptom

After deleting all slots/settings in the Settings page and saving, `Profiles.json` still contains the old slot data. The save appears to succeed (no error notification) but the file content reverts to stale data.

---

## Root Cause

`HotkeyService.InitializeAsync()` stores a direct reference to the config object returned by `_configService.LoadAsync()`:

```csharp
_config = await _configService.LoadAsync(); // stale reference after any save
```

`SettingsViewModel.Save()` then does:

```csharp
await _configService.SaveAsync(_config);           // (1) saves user changes
_ = _hotkeyService.UpdateHotkey(id, hotkeyConfig); // (2) HOTKEY SERVICE: SaveAsync(old _config)
```

Step (1) saves the user's modified config and updates `ConfigService._cachedConfig` to the new object.  
Step (2) calls `HotkeyService.UpdateHotkey`, which calls `SaveAsync(HotkeyService._config)` — the **original stale reference** — overwriting the user's changes with old slot data.

---

## Correct Pattern

**In the service that triggers the save (SettingsViewModel):** Replace the post-save `UpdateHotkey` calls with cache-only refresh:

```csharp
await _configService.SaveAsync(_config);
_hotkeyService.RebuildCache(); // refresh cache only, no second save
```

**In the hotkey service:** Provide a `RebuildCache()` method that re-reads from `_configService.Current` before rebuilding the in-memory hotkey cache:

```csharp
public void RebuildCache()
{
    _config = _configService.Current;  // pick up latest cached config
    RebuildHotkeyCache();
}
```

---

## Detection Checklist

- [ ] Does the service hold a `_config` reference from `LoadAsync()`?
- [ ] Could that reference become stale after another component calls `SaveAsync()`?
- [ ] Does the service call `SaveAsync()` with that stale reference?
