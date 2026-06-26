## Context

The global hotkey system has three layers: (1) `GlobalKeyboardHook` — a `WH_KEYBOARD_LL` low-level native hook with hybrid modifier tracking, (2) `HotkeyService` — the application service that maintains a `_hotkeysByMainKey` lookup cache built from `Profiles.json` config, and (3) `SettingsGeneralPage` — the WPF settings UI that captures key combinations via `PreviewKeyDown`. These layers are loosely connected via `IHotkeyService`, but the connection is incomplete: the UI writes hotkey changes to `ProfilesConfig` in-memory (saved on explicit save), while the live `HotkeyService` cache rebuilds only on `InitializeAsync()` (startup). There is no live feedback loop, no validation, and no empty-state support.

Twelve distinct issues were identified during audit — 3 bugs (inconsistent defaults, dead `UpdateHotkey` code, unrecoverable empty state), 4 quality concerns (magic strings, code-behind business logic, silent error swallowing, no live update), and 5 feature gaps (no empty hotkey, no conflict detection, no system-reserved filtering, no visual feedback, no reusable control).

## Goals / Non-Goals

**Goals:**
- Enable users to clear a hotkey (unassign it) by pressing Backspace/Delete in the capture field
- Detect and visually warn when two actions share the same key+modifier combination
- Detect and warn when a user captures a Windows-reserved combination
- Apply hotkey changes to the running engine immediately without restart
- Extract capture logic into a reusable `HotkeyBox` WPF control
- Unify default hotkey values to a single source of truth
- Wire up the existing but dead `UpdateHotkey` API path
- Integrate hotkey validation into `ConfigValidationPipeline` for save-time enforcement
- Eliminate magic strings with `HotkeyActionIds` and `HotkeyModifiers` constants

**Non-Goals:**
- Dynamic hotkey discovery (adding arbitrary new action IDs at runtime)
- Multi-key chord sequences (e.g., `Ctrl+K, Ctrl+S` — only single-combo supported)
- Per-application hotkey overrides
- Hotkey export/import
- Visual drag-and-drop hotkey rebinding UX
- Changing `GlobalKeyboardHook` internals (stable, well-tested)

## Decisions

### Decision 1: `HotkeyConfig.Key == ""` means "unassigned"
**Alternatives considered**: Null `HotkeyConfig`, separate `Enabled: bool` flag.

**Rationale**: Empty string is already the C# default and maps naturally to JSON `"Key": ""`. Null requires null-check propagation across the entire system. An `Enabled` flag adds serialization complexity without benefit. The `[JsonIgnore]` property `IsEmpty => string.IsNullOrEmpty(Key)` provides a clean API barrier so callers never check `Key` emptiness directly.

### Decision 2: Validation returns structured result rather than throwing
**Alternatives considered**: Throw on conflict, return `bool`, fire event.

**Rationale**: `HotkeyValidationResult` with `Conflicts`, `IsSystemReserved`, and `IsEmpty` lets the UI layer render nuanced feedback (different warning colors for conflict vs. reserved vs. empty). Matches the `ValidationResult` pattern already used in `ConfigValidationPipeline`. Event-driven validation would require subscription management and risk race conditions.

### Decision 3: HotkeyBox as a WPF UserControl, not a Behavior/Style
**Alternatives considered**: Attached behavior on TextBox, custom ControlTemplate style, separate capture service.

**Rationale**: Hotkey capture touches three UI concerns (input capture, clear gesture, validation display) that form a cohesive visual unit. A UserControl encapsulates the Grid layout, TextBox, and conflict badge in one XAML file, is trivially composable (just drop `<controls:HotkeyBox />`), and follows the existing `Views/Controls/` pattern. A Behavior would scatter the visual template across calling pages. A service would add unnecessary abstraction for what is fundamentally a UI concern.

### Decision 4: Live-update via new `ApplyHotkey()` (non-async, no persistence)
**Alternatives considered**: Modify `UpdateHotkey()` to not save, fire config change event.

**Rationale**: `UpdateHotkey()` currently saves to disk immediately, which conflicts with the SettingsViewModel's explicit save model. `ApplyHotkey()` updates the in-memory config + rebuilds the cache without persistence. The existing `UpdateHotkey()` is retained for save-time use (called from `SettingsViewModel.Save()`). This avoids adding an event-based notification path between ConfigService and HotkeyService.

### Decision 5: Conflict warnings are non-blocking (PowerToys/VS Code style)
**Alternatives considered**: Block save on conflict, automatically reassign the other hotkey.

**Rationale**: Users may intentionally share a hotkey combination if one action is infrequently used. Blocking save forces a resolution path that adds friction. Auto-reassigning the other hotkey silently changes configuration without consent. Visual warning with red badge + tooltip provides full user agency without obfuscating the problem.

### Decision 6: HotkeyConstants as a single static file
**Alternatives considered**: Constants on `HotkeyConfig` class, constants on `IHotkeyService`, resource file.

**Rationale**: Action IDs and modifier keywords are used across 5+ namespaces (Models, Services, ViewModels, Views, Tests). A dedicated `Helpers/HotkeyConstants.cs` avoids circular dependencies. The existing `Helpers/` directory already hosts similar utility constants.

## Risks / Trade-offs

- **[Risk] User clears both ShowGrid and ShowSwitcher hotkeys, can't open radial menu at all** → Mitigation: Tray icon right-click menu always provides "Settings" access; user can re-configure. Documentation note in tooltip: "Without any global hotkey, use tray menu to access settings."

- **[Risk] Backspace/Delete during normal text editing in other fields triggers hotkey clear** → Mitigation: `HotkeyBox` is a dedicated control, only its TextBox handles Backspace for clear. Standard TextBoxes unaffected.

- **[Risk] HotkeyConfig.IsEmpty serialized as Key="" may confuse external config tools** → Mitigation: This is the most natural representation. Document in code comments on HotkeyConfig.

- **[Trade-off] ApplyHotkey rebuilds entire cache per change instead of incremental update** → Acceptable: Cache rebuild iterates ~2 items (O(n) where n=number of registered actions). No measurable performance impact at current scale.

- **[Trade-off] Reset to defaults restores both hotkeys with default values** → Expected behavior: `ResetConfig` creates a fresh `ProfilesConfig` with the dictionary defaults from `ProfilesConfig.cs`.
