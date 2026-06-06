## ADDED Requirements

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
