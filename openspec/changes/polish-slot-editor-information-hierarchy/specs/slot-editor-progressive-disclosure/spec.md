## ADDED Requirements

### Requirement: Low-priority field guidance SHALL use progressive disclosure
The system SHALL move descriptions, examples, and other low-priority instructional guidance into tooltip-style or comparable secondary disclosure patterns when that content is not required for immediate comprehension.

#### Scenario: Field includes example text
- **WHEN** a parameter exposes an example or descriptive help text that is not critical to understanding current state
- **THEN** the system SHALL make that help available through secondary disclosure rather than reserving permanent vertical space beneath the field

#### Scenario: User needs more context for a field
- **WHEN** the user focuses or hovers the field help affordance
- **THEN** the system SHALL reveal the field guidance close to the related control without changing the overall page hierarchy

### Requirement: Critical status information SHALL remain visible
The system SHALL keep validation status, required-state indicators, and other critical correctness signals visible in-context rather than hiding them behind disclosure.

#### Scenario: Required field is missing
- **WHEN** a field required for slot execution is not configured
- **THEN** the UI SHALL expose that incomplete state directly in the active editing surface without requiring hover or focus to discover it

#### Scenario: Validation guidance affects correction
- **WHEN** the system has corrective validation feedback relevant to the user's current edit
- **THEN** the UI SHALL display the validation state or summary visibly even if explanatory field help uses secondary disclosure

### Requirement: Progressive disclosure SHALL not obscure keyboard access
The system SHALL ensure that secondary disclosure patterns used for field help remain accessible through non-pointer interaction.

#### Scenario: Keyboard user navigates a field with help metadata
- **WHEN** the user reaches a help affordance through keyboard navigation
- **THEN** the same guidance available on hover SHALL be reachable through focus behavior or another keyboard-accessible interaction
