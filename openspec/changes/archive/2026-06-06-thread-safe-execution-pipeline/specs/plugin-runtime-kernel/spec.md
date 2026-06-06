## ADDED Requirements

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
