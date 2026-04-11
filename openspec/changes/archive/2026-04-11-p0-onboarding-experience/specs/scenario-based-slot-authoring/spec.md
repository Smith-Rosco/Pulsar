## ADDED Requirements

### Requirement: Slot creation defaults to an intent-first flow
The system SHALL let users create common slots by selecting what they want to do before exposing plugin-specific configuration.

#### Scenario: User starts adding a slot
- **WHEN** the user invokes the primary add-slot action in settings
- **THEN** the system presents a scenario-based slot creation flow for supported common intents

### Requirement: Supported intents map to canonical plugin actions
The system SHALL map each supported slot creation intent to an existing canonical plugin and action pair that produces a standard slot configuration.

#### Scenario: User chooses switch app intent
- **WHEN** the user chooses the intent to switch to or launch an app
- **THEN** the system creates a slot configuration backed by the canonical window switching plugin action

#### Scenario: User chooses open target intent
- **WHEN** the user chooses the intent to open a program, file, folder, or URL
- **THEN** the system creates a slot configuration backed by the canonical command runner open action

#### Scenario: User chooses send keys intent
- **WHEN** the user chooses the intent to send keys or insert text
- **THEN** the system creates a slot configuration backed by the canonical command runner send keys action

#### Scenario: User chooses fill credential intent
- **WHEN** the user chooses the intent to fill a saved credential
- **THEN** the system creates a slot configuration backed by the canonical PKI fill action

### Requirement: Advanced editing remains available
The system SHALL preserve access to the existing advanced or plugin-first editing path for cases not covered by the scenario-based flow.

#### Scenario: User needs unsupported slot type
- **WHEN** the user's desired action is not covered by the supported scenario list
- **THEN** the user can switch to an advanced editing path without losing access to full plugin configuration
