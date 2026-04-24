## Why

Pulsar's plugin runtime has grown around `PluginRegistry` into a single orchestration point for discovery, activation, execution, lifecycle, circuit breaking, configuration application, and telemetry. That concentration is now the main architectural pressure point for plugin reliability and future extensibility, especially as the codebase experiments with `PluginRegistryV2`, `PluginHost`, permissions, and hot-reload concepts without one stable runtime model.

## What Changes

- Refactor the plugin platform from a registry-centric runtime into an explicit runtime kernel composed of separate catalog, runtime state, execution pipeline, breaker policy, and host responsibilities.
- Define a single lifecycle model for plugin load, enable, disable, execute, fault, and unload transitions so V1 and V2 code paths stop encoding competing semantics.
- Establish a unified execution pipeline that applies availability checks, circuit-breaker policy, activation, execution scoping, exception mapping, and telemetry in one consistent order.
- Move circuit-breaker and execution-policy behavior out of `PluginRegistry` field-level state into dedicated runtime services that are independently testable and observable.
- Preserve existing plugin contracts and persisted configuration while enabling future runtime features such as host isolation, hot reload, permission gates, and richer health diagnostics to compose cleanly.

## Capabilities

### New Capabilities
- `plugin-runtime-kernel`: Defines the runtime architecture, lifecycle state model, execution pipeline ordering, and resilience boundaries for Pulsar plugins.

### Modified Capabilities
- None.

## Impact

- Affected runtime code: `Pulsar/Pulsar/Services/PluginRegistry.cs`, `Pulsar/Pulsar/Services/PluginRegistryV2.cs`, `Pulsar/Pulsar/Core/Plugin/PluginHost.cs`, `Pulsar/Pulsar/Core/Plugin/PluginLoader.cs`, runtime monitoring services, and related DI registration paths.
- Affected tests: plugin runtime, lifecycle, circuit-breaker, and execution-path tests in `Pulsar/Pulsar.Tests/` will need expanded coverage around runtime state transitions and policy composition.
- Affected documentation: `ARCHITECTURE.md`, `Docs/architecture/PLUGIN_SYSTEM.md`, and plugin-development guidance will need to align with the new runtime kernel model.
- Affected platform evolution: this change establishes the internal runtime contract needed for safe adoption of host isolation, runtime unloading, permission enforcement, and other advanced plugin-platform features.
