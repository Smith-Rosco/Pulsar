## ADDED Requirements

### Requirement: Wizard display failure produces user-visible notification
The system SHALL produce a user-visible notification when the first-launch setup wizard cannot be displayed, and SHALL continue to normal application startup rather than silently skipping onboarding.

#### Scenario: Wizard dialog fails to open
- **WHEN** the deferred startup attempts to show the first-launch setup wizard and the dialog creation or display throws an exception
- **THEN** the system logs the error, shows a tray notification informing the user that setup could not be displayed, and continues to normal startup

#### Scenario: Wizard dialog succeeds
- **WHEN** the deferred startup successfully shows the first-launch setup wizard
- **THEN** the system proceeds with the normal wizard flow without any tray notification

### Requirement: Tutorial crash does not permanently disable the tutorial
The system SHALL distinguish between tutorial completion by the user and tutorial termination due to an error. A crash SHALL NOT set `HasCompletedTutorial = true`.

#### Scenario: Tutorial encounters an error mid-step
- **WHEN** the tutorial orchestrator catches an exception during step display or transition
- **THEN** the system records the crashed step identifier in a dedicated field and does NOT mark `HasCompletedTutorial` as true

#### Scenario: Tutorial resumes after crash recovery
- **WHEN** the application restarts after a previous tutorial crash
- **THEN** the system detects the crash marker and resumes the tutorial from the crashed step, providing the user an opportunity to continue

#### Scenario: User genuinely completes the tutorial
- **WHEN** the user reaches the final tutorial step and clicks the completion action
- **THEN** the system sets `HasCompletedTutorial = true` and clears any crash marker
