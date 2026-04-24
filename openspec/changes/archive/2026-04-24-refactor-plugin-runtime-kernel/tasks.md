## 1. Establish runtime-kernel boundaries

- [x] 1.1 Introduce explicit runtime abstractions for plugin catalog, runtime state management, execution pipeline, and breaker policy without changing external plugin contracts.
- [x] 1.2 Define and codify a single plugin lifecycle state model covering load, enable, disable, execute, fault, recovery, and unload transitions.
- [x] 1.3 Add DI wiring and compatibility facades so existing callers can continue using `PluginRegistry` while the new runtime kernel is introduced behind it.

## 2. Extract execution and resilience policy from registry orchestration

- [x] 2.1 Move execution-time availability, activation-readiness, execution-scope, outcome-classification, and telemetry-ordering logic out of `PluginRegistry.ExecuteAsync()` into a deterministic execution pipeline.
- [x] 2.2 Move extension-plugin circuit-breaker counters, cooldown logic, and recovery behavior into a dedicated runtime policy/state service.
- [x] 2.3 Update runtime notifications, usage tracking, and health monitoring integrations to consume unified execution outcomes from the new pipeline.

## 3. Normalize hosting and lifecycle behavior

- [x] 3.1 Adapt `PluginHost` and related activation paths so host responsibilities remain limited to instance creation, isolation, host-local lifecycle bridging, and unload behavior.
- [x] 3.2 Remove duplicated or conflicting lifecycle semantics between `PluginRegistry`, `PluginRegistryV2`, and `PluginHost` so all execution paths follow the same state transitions.
- [x] 3.3 Decide the fate of `PluginRegistryV2` by either folding its unique capabilities into the runtime kernel or retiring duplicated orchestration code after migration.

## 4. Validate and document the new runtime model

- [x] 4.1 Add tests for runtime state transitions, compatibility facade behavior, deterministic execution ordering, and breaker-policy transitions.
- [x] 4.2 Add tests covering success, handled failure, exception, and recovery telemetry behavior through the unified execution pipeline.
- [x] 4.3 Update `ARCHITECTURE.md`, `Docs/architecture/PLUGIN_SYSTEM.md`, and plugin-platform guidance to describe the runtime kernel, lifecycle model, and execution pipeline ordering.
- [x] 4.4 Run targeted plugin-runtime tests and `dotnet build Pulsar/Pulsar/Pulsar.csproj`, resolving regressions before the refactor is considered ready.
