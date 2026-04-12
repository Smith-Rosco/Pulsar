## 1. Startup Scheduling

- [x] 1.1 Introduce scheduler services and DI registrations for deferred startup and background work.
- [x] 1.2 Refactor `AppStartupCoordinator` to submit deferred startup responsibilities through the scheduler with explicit task identities.
- [x] 1.3 Migrate startup-related fire-and-forget paths in `ConfigService` and related services onto the scheduler.

## 2. Plugin Runtime Loading

- [x] 2.1 Refactor plugin discovery to cache descriptor results for the current app lifetime without activating extension instances.
- [x] 2.2 Update `PluginRegistry` runtime flow so extension plugin activation remains on demand while preserving execution, configuration, and circuit-breaker behavior.
- [x] 2.3 Add tests covering deferred discovery, descriptor reuse, and first-use activation.

## 3. Lightweight Context Capture

- [x] 3.1 Refactor `PulsarContext` to remove synchronous executable path lookup from capture and add lazy resolution with cached results.
- [x] 3.2 Update runtime and plugin call sites that require executable path access to use the new lazy path access pattern safely.
- [x] 3.3 Add tests or simulator coverage for context capture compatibility and access-denied path resolution.

## 4. Verification

- [x] 4.1 Run startup-focused validation for blocking readiness versus deferred work behavior.
- [x] 4.2 Run `dotnet build Pulsar/Pulsar/Pulsar.csproj` and relevant tests, fixing regressions before completion.
