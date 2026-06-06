## Purpose
Define the context-capture contract so radial menu invocation remains lightweight while executable path lookup is deferred and stable within a captured context.
## Requirements
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

### Requirement: PulsarContext SHALL be immutable after construction
The `PulsarContext` instance SHALL have no mutable properties, fields with setters (including `internal set`), or methods that modify its state after the private constructor completes. All data SHALL be set exclusively during construction.

#### Scenario: Context fields cannot be reassigned
- **WHEN** code attempts to set any property or field on a `PulsarContext` instance after construction
- **THEN** the operation SHALL fail at compile time (no accessible setter or mutable field)

#### Scenario: Per-execution data lives in execution scope
- **WHEN** the plugin execution pipeline needs to associate the current plugin ID or permission interceptor with an execution
- **THEN** that data SHALL be stored in `PluginExecutionContext` (the `AsyncLocal`-based execution scope) rather than on `PulsarContext`

#### Scenario: Multiple executions share the same context snapshot safely
- **WHEN** the same `PulsarContext` instance is passed to two sequential plugin executions
- **THEN** the second execution SHALL NOT observe any state written by the first execution (no lingering `CurrentPluginId` or `PermissionInterceptor`)

