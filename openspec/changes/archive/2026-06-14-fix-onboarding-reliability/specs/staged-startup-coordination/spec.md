## MODIFIED Requirements

### Requirement: Deferred Startup Failures Are Isolated
The system SHALL isolate deferred initialization failures so they do not prevent already-ready core capabilities from continuing to run, and the runtime SHALL record which deferred startup work item failed. Failures in onboarding-specific deferred work SHALL produce user-visible feedback in addition to log entries.

#### Scenario: Deferred task fails
- **WHEN** a deferred warm-up task throws an exception after startup readiness has been reached
- **THEN** the failure is logged and core runtime capabilities remain available

#### Scenario: Onboarding deferred task fails
- **WHEN** the deferred work item responsible for displaying the first-launch setup wizard throws an exception
- **THEN** the system SHALL show a tray notification to the user in addition to logging the failure, and SHALL continue normal startup

#### Scenario: Scheduled deferred task fails
- **WHEN** a scheduled deferred startup work item faults
- **THEN** the runtime SHALL record the work item identity and failure details for diagnostics without rolling back ready core services
