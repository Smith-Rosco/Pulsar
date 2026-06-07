## ADDED Requirements

### Requirement: System presents a unified intent grid on slot creation
The system SHALL present a single view containing curated intent cards when the user opens the Create Slot dialog, eliminating the separate Scenario/Advanced flow toggle.

#### Scenario: User opens Create Slot dialog
- **WHEN** the user clicks "Add Slot" from the settings page
- **THEN** the dialog SHALL display a grid of primary intent cards (Switch App, Open Target, Send Keys, Fill Secret, Run Script, System) without any "Scenario vs Advanced" flow toggle

#### Scenario: User clicks an intent card
- **WHEN** the user clicks any primary intent card
- **THEN** the system SHALL create a slot draft with the card's mapped plugin ID and default action, and SHALL transition the view to the configuration phase

#### Scenario: User searches slot types
- **WHEN** the user types in the search bar at the top of the picker
- **THEN** the system SHALL filter primary cards in real-time and SHALL also surface matching results from the full plugin registry

### Requirement: System provides browse-all path for non-primary slot types
The system SHALL allow users to access all registered plugin types, including extension plugins, through a collapsible "Browse All" section.

#### Scenario: User expands browse-all section
- **WHEN** the user clicks "Browse all slot types..." at the bottom of the picker
- **THEN** the system SHALL expand a categorized list of all registered plugin types grouped by category, replacing the need for the former "Advanced" flow

#### Scenario: User selects a plugin type from browse-all
- **WHEN** the user selects a plugin type from the expanded browse-all list
- **THEN** the system SHALL create a slot draft with the selected plugin ID and SHALL transition to the configuration phase

### Requirement: Intent cards map to canonical plugin-action pairs
Each curated intent card SHALL map to a specific plugin ID and default action, pre-selecting the most common action without requiring the user to choose an action explicitly during the picker phase.

#### Scenario: User picks Switch App card
- **WHEN** the user clicks the "Switch App" card
- **THEN** the system SHALL create a draft with pluginId "com.pulsar.winswitcher" and action "switch"

#### Scenario: User picks Open Target card
- **WHEN** the user clicks the "Open Target" card
- **THEN** the system SHALL create a draft with pluginId "com.pulsar.command" and action "run"

#### Scenario: User picks Send Keys card
- **WHEN** the user clicks the "Send Keys" card
- **THEN** the system SHALL create a draft with pluginId "com.pulsar.command" and action "sendkeys"

#### Scenario: User picks Fill Secret card
- **WHEN** the user clicks the "Fill Secret" card
- **THEN** the system SHALL create a draft with pluginId "com.pulsar.pki" and action "fill"

### Requirement: Configuration phase provides back-navigation to picker
The configuration view SHALL include a back arrow that returns the user to the type picker, preserving the draft state so the user can compare different slot type configurations.

#### Scenario: User wants to change slot type during configuration
- **WHEN** the user clicks the back arrow in the configuration phase
- **THEN** the system SHALL return to the picker phase without discarding the current draft slot

#### Scenario: User selects a different type after returning
- **WHEN** the user selects a new intent card after navigating back from configuration
- **THEN** the system SHALL replace the draft slot with a new one for the selected type and SHALL transition back to configuration

### Requirement: Plugin metadata supports primary card designation
Plugin display metadata SHALL support an optional `IsPrimary` flag that, when true, promotes the plugin type to the curated first-view grid.

#### Scenario: Extension plugin declares primary eligibility
- **WHEN** an extension plugin's display metadata sets `IsPrimary = true`
- **THEN** the plugin SHALL appear as an additional card in the curated intent grid alongside the built-in primary cards

#### Scenario: Plugin does not declare primary eligibility
- **WHEN** a plugin's display metadata has `IsPrimary = false` or absent
- **THEN** the plugin SHALL appear only in the "Browse All" expanded category list
