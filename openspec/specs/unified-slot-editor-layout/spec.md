## Purpose

Defines the shared single-column layout of the unified slot editor (Create and Edit), covering the Behavior/Appearance/Advanced section hierarchy, the sub-action editor block, and the action selector presentation.

## Requirements

### Requirement: Create and edit dialogs SHALL share identical layout structure
The system SHALL render both the Create Slot and Edit Slot configuration views using the same single-column layout with Behavior, Appearance, and Advanced sections in consistent order.

#### Scenario: User creates a new slot and enters configuration
- **WHEN** the user selects a slot type and enters the configuration phase
- **THEN** the layout SHALL present Behavior section first (action selector + required parameters), followed by Appearance section (Label + Color + Icon), followed by an Advanced expander

#### Scenario: User edits an existing slot
- **WHEN** the user opens an existing slot for editing
- **THEN** the layout SHALL present Behavior section first, followed by Appearance section, followed by Advanced expander — identical to the create configuration layout

### Requirement: Appearance section SHALL consistently group Label, Color, and Icon
The system SHALL group Label, Color, and Icon into a single Appearance section in both create and edit dialogs, eliminating the current inconsistency where Label is standalone in create but inside Appearance in edit.

#### Scenario: User configures appearance in Create Slot
- **WHEN** the user scrolls to the Appearance section in the create dialog
- **THEN** Label, Color, and Icon SHALL appear together as a single grouped section

#### Scenario: User configures appearance in Edit Slot
- **WHEN** the user scrolls to the Appearance section in the edit dialog
- **THEN** Label, Color, and Icon SHALL appear together as a single grouped section, identical to the create dialog

### Requirement: Configuration view header SHALL use a unified simplified preview
The system SHALL display a back arrow (create only), the slot orb, slot label, and a single compound status indicator in the configuration header, removing the separate type badge, health badge, plugin description, and summary tokens from the header area.

#### Scenario: Configuration is active
- **WHEN** the configuration phase is active for a selected slot type
- **THEN** the header SHALL show exactly: back arrow (create mode), slot orb with status-colored ring, slot label text, and a compound status badge (e.g., "Ready", "Needs Setup")

#### Scenario: Summary tokens exist for configured parameters
- **WHEN** the slot has configured parameters generating summary tokens
- **THEN** summary tokens SHALL render within the Behavior section below the action selector, NOT in the header area

### Requirement: Action selector SHALL use segmented button group for 2-4 actions
The system SHALL render the action selector as a horizontal segmented button group when a slot type has 2 to 4 available actions, allowing all options to be visible at a glance.

#### Scenario: Slot type has 3 actions
- **WHEN** the slot draft has exactly 3 available actions
- **THEN** the action selector SHALL render as three side-by-side segmented buttons with the selected action highlighted

#### Scenario: Slot type has 1 action
- **WHEN** the slot draft has exactly 1 available action
- **THEN** the action SHALL be displayed as a read-only label indicating the sole behavior

#### Scenario: Slot type has more than 4 actions
- **WHEN** the slot draft has more than 4 available actions
- **THEN** the action selector SHALL use a ComboBox dropdown

### Requirement: Behavior section SHALL include the sub-action editor and layout-style picker
The unified slot editor's Behavior section SHALL host the cascade sub-action editor and the Fan/Ring layout-style picker, following the existing section hierarchy.

#### Scenario: Editor renders sub-action block within Behavior
- **WHEN** the configuration phase renders a slot in the unified editor
- **THEN** the Behavior section SHALL include the sub-action editor block below the action selector and required parameters
- **AND** the Fan/Ring layout-style picker SHALL be rendered adjacent to it

#### Scenario: Editor with no sub-actions stays compact
- **WHEN** a slot has no sub-actions
- **THEN** the sub-action block SHALL render as a compact empty state with an add affordance
- **AND** SHALL NOT push required parameters out of view

#### Scenario: Create and Edit render identically
- **WHEN** the sub-action block renders in Create mode or Edit mode
- **THEN** it SHALL use the same structure and order in both, consistent with the unified editor contract
