## ADDED Requirements

### Requirement: HotkeyConfig SHALL expose empty-state semantics
The `HotkeyConfig` model SHALL provide an `IsEmpty` property that returns `true` when `Key` is null or empty, indicating the hotkey is unassigned. It SHALL also provide a `NormalizedSignature` property (`"MODIFIERS+KEY"` uppercase) for equality comparison, and a `DisplayText` property for human-readable rendering (`"Control + Q"` format).

#### Scenario: IsEmpty is true when key is empty
- **WHEN** `HotkeyConfig.Key` is `""` or null
- **THEN** `IsEmpty` SHALL return `true`

#### Scenario: IsEmpty is false when key is set
- **WHEN** `HotkeyConfig.Key` is `"Q"` and `Modifiers` is `"Control"`
- **THEN** `IsEmpty` SHALL return `false`

#### Scenario: NormalizedSignature formats for comparison
- **WHEN** `HotkeyConfig` has `Key = "Q"` and `Modifiers = "Control,Shift"`
- **THEN** `NormalizedSignature` SHALL equal `"CONTROL,SHIFT+Q"` (case-insensitive, consistent order)

#### Scenario: NormalizedSignature is empty when key is empty
- **WHEN** `HotkeyConfig.IsEmpty` is `true`
- **THEN** `NormalizedSignature` SHALL return `""`

#### Scenario: DisplayText formats for user display
- **WHEN** `HotkeyConfig` has `Key = "Q"` and `Modifiers = "Control"`
- **THEN** `DisplayText` SHALL be `"Control + Q"`

#### Scenario: DisplayText with no modifiers shows key only
- **WHEN** `HotkeyConfig` has `Key = "F1"` and `Modifiers = ""`
- **THEN** `DisplayText` SHALL be `"F1"`

### Requirement: IHotkeyService SHALL validate hotkey assignments for conflicts
`IHotkeyService` SHALL expose a `ValidateHotkey(string actionId, HotkeyConfig config)` method that returns a `HotkeyValidationResult` indicating whether the configuration conflicts with any other registered action's hotkey, whether it is a system-reserved combination, or whether it is empty.

#### Scenario: No conflict when unique combination
- **WHEN** `ValidateHotkey("ShowGrid", {Key="F1", Modifiers="Control"})` is called
- **AND** no other registered action uses `Control+F1`
- **THEN** `HotkeyValidationResult.Conflicts` SHALL be empty
- **THEN** `HotkeyValidationResult.IsSystemReserved` SHALL be `false`

#### Scenario: Conflict detected with another action
- **WHEN** `ValidateHotkey("ShowGrid", {Key="Q", Modifiers="Control"})` is called
- **AND** `"ShowSwitcher"` is already configured with `{Key="Q", Modifiers="Control"}`
- **THEN** `HotkeyValidationResult.Conflicts` SHALL contain one entry with `ConflictingActionId = "ShowSwitcher"`

#### Scenario: Self-reference is not a conflict
- **WHEN** `ValidateHotkey("ShowGrid", {Key="Q", Modifiers="Control"})` is called
- **AND** only `"ShowGrid"` itself uses `Control+Q`
- **THEN** `HotkeyValidationResult.Conflicts` SHALL be empty

#### Scenario: Empty hotkey returns IsEmpty with no conflicts
- **WHEN** `ValidateHotkey("ShowGrid", {Key="", Modifiers=""})` is called
- **THEN** `HotkeyValidationResult.IsEmpty` SHALL be `true`
- **THEN** `HotkeyValidationResult.Conflicts` SHALL be empty

#### Scenario: System-reserved combination detected
- **WHEN** `ValidateHotkey("ShowGrid", {Key="Delete", Modifiers="Control,Alt"})` is called
- **THEN** `HotkeyValidationResult.IsSystemReserved` SHALL be `true`

#### Scenario: Empty hotkeys not compared for conflicts
- **WHEN** `ValidateHotkey("ShowGrid", {Key="Q", Modifiers="Control"})` is called
- **AND** `"ShowSwitcher"` is configured with `{Key="", Modifiers=""}` (empty)
- **THEN** `HotkeyValidationResult.Conflicts` SHALL NOT include the empty `"ShowSwitcher"` entry

### Requirement: HotkeyService SHALL skip empty hotkeys during cache rebuild
The `RebuildHotkeyCache()` method SHALL skip any `HotkeyConfig` where `IsEmpty` is `true`, ensuring no action is triggered for unassigned hotkeys.

#### Scenario: Empty hotkey not added to lookup cache
- **WHEN** `HotkeyConfig` for action `"ShowGrid"` has `Key = ""`
- **AND** `RebuildHotkeyCache()` executes
- **THEN** the `_hotkeysByMainKey` dictionary SHALL NOT contain an entry for `"ShowGrid"`

#### Scenario: Assigned hotkey added to lookup cache
- **WHEN** `HotkeyConfig` for action `"ShowGrid"` has `Key = "Q"` and `Modifiers = "Control"`
- **AND** `RebuildHotkeyCache()` executes
- **THEN** the `_hotkeysByMainKey` dictionary SHALL contain a list for VK code `Q` with the `ShowGrid` action

### Requirement: RebuildHotkeyCache SHALL log parse failures instead of silently ignoring
The `RebuildHotkeyCache()` method SHALL catch exceptions during hotkey parsing and SHALL log a warning with the action ID via `ILogger<HotkeyService>`, rather than silently swallowing the exception.

#### Scenario: Invalid key name logged and skipped
- **WHEN** a hotkey config has `Key = "InvalidKeyName"`
- **AND** `RebuildHotkeyCache()` attempts to parse it
- **THEN** a warning log entry SHALL be written including the action ID
- **THEN** the invalid entry SHALL be skipped (not added to cache)

### Requirement: ProfileSettings SHALL define hotkey defaults as the single source of truth
The `ProfileSettings.Hotkeys` dictionary initializer SHALL be the authoritative source of default hotkey values. No other location (`HotkeyService.InitializeAsync` or `SettingsViewModel` getters) SHALL define fallback hotkey values independently.

#### Scenario: InitializeAsync does not force-create defaults
- **WHEN** `ProfilesConfig` is loaded with an empty `Hotkeys` dictionary
- **AND** `InitializeAsync()` executes
- **THEN** no default `ShowGrid` or `ShowSwitcher` entries SHALL be created
- **THEN** `_hotkeysByMainKey` SHALL remain empty

#### Scenario: SettingsViewModel getter returns existing config or empty
- **WHEN** `ShowGridHotkey` getter is called
- **AND** `_config.Settings.Hotkeys` does not contain `"ShowGrid"`
- **THEN** the getter SHALL return a new `HotkeyConfig()` with empty `Key` and `Modifiers`
- **THEN** it SHALL NOT return a hardcoded default (e.g., `{Key="Q", Modifiers="Control"}`)

### Requirement: ConfigValidationPipeline SHALL validate hotkey configurations
The `ConfigValidationPipeline` SHALL include a hotkey validation stage that detects duplicate hotkey combinations across all configured actions and emits warnings.

#### Scenario: Duplicate hotkey emits warning
- **WHEN** `ValidateAsync()` is called on a config where `"ShowGrid"` and `"ShowSwitcher"` both have `{Key="Q", Modifiers="Control"}`
- **THEN** the `ValidationResult` SHALL contain at least one warning with the category `"Hotkeys"`
- **THEN** the warning message SHALL identify both conflicting action IDs

#### Scenario: Unique hotkeys emit no warnings
- **WHEN** `ValidateAsync()` is called on a config where all non-empty hotkeys have unique `NormalizedSignature` values
- **THEN** no hotkey-related warnings SHALL be added

#### Scenario: Empty hotkeys do not trigger conflict warnings
- **WHEN** `ValidateAsync()` is called on a config where `"ShowGrid"` has `{Key="", Modifiers=""}` and `"ShowSwitcher"` has `{Key="", Modifiers=""}`
- **THEN** no hotkey conflict warnings SHALL be emitted

### Requirement: ReservedHotkeys SHALL catalog Windows-system-reserved combinations
A static `ReservedHotkeys` class SHALL define the set of key+modifier combinations that Windows reserves (Ctrl+Alt+Del, Win+L, Ctrl+Esc, Alt+F4, Alt+Tab, Ctrl+Alt+Tab, Win+Tab) so they can be checked during validation.

#### Scenario: Reserved combinations defined at compile time
- **WHEN** the application is compiled
- **THEN** `ReservedHotkeys.SystemReserved` SHALL contain at least 7 entries including `Ctrl+Alt+Del`, `Win+L`, and `Alt+F4`
