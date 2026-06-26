## ADDED Requirements

### Requirement: IHotkeyService SHALL support immediate hotkey application without persistence
`IHotkeyService` SHALL expose an `ApplyHotkey(string actionId, HotkeyConfig config)` method that updates the in-memory configuration and rebuilds the hotkey lookup cache immediately, without persisting to disk. This enables the settings UI to preview hotkey changes live.

#### Scenario: ApplyHotkey updates cache for assigned hotkey
- **WHEN** `ApplyHotkey("ShowGrid", {Key="F1", Modifiers="Control"})` is called
- **THEN** pressing `Ctrl+F1` SHALL invoke the registered `ShowGrid` action callback immediately
- **THEN** no file I/O SHALL occur

#### Scenario: ApplyHotkey removes hotkey from cache when cleared
- **WHEN** `ApplyHotkey("ShowGrid", {Key="", Modifiers=""})` is called
- **AND** `"ShowGrid"` was previously assigned to `Ctrl+Q`
- **THEN** pressing `Ctrl+Q` SHALL NO LONGER invoke the `ShowGrid` action
- **THEN** the hotkey cache SHALL not contain `"ShowGrid"`

#### Scenario: ApplyHotkey handles previously-empty hotkey
- **WHEN** `ApplyHotkey("ShowGrid", {Key="Q", Modifiers="Control"})` is called
- **AND** `"ShowGrid"` was previously empty/unassigned
- **THEN** pressing `Ctrl+Q` SHALL invoke the `ShowGrid` action

### Requirement: IHotkeyService.UpdateHotkey SHALL persist AND apply changes
The existing `UpdateHotkey(string actionId, HotkeyConfig newHotkey)` method SHALL persist the hotkey to disk via `IConfigService.SaveAsync()` AND rebuild the cache, ensuring post-save consistency.

#### Scenario: UpdateHotkey persists to Profiles.json
- **WHEN** `UpdateHotkey("ShowGrid", {Key="F5", Modifiers="Shift"})` is called
- **THEN** the value SHALL be saved to `Profiles.json` under `Settings.Hotkeys["ShowGrid"]`
- **THEN** the hotkey cache SHALL be rebuilt to reflect the new assignment

### Requirement: SettingsViewModel.Save SHALL call UpdateHotkey for all hotkeys
The `SettingsViewModel.Save()` method SHALL invoke `IHotkeyService.UpdateHotkey()` for `ShowGrid` and `ShowSwitcher` after successful config save, so hotkey changes committed via the settings dialog are immediately operational.

#### Scenario: Save triggers hotkey update
- **WHEN** user changes `ShowGridHotkey` to `Ctrl+F1` in settings and clicks Save
- **THEN** `IHotkeyService.UpdateHotkey("ShowGrid", ...)` SHALL be called with the new configuration
- **THEN** pressing `Ctrl+F1` SHALL open the radial menu without requiring a restart

#### Scenario: Save with empty hotkey updates correctly
- **WHEN** user clears `ShowSwitcherHotkey` in settings and clicks Save
- **THEN** `IHotkeyService.UpdateHotkey("ShowSwitcher", {Key="", Modifiers=""})` SHALL be called
- **THEN** the previously assigned hotkey combination SHALL no longer open the switcher

### Requirement: HotkeyService SHALL be injectable with ILogger
The `HotkeyService` constructor SHALL accept `ILogger<HotkeyService>` via dependency injection to support diagnostic logging of parse failures and cache rebuilds.

#### Scenario: Logger injected at construction
- **WHEN** `HotkeyService` is resolved from DI
- **THEN** it SHALL receive an `ILogger<HotkeyService>` instance
- **THEN** cache rebuild warnings SHALL be written through this logger
