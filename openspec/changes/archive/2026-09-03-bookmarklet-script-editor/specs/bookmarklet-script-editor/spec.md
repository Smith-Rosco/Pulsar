## Purpose

Define the in-app bookmarklet script authoring capability: creating, opening, editing, and saving `.js` web scripts inside Pulsar, validating script content with the same rules the runner enforces, and interpolating run-time parameters, without changing the bookmarklet execution mechanism.

## ADDED Requirements

### Requirement: User can create and edit bookmarklet scripts in-app
The system SHALL provide an in-app editor that lets the user create a new bookmarklet script, open an existing script file, edit its content, and save it to the Pulsar scripts directory.

#### Scenario: Creating a new script
- **WHEN** the user creates a new script in the editor
- **THEN** the editor SHALL open with an empty script template ready for editing

#### Scenario: Saving a script
- **WHEN** the user saves a script with content
- **THEN** the script SHALL be written to the Pulsar scripts directory (`%APPDATA%\Pulsar\Scripts\`) with a `.js` extension
- **AND** the saved file SHALL be selectable through the bookmarklet `run` action's script-file picker

#### Scenario: Opening an existing script
- **WHEN** the user opens an existing script file from the Pulsar scripts directory
- **THEN** the editor SHALL load and display its content for editing

### Requirement: Editor validates script content with the runner's rules
The system SHALL validate script content in the editor using the same preprocessing rules the runner applies (empty-content, BOM handling, `javascript:` prefix handling), surfacing errors and warnings to the user without blocking saving.

#### Scenario: Valid content passes validation
- **WHEN** the user's script content passes the runner's validation rules
- **THEN** the editor SHALL indicate the script is valid with no errors

#### Scenario: Invalid content shows errors
- **WHEN** the user's script content fails validation (e.g. empty content or a rejected construct)
- **THEN** the editor SHALL surface the validation errors/warnings inline
- **AND** the user SHALL still be able to save the script if they choose, with the issues clearly shown

### Requirement: Scripts support run-time parameter interpolation
The system SHALL let a script declare parameter placeholders that are interpolated with per-invocation values when the bookmarklet runs.

#### Scenario: Placeholder is interpolated at run time
- **WHEN** a script contains a declared placeholder and the user runs the bookmarklet with a value for that placeholder
- **THEN** the executed payload SHALL contain the interpolated value in place of the placeholder

#### Scenario: Missing parameter value is reported
- **WHEN** a script declares a placeholder but the invocation does not provide a value
- **THEN** the run SHALL fail with a user-meaningful message identifying the missing value
- **AND** the failure SHALL follow the standard action-feedback path

### Requirement: Editor entry is discoverable from web-script surfaces
The system SHALL expose an entry point to the script editor from the web-scripts/Bookmarklet settings surface so users can author scripts without leaving the app.

#### Scenario: Entry point opens the editor
- **WHEN** the user activates the script-editor entry from the web-scripts surface
- **THEN** the editor SHALL open
- **AND** its UI text SHALL resolve through the localization service (no hardcoded strings)
