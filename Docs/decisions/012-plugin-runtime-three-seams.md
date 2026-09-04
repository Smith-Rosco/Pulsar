# ADR-012: Split the Plugin Runtime Facade into Three Narrow Seams (Registration / Execution / Runtime Ops)

**Status**: Accepted (2026-09-04)
**Date**: 2026-09-04
**Deciders**: Pulsar Development Team
**Supersedes**: the shallow aggregation facade `IPluginRegistry` (14 methods) + pass-through `PluginRegistry` class
**Implementation**: `Services/Interfaces/IPluginRegistry.cs` (rewritten), `Services/Interfaces/IPluginExecutor.cs` (new), `Services/Interfaces/IPluginRuntimeOps.cs` (new), `Core/Plugin/Runtime/PluginRuntimeKernel.cs`, `Services/PluginRuntimeServiceCollectionExtensions.cs`, `Services/PluginRegistry.cs` (deleted), plus the consumer migration in `ViewModels/Strategies/*`, `ViewModels/Settings/*`, `Services/ExternalPluginLifecycleOps.cs`, `App.xaml.cs`, `Pulsar.Simulator/Program.cs`

---

## Context

The architecture review (2026-09-04, candidate A) found the plugin runtime's consumer-facing surface was a wide facade:

- **`IPluginRegistry` exposed 14 methods** spanning five unrelated concerns: discovery/loading, activation & query, execution, runtime state mutation, and uninstall/teardown. A consumer that only wanted to run an action (the Slot hot path) was handed an interface that also promised install, uninstall, and unload.
- **A shallow aggregation layer** (`PluginRegistry` class) wrapped the real module (`PluginRuntimeKernel`) and forwarded every call unchanged. It added a file, a DI indirection, and a second place for members to drift, while concentrating no logic — the definition of a shallow module. Its deletion-test symptom: removing the wrapper changed nothing about the kernel's complexity, which is where the complexity already lived.
- **Consumers were coupled to the wrong grain.** The execution strategies (`PluginActionStrategy` etc.) and the external-plugin lifecycle operator (`ExternalPluginLifecycleOps`, committed as b4af4e1) both held the full 14-method interface even though each exercised a disjoint subset. Settings/analytics pages reached runtime-mutation primitives through the same fat registry reference.

The governing principle (deep modules, narrow seams): an interface should name *one job* and be the smallest thing a given caller class needs. Three caller populations exist and each maps to one seam:

| Seam | Consumer population | Methods |
|---|---|---|
| **Registration** (`IPluginRegistry`) | discovery/startup, config validation, recommendation engine, usage read-model, `MenuSession` strategy wiring | load, discover, get descriptor(s), get plugin(s), get-or-activate, is-enabled (8) |
| **Execution** (`IPluginExecutor`) | Slot execution path — `PluginActionStrategy` and its creators | `ExecuteAsync` (1) |
| **Runtime ops** (`IPluginRuntimeOps`) | `ExternalPluginLifecycleOps`, Settings/analytics disable, exit-path unload | refresh discovery, deactivate, set state, grant permissions, unload all (5) |

## Decision

1. **Split the 14-method surface into three narrow seams** — `IPluginRegistry` (registration, 8), `IPluginExecutor` (execution, 1), `IPluginRuntimeOps` (ops, 5) — defined in `Services/Interfaces/`.

2. **Keep one deep implementation: `PluginRuntimeKernel` implements all three interfaces.** No new orchestration layer is introduced; the kernel already owned every operation behind the old facade and continues to. DI registers all three interfaces to the **same `PluginRuntimeKernel` singleton** (`AddPluginRuntime`), so seams are a compile-time contract, not separate service instances.

3. **Delete the pass-through `PluginRegistry` class.** Complexity returns to the kernel, where it already was; nothing is lost but an indirection.

4. **Re-point each consumer to its narrowest seam**:
   - Execution hot path → `IPluginExecutor`: `PluginActionStrategy`, and the providers/strategies that construct it (`CommandPageProvider`, `ProcessPageProvider`, `CascadeSubMenuStrategy`, `SubMenu` entry path).
   - Lifecycle orchestration → `IPluginRuntimeOps` + `IPluginRegistry`: `ExternalPluginLifecycleOps` (ops primitives via `IPluginRuntimeOps`; descriptor/activation queries stay on `IPluginRegistry`).
   - Settings & analytics → `IPluginRuntimeOps` for disable/state (`PluginViewModel`, `PluginManagerViewModel`, `ExternalPluginViewModel`, `ExternalPluginManagerViewModel`, `SettingsAnalyticsPageViewModel`); the analytics read-model keeps `IPluginRegistry` for `GetAllPlugins`.
   - Exit path → `IPluginRuntimeOps.UnloadAllAsync` (`App.OnExit`).
   - Registration-only consumers keep `IPluginRegistry` unchanged: `AppStartupCoordinator`, `ConfigValidationPipeline`, `PluginRecommendationEngine`, `UsageStatsReadModel`, `MenuSession`, `Pulsar.Simulator` (load/discover only; its invocation resolves `IPluginExecutor`).

5. **Tests construct the kernel directly.** `PluginRegistryExecutionTests` / `PluginRegistryCircuitBreakerTests` / `PluginRuntimeLoadingTests` replaced `new PluginRegistry(kernel, catalog, runtimeState)` with the kernel itself; executor/ops expectations moved onto `Mock<IPluginExecutor>` / `Mock<IPluginRuntimeOps>` where the seam boundary demands it.

## Considered Options

- **Keep the facade, add one executor seam** (minimal churn) — rejected: the ops consumers (lifecycle operator, Settings, analytics) would still reach teardown/state mutations through a fat registry; the wrapper class and its drift risk remain. The review's deletion-test logic applies to the whole facade, not just `ExecuteAsync`.
- **Turn each seam into its own class** (three orchestrators over shared state) — rejected: the kernel already concentrates the deep logic; three classes would *add* orchestration layers and shared-state coordination, increasing surface instead of narrowing it. Seams must be interfaces over one implementation.
- **Keep `PluginRegistry` as a thin "facade for DI compatibility"** — rejected: with no DI compatibility to preserve (all consumers migrate in the same change), the wrapper has no reason to exist. Deleting it is the proof that the old shape was shallow.
- **Split by lifecycle phase instead of by consumer** (load / run / unload) — rejected: the consumer populations don't line up with phases; execution and enable/disable both cross phases, so a phase split would leave every consumer holding multiple seams.

## Consequences

- **Narrow interfaces per caller class**: no consumer holds an interface bigger than its job; adding a member to the runtime no longer forces a decision about 14-method compatibility.
- **The execution hot path is decoupled from runtime administration**: strategies cannot accidentally reach install/uninstall/state primitives; the ops surface is only injectable where lifecycle/administration is a real responsibility.
- **One fewer layer**: `PluginRegistry.cs` is gone; the module graph is `kernel` → narrow seams → consumers.
- **Seam boundaries are now test-visible**: `ExternalPluginLifecycleOpsTests` mocks `IPluginRuntimeOps` and `IPluginRegistry` separately, locking the ops-vs-registration split in the sequence assertions.
- **Behavior-neutral**: full test suite passes unchanged (993/993); `dotnet build` stays at 0 warnings / 0 errors.
- Follow-up (explicitly out of scope): the roadmap's "renderer pluginization" (`IPluginRegistry`-hosted third-party renderers) remains a future change and now has a narrower registration surface to build against.

---

**Change History**:
- v1.0.0 (2026-09-04): Initial version — implements architecture-review candidate A (facade too wide); candidate B (external-plugin lifecycle timing, commit b4af4e1) shipped separately.
