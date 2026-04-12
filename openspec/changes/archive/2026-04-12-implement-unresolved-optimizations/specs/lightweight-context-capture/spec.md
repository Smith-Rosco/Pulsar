## ADDED Requirements

### Requirement: Context capture SHALL keep synchronous foreground capture lightweight
The system SHALL capture only readiness-critical foreground context synchronously during radial menu invocation.

#### Scenario: Radial menu captures foreground context
- **WHEN** Pulsar captures the target foreground context while opening the radial menu
- **THEN** it SHALL synchronously capture window handle, process id, and process name without requiring executable path resolution

### Requirement: Executable path resolution SHALL be lazy and fault-tolerant
The runtime SHALL resolve the target executable path only when a consumer requests it and SHALL tolerate access-denied or process-race failures without failing the entire context.

#### Scenario: Plugin requests target executable path
- **WHEN** a plugin explicitly requests the target executable path from the context
- **THEN** the runtime SHALL resolve it on demand and return an empty or failed-safe result if the path cannot be read

### Requirement: Lazy executable path resolution SHALL be stable within a captured context
Once a captured context resolves the target executable path, the runtime SHALL reuse the resolved value for subsequent requests within the same context instance.

#### Scenario: Multiple consumers request executable path
- **WHEN** the same captured context receives more than one executable path request
- **THEN** the runtime SHALL reuse the first resolved value instead of repeating process module access for each request
