## Purpose

The plugin runtime kernel provides a decomposed, DI-composed set of services that separate plugin discovery, runtime state management, execution policy, circuit breaking, and instance hosting into distinct responsibilities, with thread-safe state stores and deterministic execution pipelines.

## Requirements

### Requirement: Plugin runtime responsibilities are explicitly separated
The plugin platform SHALL separate plugin discovery, runtime state management, execution policy, and instance hosting into distinct runtime responsibilities so no single registry class remains the authoritative owner of all plugin behavior.

#### Scenario: Catalog and runtime concerns are separated
- **WHEN** the application discovers plugins and prepares them for execution
- **THEN** descriptor discovery and metadata ordering SHALL be handled independently from runtime activation and execution policy

#### Scenario: Hosting and policy concerns are separated
- **WHEN** a plugin instance is loaded or unloaded
- **THEN** instance-hosting behavior SHALL be managed independently from circuit breaking, telemetry, and user enablement policy

### Requirement: Plugin lifecycle semantics are centrally defined
The plugin runtime SHALL define one authoritative lifecycle state model for plugin load, enable, disable, execute, fault, recover, and unload transitions, and all runtime execution paths MUST follow that model.

#### Scenario: Enable semantics remain consistent across runtime paths
- **WHEN** a plugin is activated through any supported runtime path
- **THEN** the runtime SHALL apply the same lifecycle transition rules and SHALL NOT allow competing implementations to invoke lifecycle hooks with different semantics

#### Scenario: Faulted plugins enter a defined state
- **WHEN** plugin activation or execution fails with an unhandled exception
- **THEN** the runtime SHALL record the failure against the plugin lifecycle state and SHALL expose a deterministic next-step policy for recovery, disablement, or unload

### Requirement: Plugin execution passes through a deterministic policy pipeline
The runtime SHALL process every plugin execution through a consistent sequence of policy stages that includes availability evaluation, activation readiness, execution scoping, result classification, and telemetry updates.

#### Scenario: Execution ordering is consistent
- **WHEN** a plugin action is requested
- **THEN** the runtime SHALL evaluate enablement and resilience policy before invoking plugin business logic

#### Scenario: Telemetry is recorded from unified execution outcomes
- **WHEN** plugin execution succeeds, returns a handled error result, or throws an exception
- **THEN** the runtime SHALL classify the outcome consistently and SHALL update monitoring and usage services according to the same execution policy rules

### Requirement: Circuit breaker state is a first-class runtime policy
The plugin runtime SHALL manage extension-plugin circuit-breaker state through a dedicated runtime policy service rather than registry-local counters and timestamps.

#### Scenario: Breaker policy prevents unsafe execution
- **WHEN** an extension plugin exceeds the configured crash threshold
- **THEN** the runtime SHALL prevent further execution attempts for the configured cooldown period and SHALL expose the breaker state for diagnostics and user feedback

#### Scenario: Breaker recovery is explicit
- **WHEN** a plugin becomes eligible for retry after cooldown
- **THEN** the runtime SHALL transition through an explicit recovery path instead of silently clearing hidden registry state

### Requirement: Compatibility facades preserve current plugin contracts during migration
The runtime refactor SHALL preserve existing plugin-facing contracts, persisted plugin configuration, and current registry entry points while the internal runtime kernel is introduced.

#### Scenario: Existing plugin IDs and actions remain valid
- **WHEN** the runtime kernel replaces registry-owned orchestration internally
- **THEN** existing plugin IDs, action names, and persisted plugin profile references SHALL continue to execute without requiring configuration migration

#### Scenario: Existing callers continue to use registry entry points
- **WHEN** application code invokes current plugin-registry APIs during migration
- **THEN** those calls SHALL continue to function through a compatibility facade over the new runtime kernel

### Requirement: Runtime kernel components SHALL be composed via dependency injection
The plugin runtime kernel, catalog, state store, breaker policy, and execution pipeline SHALL be registered as DI services and composed through constructor injection rather than manual instantiation within a facade.

#### Scenario: Each runtime component is independently replaceable
- **WHEN** a unit test provides a mock `IPluginBreakerPolicy` to the DI container
- **THEN** the execution pipeline SHALL use the mock implementation without requiring changes to any production code

#### Scenario: PluginRegistry receives components through constructor injection
- **WHEN** the DI container resolves `IPluginRegistry`
- **THEN** the `PluginRegistry` implementation SHALL receive all runtime dependencies (catalog, state store, breaker, pipeline, kernel) via constructor parameters rather than calling `new` internally

#### Scenario: Runtime components share the same DI service provider
- **WHEN** a runtime component needs an optional service (e.g., `IPluginHealthMonitor`)
- **THEN** it SHALL receive that service through constructor injection rather than calling `IServiceProvider.GetService()` internally
