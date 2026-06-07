## ADDED Requirements

### Requirement: SlotTypeCard SHALL unify curated and registered slot types
The system SHALL provide a `SlotTypeCard` model class that represents both curated primary intent cards and secondary plugin entries from the plugin registry in a single collection.

#### Scenario: Building the picker data source
- **WHEN** the slot editor initializes
- **THEN** the ViewModel SHALL build a unified collection of `SlotTypeCard` instances from (a) a curated list of primary intent cards with explicit PluginId and DefaultAction, and (b) all registered plugin types from `IPluginMetadataRegistry`

#### Scenario: Querying primary cards for the grid
- **WHEN** the picker renders the curated intent grid
- **THEN** the system SHALL filter the unified collection by `IsPrimary == true` to produce the first-view grid

#### Scenario: Querying all cards for browse-all
- **WHEN** the picker renders the expanded browse-all section
- **THEN** the system SHALL group all cards by `Category` for categorized display

### Requirement: SlotTypeCard SHALL carry optional DefaultAction
The `SlotTypeCard` model SHALL include an optional `DefaultAction` string that, when set, pre-selects a specific plugin action during draft creation, enabling curated cards to bypass the action selection step for the most common use case.

#### Scenario: Curated card with DefaultAction creates draft
- **WHEN** the user selects a `SlotTypeCard` with `DefaultAction = "switch"`
- **THEN** the system SHALL create a draft slot with that action pre-selected and SHALL still display the action selector for the user to change it

#### Scenario: Plugin card without DefaultAction creates draft
- **WHEN** the user selects a `SlotTypeCard` with `DefaultAction = null` from the browse-all list
- **THEN** the system SHALL create a draft slot with the first available action or require the user to select an action

### Requirement: Plugin display metadata SHALL support IsPrimary flag
The plugin display metadata model SHALL include an `IsPrimary` boolean property (default false) that plugin authors can set to true to promote their plugin type to the curated first-view grid.

#### Scenario: Plugin declares primary eligibility
- **WHEN** a plugin's display metadata sets `IsPrimary = true`
- **THEN** the `SlotTypeCard` built from that plugin SHALL have `IsPrimary = true` and SHALL appear in the curated grid

### Requirement: SlotEditorViewModel SHALL replace AddSlotViewModel and SlotConfigurationDialogViewModel
The system SHALL provide a single `SlotEditorViewModel` class with an `EditorMode` property (`Create` or `Edit`) that handles both new slot creation and existing slot editing, eliminating the duplicate ViewModel architecture.

#### Scenario: Opening the Create Slot dialog
- **WHEN** the user invokes the add-slot action in settings
- **THEN** the system SHALL construct `SlotEditorViewModel` with `EditorMode.Create` and SHALL start in the picker phase

#### Scenario: Opening the Edit Slot dialog
- **WHEN** the user clicks edit on an existing slot
- **THEN** the system SHALL construct `SlotEditorViewModel` with `EditorMode.Edit` and the existing `PluginSlot`, starting directly in the configuration phase

#### Scenario: ViewModel exposes identical contracts for both modes
- **WHEN** the dialog footer queries the ViewModel for button text and commands
- **THEN** the `SlotEditorViewModel` SHALL return the same `IWizardDialogViewModel` or `IDialogViewModel` contract regardless of EditorMode
