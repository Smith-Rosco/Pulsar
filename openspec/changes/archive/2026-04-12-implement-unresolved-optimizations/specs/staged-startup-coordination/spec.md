## MODIFIED Requirements

### Requirement: Staged Startup Classification
The system SHALL classify startup work into blocking initialization and deferred warm-up so that required runtime readiness is preserved while non-critical work can be postponed, and deferred work SHALL be submitted through explicit startup scheduling rather than ad hoc background dispatch.

#### Scenario: Critical startup work blocks readiness
- **WHEN** a startup responsibility is required before Pulsar can safely provide its core runtime behavior
- **THEN** that responsibility is completed before the application reports itself ready

#### Scenario: Non-critical startup work is deferred
- **WHEN** a startup responsibility is not required for initial runtime correctness
- **THEN** the system may defer it until after the UI shell and core runtime are available

#### Scenario: Deferred startup work is scheduled explicitly
- **WHEN** Pulsar begins deferred warm-up after blocking readiness has completed
- **THEN** each deferred startup responsibility SHALL be enqueued through the startup scheduler with an explicit work identity instead of being launched as anonymous background work

### Requirement: Deferred Startup Failures Are Isolated
The system SHALL isolate deferred initialization failures so they do not prevent already-ready core capabilities from continuing to run, and the runtime SHALL record which deferred startup work item failed.

#### Scenario: Deferred task fails
- **WHEN** a deferred warm-up task throws an exception after startup readiness has been reached
- **THEN** the failure is logged and core runtime capabilities remain available

#### Scenario: Scheduled deferred task fails
- **WHEN** a scheduled deferred startup work item faults
- **THEN** the runtime SHALL record the work item identity and failure details for diagnostics without rolling back ready core services
