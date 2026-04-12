## Why

Pulsar has started separating blocking startup from deferred work, but the current runtime still pays avoidable cold-start and steady-state costs from eager plugin discovery, scattered background work, and heavyweight context capture. These gaps now constrain startup responsiveness and make later performance improvements harder to implement safely.

## What Changes

- Deepen staged startup so only readiness-critical work remains on the blocking path and all other startup work is scheduled through explicit deferred phases.
- Introduce a lazy plugin runtime model that separates plugin catalog discovery from plugin activation and prevents extension plugins from being instantiated until configuration or execution requires them.
- Add a controlled background work scheduler for deferred startup and fire-and-forget tasks so work can be prioritized, deduplicated, cancelled, and observed.
- Make process context capture lighter by keeping expensive target executable path resolution off the synchronous menu invocation path unless a consumer explicitly needs it.

## Capabilities

### New Capabilities
- `lazy-plugin-runtime-loading`: Discover and cache plugin descriptors separately from live plugin activation so extension plugins are activated on demand.
- `background-work-scheduling`: Schedule deferred and non-UI background work through a controlled queue with prioritization, cancellation, and telemetry.
- `lightweight-context-capture`: Capture only minimal foreground process data synchronously and resolve heavyweight process details lazily.

### Modified Capabilities
- `staged-startup-coordination`: Expand startup staging requirements so deferred work is orchestrated through explicit scheduling phases instead of ad hoc background dispatch.

## Impact

- Affects `Services/AppStartupCoordinator.cs`, `Services/ConfigService.cs`, `Services/ProcessRegistryService.cs`, and startup registration in `App.xaml.cs`.
- Affects plugin runtime code in `Services/PluginRegistry.cs` and `Core/Plugin/PluginLoader.cs`.
- Affects `Core/Plugin/PulsarContext.cs` and any plugin code that consumes `TargetExePath`.
- Requires new DI registrations, new scheduler/runtime services, and updated tests around startup sequencing, plugin activation, and context capture.
