## Purpose
Define the scheduling contract for deferred startup and non-UI background work so Pulsar can observe, control, and diagnose runtime work outside the foreground path.

## Requirements

### Requirement: Deferred application work SHALL run through a controlled scheduler
The system SHALL execute deferred startup and non-UI background work through a scheduler that can name, prioritize, and observe each work item.

#### Scenario: Startup coordinator schedules deferred warm-up
- **WHEN** Pulsar transitions from blocking startup to deferred warm-up
- **THEN** the startup coordinator SHALL submit deferred work items through the scheduler instead of launching ad hoc background tasks directly

### Requirement: Scheduled background work SHALL support cancellation and failure reporting
The scheduler SHALL allow hosted work to be cancelled during shutdown and SHALL log failures without terminating already-ready core runtime behavior.

#### Scenario: Application exits with deferred work still running
- **WHEN** Pulsar begins shutdown while scheduled background work is still active
- **THEN** the scheduler SHALL signal cancellation to outstanding work and record any unfinished or failed items for diagnostics

### Requirement: Scheduler submissions SHALL support duplicate control for startup work
The scheduler SHALL allow startup-related work items to declare duplicate-control behavior so the same logical warm-up is not started multiple times concurrently.

#### Scenario: Repeated request schedules the same startup task
- **WHEN** a service submits a deferred startup work item that is already in progress
- **THEN** the scheduler SHALL reuse or reject the duplicate submission according to the task's duplicate-control policy

### Requirement: Foreground configuration reads SHALL avoid hidden side-effecting background work
The system SHALL keep foreground configuration and settings discovery reads free of ad hoc background mutation work unless that work is explicitly requested by the calling flow.

#### Scenario: Settings dialog queries discovery data
- **WHEN** a settings-facing dialog requests discovery data for configuration purposes
- **THEN** the system SHALL not implicitly trigger process registration, cache persistence, or unrelated background mutation as a side effect of serving that read
