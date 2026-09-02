## Purpose

Defines automatic injection of sensible default sub-actions when a slot is created, so common slot types ship with useful cascades out of the box.

## Requirements

### Requirement: Slot creation SHALL inject default sub-actions for known types
When a user creates a slot of a known default-bearing type, Pulsar SHALL pre-populate the slot's `SubActions` with the type's default catalog.

#### Scenario: Clipboard-type slot is created
- **WHEN** a user creates a slot for a type with a clipboard/send-keys catalog
- **THEN** the slot SHALL pre-populate sub-actions for common clipboard operations (e.g., cut/copy/paste/select-all equivalents per plugin conventions)

#### Scenario: System-tools slot is created
- **WHEN** a user creates a slot for a type with a system-tools catalog
- **THEN** the slot SHALL pre-populate sub-actions for common system tools (e.g., notepad/calculator/task-manager equivalents)

#### Scenario: Type has no default catalog
- **WHEN** a user creates a slot of a type without a default catalog
- **THEN** the slot SHALL be created with an empty `SubActions` list

### Requirement: Injected defaults SHALL be overridable
Default-injected sub-actions SHALL be editable and removable like user-authored ones; they SHALL NOT be locked.

#### Scenario: User edits an injected default
- **WHEN** the user edits or removes a default-injected sub-action in the editor
- **THEN** the change SHALL be respected and persisted
- **AND** re-creating the slot type SHALL re-inject the defaults afresh (per new creation)

### Requirement: Default injection SHALL NOT disturb existing slots
Injection applies at creation time only; editing an existing slot SHALL NOT inject defaults.

#### Scenario: Editing an existing slot
- **WHEN** the user opens an existing slot for editing
- **THEN** its `SubActions` SHALL remain exactly as persisted (no re-injection)
