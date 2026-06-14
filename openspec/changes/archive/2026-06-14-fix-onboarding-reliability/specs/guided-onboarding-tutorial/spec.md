## MODIFIED Requirements

### Requirement: Tutorial progress survives app restarts
The system SHALL persist onboarding tutorial state so that incomplete progress, completed milestones, explicit skip decisions, and crash recovery state survive application restart.

#### Scenario: Incomplete tutorial resumes after restart
- **WHEN** the user exits the application after starting but not completing the tutorial
- **THEN** the system resumes from the saved tutorial state instead of restarting from the first step

#### Scenario: Crashed tutorial resumes after restart
- **WHEN** the user restarts the application after a previous tutorial session terminated due to an error
- **THEN** the system detects the crash marker and resumes the tutorial from the crashed step

#### Scenario: Tutorial recovery without crash marker
- **WHEN** the application starts and the tutorial was previously terminated without a crash marker and without completion
- **THEN** the system resumes from the last saved step as normal

## ADDED Requirements

### Requirement: Tutorial crash state is distinct from completion state
The system SHALL record tutorial crashes in a dedicated field separate from the completion flag, and SHALL NOT mark the tutorial as completed when an error terminates the session.

#### Scenario: Tutorial error terminates session
- **WHEN** the tutorial orchestrator catches an unhandled exception during step display or transition
- **THEN** the system records the current step identifier in a crash state field and does NOT set the tutorial completion flag

#### Scenario: Tutorial gracefully shuts down after error
- **WHEN** error handling completes after a tutorial crash
- **THEN** the overlay window is closed and the tutorial is not marked as active, but the tutorial remains eligible for automatic resume on next launch
