## ADDED Requirements

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
