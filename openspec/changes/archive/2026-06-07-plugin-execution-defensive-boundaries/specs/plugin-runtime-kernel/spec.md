## MODIFIED Requirements

### Requirement: Plugin execution passes through a deterministic policy pipeline
The runtime SHALL process every plugin execution through a consistent sequence of policy stages that includes availability evaluation, activation readiness, execution scoping, result classification, and telemetry updates. The pipeline SHALL enforce a maximum execution duration (default 30 seconds) and SHALL propagate a `CancellationToken` through all stages.

#### Scenario: Execution ordering is consistent
- **WHEN** a plugin action is requested
- **THEN** the runtime SHALL evaluate enablement and resilience policy before invoking plugin business logic

#### Scenario: Telemetry is recorded from unified execution outcomes
- **WHEN** plugin execution succeeds, returns a handled error result, or throws an exception
- **THEN** the runtime SHALL classify the outcome consistently and SHALL update monitoring and usage services according to the same execution policy rules

#### Scenario: Execution timeout is enforced
- **WHEN** a plugin execution exceeds the configured timeout duration
- **THEN** the runtime SHALL cancel execution via `CancellationToken`
- **AND** the runtime SHALL classify the outcome as a critical blocked execution
- **AND** the runtime SHALL transition the plugin to `Faulted` and update breaker policy

#### Scenario: CancellationToken is propagated to plugins
- **WHEN** the pipeline invokes `plugin.ExecuteAsync()`
- **THEN** the runtime SHALL pass a `CancellationToken` linked to the execution timeout
- **AND** the plugin SHALL receive the token through its `ExecuteAsync` method signature

### Requirement: Circuit breaker state is a first-class runtime policy
The plugin runtime SHALL manage extension-plugin circuit-breaker state through a dedicated runtime policy service rather than registry-local counters and timestamps. The breaker SHALL treat execution timeout as a failure event equivalent to an unhandled exception.

#### Scenario: Breaker policy prevents unsafe execution
- **WHEN** an extension plugin exceeds the configured crash threshold (including timeouts)
- **THEN** the runtime SHALL prevent further execution attempts for the configured cooldown period and SHALL expose the breaker state for diagnostics and user feedback

#### Scenario: Breaker recovery is explicit
- **WHEN** a plugin becomes eligible for retry after cooldown
- **THEN** the runtime SHALL transition through an explicit recovery path instead of silently clearing hidden registry state

#### Scenario: Timeout failure is treated as breaker event
- **WHEN** a plugin execution times out
- **THEN** the breaker policy SHALL record the failure with the same atomic increment as an unhandled exception
- **AND** the timeout SHALL count toward the breaker's crash threshold
