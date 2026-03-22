## ADDED Requirements

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

### Requirement: Full configuration SHALL establish a deeper editing hierarchy
The system SHALL present full configuration as a deeper editing layer with a stronger hierarchy than quick edit, making slot identity, validation state, and grouped fields easy to understand.

#### Scenario: User opens full configuration from the slots page
- **WHEN** the full configuration dialog opens
- **THEN** the dialog SHALL identify the slot clearly and present grouped editing content in a more explicit hierarchy than the inline quick-edit surface

#### Scenario: Slot has both common and advanced settings
- **WHEN** the dialog renders a slot with mixed-complexity fields
- **THEN** the dialog SHALL differentiate foundational settings from deeper configuration without forcing all content into the same visual priority
