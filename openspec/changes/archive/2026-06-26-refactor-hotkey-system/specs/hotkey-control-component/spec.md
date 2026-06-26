## ADDED Requirements

### Requirement: HotkeyBox SHALL capture key combinations on focus
The `HotkeyBox` user control SHALL listen for `PreviewKeyDown` events on its internal read-only `TextBox` and SHALL interpret the pressed key plus the current `Keyboard.Modifiers` state to produce a `HotkeyConfig` value.

#### Scenario: Key with modifiers captured
- **WHEN** the user focuses the HotkeyBox and presses `Ctrl+Shift+Q`
- **THEN** the bound `Hotkey` property SHALL be set to `{Key="Q", Modifiers="Control,Shift"}`
- **THEN** the TextBox SHALL display `"Control,Shift + Q"`

#### Scenario: Modifier-only press ignored
- **WHEN** the user focuses the HotkeyBox and presses only `Ctrl` (or `Shift`, `Alt`, `Win`)
- **THEN** the captured key event SHALL be ignored
- **THEN** the bound `Hotkey` property SHALL NOT change

#### Scenario: Hotkey capture resumes on focus with pause
- **WHEN** the HotkeyBox receives focus
- **THEN** `IHotkeyService.Pause()` SHALL be called to prevent the global hook from triggering actions during capture

#### Scenario: Hotkey capture resumes on focus loss
- **WHEN** the HotkeyBox loses focus
- **THEN** `IHotkeyService.Resume()` SHALL be called to re-enable global hotkey detection

### Requirement: HotkeyBox SHALL support clearing the hotkey
The `HotkeyBox` control SHALL interpret `Backspace`, `Delete`, and `Escape` key presses as a request to clear the assigned hotkey, setting the bound `Hotkey` property to `{Key="", Modifiers=""}`.

#### Scenario: Backspace clears hotkey
- **WHEN** the HotkeyBox has focus and a hotkey is currently assigned
- **AND** the user presses `Backspace`
- **THEN** the bound `Hotkey` property SHALL be set to `{Key="", Modifiers=""}`
- **THEN** the TextBox SHALL display an empty string (or "(None)" placeholder)

#### Scenario: Delete clears hotkey
- **WHEN** the HotkeyBox has focus and a hotkey is currently assigned
- **AND** the user presses `Delete`
- **THEN** the bound `Hotkey` property SHALL be set to `{Key="", Modifiers=""}`

#### Scenario: Escape clears hotkey
- **WHEN** the HotkeyBox has focus and a hotkey is currently assigned
- **AND** the user presses `Escape`
- **THEN** the bound `Hotkey` property SHALL be set to `{Key="", Modifiers=""}`

### Requirement: HotkeyBox SHALL display conflict validation feedback
When the bound `ValidationResult` property indicates a conflict or system-reserved issue, the `HotkeyBox` SHALL display visual feedback: a red border around the capture TextBox, a conflict badge, and a tooltip describing the issue.

#### Scenario: Conflict badge shown when validation detects conflict
- **WHEN** `ValidationResult.Conflicts` contains at least one entry
- **THEN** the conflict badge SHALL be visible with a text like "Conflict: already assigned to \"Show Switcher\""
- **THEN** the TextBox border SHALL change to a red color

#### Scenario: No badge when validation result is clean
- **WHEN** `ValidationResult.Conflicts` is empty and `IsSystemReserved` is false and `IsEmpty` is false
- **THEN** the conflict badge SHALL be collapsed
- **THEN** the TextBox border SHALL use the normal theme color

#### Scenario: System reserved warning shown
- **WHEN** `ValidationResult.IsSystemReserved` is true
- **THEN** the conflict badge SHALL be visible with a warning about system reservation
- **THEN** the TextBox border SHALL change to an amber/orange color (warning, not error)

#### Scenario: Empty hotkey shows placeholder text
- **WHEN** the bound `Hotkey.IsEmpty` is true
- **THEN** the TextBox SHALL display the localized "(None)" placeholder
- **THEN** no conflict badge SHALL be shown

### Requirement: HotkeyBox SHALL be bound via dependency properties
The `HotkeyBox` control SHALL expose `Hotkey` and `ValidationResult` as WPF dependency properties, enabling two-way data binding with ViewModels.

#### Scenario: Hotkey property binding
- **WHEN** the control is used with `Hotkey="{Binding ShowGridHotkey}"`
- **THEN** changes to the property in code SHALL update the TextBox display
- **THEN** capturing a key combination SHALL update the ViewModel property

#### Scenario: ValidationResult property binding
- **WHEN** the control is used with `ValidationResult="{Binding ShowGridHotkeyValidation}"`
- **THEN** changes to `ShowGridHotkeyValidation` in the ViewModel SHALL update the conflict badge visibility

### Requirement: HotkeyBox SHALL support a localized placeholder
The control SHALL accept a `PlaceholderText` property that displays when no hotkey is assigned (`IsEmpty` is true). This SHALL default to the localized "(None)" string.

#### Scenario: Placeholder shown when empty
- **WHEN** `Hotkey.IsEmpty` is true
- **AND** `PlaceholderText` is set to "(None)"
- **THEN** the TextBox SHALL display "(None)" as placeholder/watermark text

### Requirement: HotkeyBox SHALL support an ActionId for validation dispatch
The control SHALL expose an `ActionId` string property so that when a hotkey is captured or cleared, the ViewModel or code-behind can trigger `IHotkeyService.ValidateHotkey()` with the correct action identifier.

#### Scenario: ActionId passed through on capture
- **WHEN** `ActionId` is set to `"ShowGrid"` on the HotkeyBox
- **AND** user captures `Ctrl+F1`
- **THEN** the consumer SHALL be able to call `ValidateHotkey("ShowGrid", capturedConfig)` using the control's `ActionId`
