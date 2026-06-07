## MODIFIED Requirements

### Requirement: Collapsed slot cards SHALL remain prose-free scan surfaces
The system SHALL present collapsed slot cards as compact scan surfaces that communicate slot identity and health state without explanatory or instructional prose.

#### Scenario: User scans multiple configured slots
- **WHEN** the slots page renders several collapsed slot cards
- **THEN** each card SHALL expose the slot's identity and health state in a compact form without showing helper sentences about editing or configuration flow

#### Scenario: Slot is incomplete
- **WHEN** a collapsed slot has missing required configuration
- **THEN** the collapsed card SHALL communicate the warning state through status styling or compact state text rather than a long explanatory message

### Requirement: Expanded quick edit SHALL communicate editing intent immediately
The system SHALL make it visually clear that the expanded inline layer is for direct editing rather than for summary reading.

#### Scenario: User expands a slot card
- **WHEN** the inline quick-edit surface opens
- **THEN** the visible controls SHALL dominate the layout and summary-only presentation blocks SHALL NOT appear ahead of the editable controls

#### Scenario: User returns to a previously expanded slot
- **WHEN** the user reopens a slot card they edited recently
- **THEN** the expanded content SHALL present the same editing structure consistently so the user can reorient quickly

### Requirement: Unified slot editor SHALL establish a single editing hierarchy for both Create and Edit
The system SHALL present the Create and Edit Slot configuration views as a single unified editing hierarchy with three landmark sections (Behavior, Appearance, Advanced) in consistent order. The slot identity SHALL be shown through the header orb, label, and compound status indicator — without separate type badge, health badge, or plugin description in the header area. The configuration header SHALL act as a lightweight status anchor rather than a preview panel.

#### Scenario: User opens full configuration from the slots page
- **WHEN** the Edit Slot dialog opens
- **THEN** the dialog SHALL identify the slot clearly and present grouped editing content in a more explicit hierarchy than the inline quick-edit surface, using text hierarchy and spacing as the primary structure

#### Scenario: Slot has both common and advanced settings
- **WHEN** the dialog renders a slot with mixed-complexity fields
- **THEN** the dialog SHALL differentiate foundational settings from deeper configuration without forcing all content into the same visual priority or surrounding every group with equivalent bordered panels. The three landmark sections (Behavior, Appearance, Advanced) SHALL be consistent between Create and Edit views.

#### Scenario: User creates a new slot
- **WHEN** the Create Slot dialog opens
- **THEN** the picker phase SHALL show only the intent grid (Phase 1). After selection, the configuration phase SHALL present the same Behavior → Appearance → Advanced hierarchy as the Edit dialog.

#### Scenario: Slot preview badges render with theme tones
- **WHEN** the configuration view renders slot identity and status indicators
- **THEN** the compound status badge in the header SHALL preserve readable text contrast and SHALL NOT degrade into border-only pills because of resource scope mismatches

#### Scenario: Create Slot picker phase is active
- **WHEN** the unified picker renders the intent grid before a type is selected
- **THEN** the grid SHALL be the sole prominent content and SHALL NOT compete with a simultaneous configuration panel

#### Scenario: User has selected a slot type and enters configuration
- **WHEN** the configuration phase becomes active after type selection
- **THEN** action selection and required configuration SHALL read as the dominant content, with the three-section hierarchy (Behavior, Appearance, Advanced) providing clear progression

#### Scenario: Picker phase shows draft-state guidance
- **WHEN** the unified picker is waiting for the user to choose a slot type
- **THEN** the surface SHALL show a brief orienting hint without long explanatory header content, keeping the intent grid as the primary visual element
