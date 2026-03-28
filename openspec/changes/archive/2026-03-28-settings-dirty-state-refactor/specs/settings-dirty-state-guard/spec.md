## ADDED Requirements

### Requirement: Dirty state SHALL NOT be set during system-initiated loads
The ViewModel SHALL maintain a `_suppressDirty` flag that, when true, causes `MarkDirty()` to be a no-op. All system-initiated operations (initial load, context switch slot population, metadata initialization) SHALL execute within a suppressed-dirty scope.

#### Scenario: Initial settings load does not trigger dirty
- **WHEN** the SettingsWindow opens and `LoadSettings()` completes
- **THEN** `HasUnsavedChanges` SHALL be `false`
- **THEN** the Save button red dot SHALL NOT be visible

#### Scenario: Switching configuration context does not trigger dirty
- **WHEN** the user selects a different profile/context from the navigation list
- **THEN** the slot list is repopulated from the config draft
- **THEN** `HasUnsavedChanges` SHALL remain unchanged from its state before the switch
- **THEN** the Save button red dot SHALL NOT appear if no prior edits were made

#### Scenario: Slot metadata initialization does not trigger dirty
- **WHEN** `InitializeSlotMetadata()` sets `slot.Action` to a fallback value during context load
- **THEN** `HasUnsavedChanges` SHALL NOT be set to `true`
- **THEN** only subsequent user-initiated edits SHALL mark the state as dirty

#### Scenario: GeneralSettings assignment during load does not trigger dirty
- **WHEN** `GeneralSettings` is assigned a new `ProfileSettings` object during `LoadSettings()`
- **THEN** the `OnGeneralSettingsPropertyChanged` handler SHALL NOT call `MarkDirty()`
- **THEN** `HasUnsavedChanges` SHALL remain `false` after load completes

### Requirement: User edits SHALL reliably set dirty state
Despite the suppression mechanism, all genuine user-initiated modifications SHALL still trigger `MarkDirty()` and set `HasUnsavedChanges = true`.

#### Scenario: User modifies a slot label
- **WHEN** the user edits a slot's label in the settings UI
- **THEN** `HasUnsavedChanges` SHALL become `true`
- **THEN** the Save button red dot SHALL appear

#### Scenario: User adds a new slot via dialog
- **WHEN** the user opens the Add Slot dialog, configures a slot, and confirms
- **THEN** `CommitCreatedSlot()` is called
- **THEN** `HasUnsavedChanges` SHALL become `true`
- **THEN** the Save button red dot SHALL appear

#### Scenario: User modifies GeneralSettings after load
- **WHEN** the user changes a setting (e.g., SlotsPerPage) after the UI has fully loaded
- **THEN** `HasUnsavedChanges` SHALL become `true`

### Requirement: Add Slot dialog draft SHALL be isolated from dirty state
The draft `PluginSlot` object created during the Add Slot dialog lifecycle SHALL NOT cause `MarkDirty()` to be called on the parent ViewModel before the user confirms the dialog.

#### Scenario: Opening Add Slot dialog does not trigger dirty
- **WHEN** the user opens the Add Slot dialog
- **THEN** `CreateSlotDraft()` creates a temporary slot object
- **THEN** `HasUnsavedChanges` SHALL NOT change
- **THEN** the Save button red dot SHALL NOT appear

#### Scenario: Selecting a plugin type in Add Slot dialog does not trigger dirty
- **WHEN** the user selects a plugin type inside the Add Slot dialog
- **THEN** `SetSlotDraftAction()` and `InitializeSlotMetadata()` run on the draft slot
- **THEN** `HasUnsavedChanges` on the parent SettingsViewModel SHALL NOT change

#### Scenario: Cancelling Add Slot dialog does not trigger dirty
- **WHEN** the user opens the Add Slot dialog and then cancels
- **THEN** `HasUnsavedChanges` SHALL remain `false` (assuming no prior edits)

### Requirement: Suppression scope SHALL be exception-safe
The `_suppressDirty` flag SHALL always be reset to its prior value after a suppressed operation completes, even if an exception is thrown during the operation.

#### Scenario: Exception during suppressed load does not leave suppression permanently active
- **WHEN** an exception is thrown inside a `WithSuppressedDirty()` or `WithSuppressedDirtyAsync()` call
- **THEN** `_suppressDirty` SHALL be reset to `false` in the `finally` block
- **THEN** subsequent user edits SHALL correctly trigger `MarkDirty()`
