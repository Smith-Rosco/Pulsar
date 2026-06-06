## ADDED Requirements

### Requirement: Feedback SHALL support a launching-in-progress state
The feedback system SHALL define a `Launching` feedback kind that represents an in-progress application launch, distinct from success, failure, and configuration error outcomes.

#### Scenario: Launching feedback is created
- **WHEN** an action begins launching an application as a fallback
- **THEN** the feedback system SHALL produce a notification with kind `Launching`, title "Launching", and a message containing the application name

#### Scenario: Launching feedback is not treated as an error
- **WHEN** the feedback kind is `Launching`
- **THEN** the system SHALL NOT play an error sound or show a warning icon
- **AND** the notification SHALL use an informational icon
