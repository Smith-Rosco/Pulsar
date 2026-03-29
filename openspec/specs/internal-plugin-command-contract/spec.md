## ADDED Requirements

### Requirement: Internal system-control plugins SHALL expose explicit actions through metadata
Built-in plugins that control Pulsar itself SHALL expose explicit user-visible actions through plugin metadata instead of requiring wrapper verbs plus nested command arguments.

#### Scenario: User configures a Pulsar system-control slot
- **WHEN** the slot editor loads metadata for an internal system-control plugin
- **THEN** it SHALL be able to show explicit actions such as opening settings or quick-adding a profile without requiring a separate `command` argument to identify the real behavior

#### Scenario: Documentation describes a system-control action
- **WHEN** a built-in internal plugin documents how to trigger an action
- **THEN** the documented slot configuration SHALL use the same explicit action identifier exposed in metadata

### Requirement: Internal command compatibility SHALL preserve existing saved slots where feasible
If an internal system-control plugin previously accepted wrapper verbs or nested command identifiers, the runtime SHALL preserve compatible execution for existing saved slots while canonicalizing the authoring contract.

#### Scenario: Existing slot uses a legacy wrapper action
- **WHEN** Pulsar executes an existing internal system-control slot that still uses an older wrapper verb or nested command shape
- **THEN** the plugin SHALL continue to resolve the request to the same logical behavior if the old form is still supported

#### Scenario: New internal slot is authored after standardization
- **WHEN** a user creates a new internal system-control slot
- **THEN** the saved slot configuration SHALL be able to reference the explicit canonical action directly instead of requiring a wrapper action plus nested command value
