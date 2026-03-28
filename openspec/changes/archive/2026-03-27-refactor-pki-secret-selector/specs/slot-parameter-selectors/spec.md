## ADDED Requirements

### Requirement: Secret picker parameters SHALL render as selector controls
When a slot parameter is backed by a secret picker intent, the system SHALL render that parameter as a selector-style control instead of an editable free-form text input in both add-slot and edit-slot dialogs.

#### Scenario: PKI secret parameter appears in edit dialog
- **WHEN** a user opens slot configuration for a PKI slot with a `secretId` parameter
- **THEN** the parameter editor SHALL show a display-only selector surface and a picker action instead of an editable text box for the raw value

#### Scenario: PKI secret parameter appears in add dialog
- **WHEN** a user configures a new PKI slot during add-slot flow
- **THEN** the parameter editor SHALL use the same selector-style control and SHALL not invite direct entry of the raw `secretId`

### Requirement: Selector controls SHALL separate stored values from displayed metadata
Selector-backed parameters SHALL preserve their stored configuration value while exposing a human-readable display value for the dialog UI. For secret selectors, the stored value SHALL remain the selected `secretId`, while the displayed value SHALL resolve to secret metadata such as label and account when available.

#### Scenario: Existing secret is already selected
- **WHEN** a PKI slot already contains a valid `secretId`
- **THEN** the dialog SHALL show the selected secret's label as the primary display value and SHALL keep the underlying stored argument unchanged

#### Scenario: No secret is selected
- **WHEN** a PKI slot has no configured `secretId`
- **THEN** the dialog SHALL show an explicit empty state such as "No secret selected" rather than a blank editable field

### Requirement: Picker-driven updates SHALL refresh parameter UI immediately
When a picker updates a slot parameter, the system SHALL use a notification-safe mutation path so field display, validation, and summary state refresh within the open dialog without requiring the user to reopen it.

#### Scenario: User selects an existing secret
- **WHEN** the user chooses a secret from the PKI selector dialog
- **THEN** the slot parameter editor SHALL immediately update to show the selected secret's display metadata in the same dialog session

#### Scenario: User creates a new secret from the selector flow
- **WHEN** the user creates and confirms a new secret during PKI selection
- **THEN** the slot parameter editor SHALL immediately show that newly created secret as the selected value without waiting for a full settings save/reload

### Requirement: Secret parameter display SHALL not depend on slot title mutation
The system SHALL display the chosen secret within the parameter editor using secret metadata resolution and SHALL NOT require changing the slot's own label merely to surface the selected secret name.

#### Scenario: User keeps a custom slot label
- **WHEN** a slot already has a custom label that differs from the selected secret's label
- **THEN** selecting a secret SHALL update the parameter selector display while preserving the slot label unless the user explicitly edits it
