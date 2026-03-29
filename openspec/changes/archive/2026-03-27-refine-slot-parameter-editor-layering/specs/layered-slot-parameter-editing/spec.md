## ADDED Requirements

### Requirement: Slot editing SHALL provide quick edit and full configuration layers
The system SHALL provide a two-layer slot editing experience composed of an inline quick-edit layer on the slots page and a dedicated full-configuration layer for complete parameter authoring.

#### Scenario: User needs a quick change
- **WHEN** the user edits a label, action, or other designated quick-edit field from the slots list
- **THEN** the system SHALL allow the change within the expanded slot card without requiring a separate dialog

#### Scenario: User needs deep configuration
- **WHEN** the user chooses to edit the complete parameter set for a slot
- **THEN** the system SHALL open a dedicated configuration surface that contains the full parameter authoring experience for that slot

### Requirement: Full configuration SHALL own long-form guidance and advanced editing
The dedicated full-configuration layer SHALL contain the explanatory content and advanced interactions that would otherwise disrupt list scanability.

#### Scenario: Action has examples and advanced parameters
- **WHEN** an action exposes examples, advanced sections, or multi-step picker flows
- **THEN** those elements SHALL be presented in the full-configuration layer rather than the inline quick-edit layer

#### Scenario: Validation details exceed list-friendly scope
- **WHEN** slot validation produces detailed corrective guidance
- **THEN** the inline card MAY show a concise status summary, but the complete validation details SHALL be available in the full-configuration layer

### Requirement: The transition between layers SHALL preserve slot context
Moving from quick edit to full configuration SHALL preserve the slot identity and current editing context so users can continue the same task without reorienting.

#### Scenario: User opens full configuration from a warning state
- **WHEN** the user launches the full-configuration layer from a slot that has validation problems
- **THEN** the full-configuration layer SHALL identify the same slot and expose the current action and parameter state immediately

#### Scenario: User closes full configuration
- **WHEN** the user returns from the full-configuration layer to the slots page
- **THEN** the system SHALL return the user to the same slot collection context rather than resetting the page selection
