## ADDED Requirements

### Requirement: Hotkey edits via HotkeyBox SHALL trigger dirty state
When the `HotkeyBox` control is used within the settings UI and a user captures or clears a hotkey that differs from the saved configuration, the `HasUnsavedChanges` SHALL become `true` and the Save button red dot SHALL appear. This extends the existing dirty-state contract to the new hotkey editing surface.

#### Scenario: User captures a different hotkey combination
- **WHEN** the user focuses a HotkeyBox bound to `ShowGridHotkey` after settings have loaded
- **AND** the user presses a key combination different from the currently persisted value
- **THEN** the `ShowGridHotkey` setter invokes `MarkDirty()`
- **THEN** `HasUnsavedChanges` SHALL become `true`
- **THEN** the Save button red dot SHALL appear

#### Scenario: User clears a hotkey
- **WHEN** the user presses Backspace on a HotkeyBox with an assigned hotkey
- **AND** the new empty `HotkeyConfig` differs from the persisted value
- **THEN** `MarkDirty()` SHALL be called
- **THEN** `HasUnsavedChanges` SHALL become `true`

#### Scenario: Capturing the same hotkey as current does not trigger dirty
- **WHEN** the user focuses a HotkeyBox and captures the exact same key combination already stored in `_config.Settings.Hotkeys`
- **THEN** `HasUnsavedChanges` SHALL NOT change as a result (the setter may fire but the value is identical)

#### Scenario: Loading settings with HotkeyBox does not trigger dirty
- **WHEN** the SettingsWindow opens and `LoadSettings()` completes
- **AND** the HotkeyBox controls populate with values from Profiles.json
- **THEN** `HasUnsavedChanges` SHALL remain `false`

#### Scenario: HotkeyBox value change during suppressed-dirty scope does not trigger dirty
- **WHEN** `HotkeyBox.Hotkey` is set while `_suppressDirty` is `true` (e.g., during system-initiated load)
- **THEN** `MarkDirty()` SHALL be a no-op
- **THEN** `HasUnsavedChanges` SHALL remain `false`
