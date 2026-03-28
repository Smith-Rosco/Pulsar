# slot-parameter-authoring

## Purpose
Define the metadata-driven slot parameter authoring experience across summaries, quick edit, and full configuration.

## Requirements

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

### Requirement: Slots page SHALL prioritize scanable slot summaries
The slots settings experience SHALL present each slot primarily as a compact summary object that supports fast scanning, comparison, and health recognition before any deep editing begins. Collapsed presentation SHALL focus on identity and configuration health and SHALL NOT spend list space on instructional prose about how to edit the slot.

#### Scenario: User scans a populated slots list
- **WHEN** the slots page renders multiple configured slots
- **THEN** each slot SHALL show a stable summary including identity, action context, and configuration health without requiring expansion

#### Scenario: Slot has missing required parameters
- **WHEN** a slot is missing one or more required parameters
- **THEN** the collapsed slot summary SHALL expose a visible warning state that communicates incomplete configuration without showing the full form

#### Scenario: Collapsed card avoids redundant guidance
- **WHEN** the slot is otherwise healthy and ready
- **THEN** the collapsed summary SHALL remain concise and SHALL NOT display helper copy whose only purpose is to instruct the user to expand or open full configuration

### Requirement: Inline expansion SHALL remain lightweight
When a user expands a slot from the list page, the inline editing surface SHALL stay limited to high-frequency controls and SHALL NOT become the primary container for long-form parameter authoring. The expanded layout SHALL prefer compact editing rows and lightweight grouping over multiple prominent section headings.

#### Scenario: User expands a simple slot
- **WHEN** a slot with a small quick-edit set is expanded
- **THEN** the expanded content SHALL expose only the designated quick-edit controls and keep the card height materially smaller than a full parameter form

#### Scenario: Slot has extensive parameter guidance
- **WHEN** a slot action includes verbose help text, many fields, or advanced configuration sections
- **THEN** the inline expanded state SHALL omit the long-form guidance and rely on the full-configuration entry point for the deeper authoring flow

#### Scenario: Quick edit avoids extra hierarchy
- **WHEN** related inline controls can be understood through alignment and proximity
- **THEN** the UI SHALL avoid adding summary sections or redundant headings that increase reading overhead without improving task clarity

### Requirement: Slot summaries SHALL communicate parameter state concisely
The slots page SHALL summarize parameter readiness using concise, list-friendly state text instead of reproducing full values or long explanations inside the card.

#### Scenario: Required parameter contains a long file path
- **WHEN** a summary includes a parameter whose raw value is long or noisy
- **THEN** the list card SHALL prefer a concise state-oriented summary such as configured, missing, or a short label rather than the full raw text

#### Scenario: Sensitive parameter is configured
- **WHEN** a parameter is marked as not safe for summary display
- **THEN** the list card SHALL communicate only safe state text and SHALL NOT reveal the underlying value

#### Scenario: User enters active edit mode
- **WHEN** the slot is expanded for quick editing
- **THEN** summary-only tokens or summary blocks SHALL yield to the active editing controls rather than occupying their own editing section
