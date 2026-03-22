## ADDED Requirements

### Requirement: Slot editor SHALL display action parameters as explicit authored fields
The system SHALL present plugin slot action parameters in the slot editor using explicit field labels and descriptions instead of relying only on plugin-specific placeholders or hidden code knowledge. For any built-in plugin slot whose selected action has parameter metadata, the editor SHALL render those parameters from metadata rather than from a hard-coded plugin template.

#### Scenario: User opens a slot with metadata-defined parameters
- **WHEN** the user expands a slot whose plugin action has declared parameter metadata
- **THEN** the editor shows labeled parameter fields for that action
- **THEN** each field includes human-readable guidance derived from metadata

### Requirement: Slot editor SHALL distinguish required and optional parameters
The system SHALL identify which action parameters are required and which are optional, and SHALL present that distinction clearly in the slot editing experience.

#### Scenario: Slot has both required and optional parameters
- **WHEN** the user edits a slot whose selected action includes required and optional parameters
- **THEN** the editor visually distinguishes required parameters from optional parameters
- **THEN** the user can determine the minimum information needed to save a valid slot

### Requirement: Slot editor SHALL be action-aware
The system SHALL render slot parameters based on the selected plugin action so that only the parameters relevant to that action are shown and validated.

#### Scenario: User changes a plugin action
- **WHEN** the user changes the action for a plugin slot to a different supported action
- **THEN** the parameter form updates to reflect the selected action's metadata-defined parameters
- **THEN** parameters that are not applicable to the selected action are no longer presented as required inputs for that action

### Requirement: Slot editor SHALL provide parameter examples and input-format guidance
The system SHALL support metadata-defined examples, placeholders, and descriptive help text for action parameters so users can understand what values are expected.

#### Scenario: Parameter has example and format help
- **WHEN** the user views a parameter with metadata-defined examples or formatting guidance
- **THEN** the editor shows that guidance near the field or in an equivalent always-discoverable affordance
- **THEN** the guidance explains the expected value shape without requiring external documentation

### Requirement: Slot editor SHALL support metadata-declared picker intents
The system SHALL support metadata hints that indicate an action parameter should be edited through a specialized input affordance such as file browsing, process selection, or secret selection.

#### Scenario: Parameter requests specialized picker support
- **WHEN** the user edits a parameter whose metadata declares a supported picker intent
- **THEN** the editor provides the corresponding specialized affordance in addition to or instead of raw text entry
- **THEN** the selected value is stored in the slot args using the parameter's canonical key

### Requirement: Slot validation SHALL run before execution
The system SHALL validate metadata-defined slot parameters during slot editing and configuration save so that common parameter mistakes are reported before the user executes a plugin.

#### Scenario: Required parameter is missing during save
- **WHEN** the user attempts to save a configuration containing a slot with a missing required parameter
- **THEN** the save validation reports the slot and parameter as invalid
- **THEN** the user receives actionable feedback before plugin execution occurs

#### Scenario: Parameter fails metadata-defined validation
- **WHEN** a slot parameter value violates a metadata-defined validator or type expectation
- **THEN** the validation result identifies the parameter and the reason it is invalid
- **THEN** runtime execution remains blocked only by the existing save/validation policy for invalid configurations

### Requirement: Built-in plugin parameter definitions SHALL be internally consistent
The system SHALL use one canonical parameter vocabulary per built-in plugin action across metadata, slot editing UI, validation, and runtime execution.

#### Scenario: Built-in plugin currently has mismatched terminology
- **WHEN** a built-in plugin has legacy or inconsistent parameter naming across metadata and runtime handling
- **THEN** the change defines a canonical parameter name for the editor and metadata
- **THEN** existing saved configurations remain interpretable through migration or alias compatibility

### Requirement: Existing slot configurations SHALL remain compatible
The system SHALL preserve compatibility with existing saved slot definitions so that previously configured slots continue to load and execute after the new slot parameter authoring model is introduced.

#### Scenario: User opens an existing configuration after upgrade
- **WHEN** Pulsar loads a profile containing slot args authored before this change
- **THEN** the slot editor can display the slot without data loss
- **THEN** the slot remains executable unless it already violates the new validation rules in a way that must be surfaced to the user
