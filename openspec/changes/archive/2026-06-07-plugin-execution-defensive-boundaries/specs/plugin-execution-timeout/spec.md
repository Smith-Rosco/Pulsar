## ADDED Requirements

### Requirement: Plugin execution SHALL have a hard timeout
The plugin execution pipeline SHALL enforce a maximum execution duration of 30 seconds per plugin invocation. If a plugin's `ExecuteAsync` does not complete within this duration, the pipeline SHALL cancel execution via the provided `CancellationToken` and classify the outcome as a critical fault.

#### Scenario: Normal execution completes within timeout
- **WHEN** a plugin executes and returns a `PluginResult` within 30 seconds
- **THEN** the pipeline SHALL process the result normally and SHALL NOT trigger any breaker or fault state

#### Scenario: Hung plugin triggers timeout
- **WHEN** a plugin's `ExecuteAsync` does not complete within 30 seconds
- **THEN** the execution SHALL be cancelled via `CancellationToken`
- **AND** the pipeline SHALL transition the plugin to `Faulted` state
- **AND** the pipeline SHALL record a breaker failure with a `TimeoutException`
- **AND** the pipeline SHALL return a `PluginExecutionOutcome` with `Kind = Blocked` and `Result.Severity = Critical`

#### Scenario: Timeout is linked to caller cancellation
- **WHEN** a caller provides a cancellation token that fires before the 30-second timeout
- **THEN** the plugin execution SHALL be cancelled using the earliest-firing token source

### Requirement: Plugins SHALL accept a CancellationToken
The `IPulsarPlugin.ExecuteAsync` method SHALL accept a `CancellationToken` parameter. Plugin implementations SHALL forward this token to any cancellable async operations they perform and SHALL respect cancellation by throwing `OperationCanceledException` or returning an error result.

#### Scenario: Plugin forwards token to async operations
- **WHEN** a plugin calls `Task.Delay`, HTTP requests, or other cancellable async operations
- **THEN** the plugin SHALL pass the received `CancellationToken` to those operations

#### Scenario: Plugin without cancellable operations compiles unchanged
- **WHEN** a plugin implementation adds the `CancellationToken` parameter but does not use it internally
- **THEN** the plugin SHALL compile and execute correctly with the default parameter value

### Requirement: Execution timeout SHALL trigger Circuit Breaker
When an extension plugin's execution times out, the Circuit Breaker policy SHALL treat the timeout as a failure event equivalent to an unhandled exception, incrementing the failure counter and potentially opening the breaker.

#### Scenario: Timeout increments breaker failure count
- **WHEN** an extension plugin times out
- **THEN** the breaker policy SHALL atomically increment the failure counter for that plugin

#### Scenario: Breaker opens after repeated timeouts
- **WHEN** an extension plugin times out 3 times within 1 minute
- **THEN** the breaker SHALL open and block further execution for 60 seconds
- **AND** the user SHALL receive a tray notification about the disabled plugin

#### Scenario: Core plugins are not breaker-protected on timeout
- **WHEN** a core plugin times out
- **THEN** the runtime SHALL transition it to `Faulted` state
- **BUT** the breaker SHALL NOT open (core plugins have no breaker protection)
