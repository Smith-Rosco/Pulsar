# slot-tone-theme-tokens Specification

## Purpose
TBD - created by archiving change promote-slot-tone-theme-tokens. Update Purpose after archive.
## Requirements
### Requirement: Slot tone brushes SHALL be provided as host theme tokens
The system SHALL provide slot type and slot health tone brushes through theme-injected resources that are available to every supported host surface rendering slot badges.

#### Scenario: Standard settings or dialog surface renders slot badge tones
- **WHEN** a themed settings page or dialog renders slot type or health badge text
- **THEN** the required slot tone brush keys SHALL be available from the active host theme resources without requiring the view to merge a local slot color dictionary solely for semantic color lookup

#### Scenario: Radial menu renders slot type badge tones
- **WHEN** the radial menu renders a slot badge that uses slot type tone coloring
- **THEN** the same semantic slot tone contract SHALL be available within the radial host's resource scope

### Requirement: Slot badge text SHALL remain visible when tone resources are resolved
The system SHALL resolve slot tone brushes through a host-safe resource path so that slot badge text remains readable instead of falling back to an invisible foreground.

#### Scenario: Slot type badge renders in full configuration dialog
- **WHEN** the full slot configuration dialog binds a badge foreground from a slot type tone key
- **THEN** the badge text SHALL render with a visible brush from the active themed resource scope

#### Scenario: Slot health badge renders in create slot dialog
- **WHEN** the create slot dialog binds a badge foreground from a slot health tone key
- **THEN** the badge text SHALL render with a visible brush from the active themed resource scope

#### Scenario: Tone key lookup is invalid or unavailable
- **WHEN** a slot badge requests a tone key that is missing from the supported theme token set
- **THEN** the system SHALL fall back to a visible semantic default tone rather than `Transparent`

