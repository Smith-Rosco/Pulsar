# ADR-013: Move Circuit-Breaker Side Effects Behind an Observation Seam (State Machine + Events + Adapter)

**Status**: Accepted (2026-09-04)
**Date**: 2026-09-04
**Deciders**: Pulsar Development Team
**Related**: ADR-002 (circuit breaker for extension plugins), ADR-012 (three narrow runtime seams)
**Implementation**: `Core/Plugin/Runtime/PluginRuntimeKernel.cs` (`PluginCircuitBreakerPolicy` + event args), `Services/PluginBreakerNotificationService.cs` (new), `Services/PluginRuntimeServiceCollectionExtensions.cs` (DI), `Services/AppStartupCoordinator.cs` (activation), tests in `Pulsar.Tests/Services/PluginBreakerNotificationServiceTests.cs` and `Pulsar.Tests/Plugin/PluginRuntimeKernelTests.cs`

---

## Context

The architecture review (2026-09-04, candidate D — "strong") found the circuit-breaker policy entangled with side effects that do not belong to a *decision*:

- **`PluginCircuitBreakerPolicy` held three consumer dependencies** it never needed to decide anything: `IPluginHealthMonitor` (telemetry), `ITrayService` (Windows toast), and `ILocalizationService` (message text). Its constructor accepted all three as optional, and `RecordFailure`/`CheckAvailability` *performed* the side effects inline — recording breaker trip/recovery telemetry and popping a localized tray notification from inside the execution pipeline's failure path.
- **The core policy was not unit-testable without UI doubles.** Every pipeline-level test that wanted trip/recovery telemetry had to construct the policy with a health-monitor mock; nothing could test "the breaker opened" without also engaging the notification path.
- **Policy logic and presentation were fused at the statement level.** The trip site interleaved state mutation, a critical log, a telemetry call, and a UI notification in one block — the same seam violation ADR-012 removed from the registry facade, now present one layer deeper.

The governing principle (deep modules, narrow seams, ports & adapters): a state machine should *decide and announce*, never *reach out*. The decision is `Trip`/`Recover`; telemetry recording and user notification are two independent *observers* of that decision.

## Decision

1. **`PluginCircuitBreakerPolicy` becomes a pure state machine.** Its constructor takes only `ILogger<PluginCircuitBreakerPolicy>?` (optional). The `_healthMonitor` / `_trayService` / `_loc` fields are deleted.

2. **State transitions are announced through two events**, raised synchronously at the transition point:
   - `Tripped` → `EventHandler<PluginBreakerTrippedEventArgs>` — payload carries `PluginId` + `Cooldown` (`TimeSpan`, the applied open duration).
   - `Recovered` → `EventHandler<PluginBreakerRecoveredEventArgs>` — payload carries `PluginId`.
   - Event argument types live beside the policy in `Pulsar.Core.Plugin.Runtime`.

3. **One adapter owns the side effects: `PluginBreakerNotificationService`** (new, `Pulsar/Services/`). It subscribes to both events in its constructor and relays:
   - Trip → `IPluginHealthMonitor.RecordCircuitBreakerTrip` **and** `ITrayService.ShowNotification` using the existing localized keys `Plugin.CircuitBreakerTitle` / `Plugin.CircuitBreakerBody` (same strings and `string.Format` shape as before — behavior parity).
   - Recovery → `IPluginHealthMonitor.RecordCircuitBreakerRecovery` only (as before, recovery never notified).

4. **Registration and activation are explicit.** `AddPluginRuntime` registers `PluginBreakerNotificationService` as a singleton next to the policy. `AppStartupCoordinator.RunBlockingInitializationAsync` resolves it once immediately after `trayService.Initialize()` — the subscription happens in the constructor, so resolving the service *is* the activation, and it happens before any plugin can execute and trip a breaker.

5. **Observer handlers are exception-isolated.** Each relay handler wraps its work in try/catch and logs on failure, so an observer fault (e.g. tray unavailable) can never propagate back into the breaker decision or the execution pipeline.

6. **Tests observe through the seam.** Pipeline-level tests that assert trip/recovery telemetry now wire a small event→monitor relay (mirroring the adapter) instead of passing a monitor into the policy constructor. Adapter behavior (telemetry relay, localized tray text, recovery-notifies-nothing, observer exception isolation) is covered by dedicated `PluginBreakerNotificationServiceTests`.

## Considered Options

- **Keep the policy as-is** (minimal churn) — rejected: the review's candidate-D rationale — a decision policy holding UI services — is exactly the coupling ADR-012 eliminated at the registry layer. The notification path also ran inside the pipeline failure path, so any UI hiccup had the blast radius of a plugin crash decision.
- **Inject an `IPluginBreakerNotifier` interface into the policy** (dependency inversion without events) — rejected: an interface the policy *calls* is still a consumer dependency the policy must carry and test with doubles; events let the policy announce without knowing any observer's shape, and multiple observers can subscribe independently.
- **Move only telemetry out, keep tray in the policy** — rejected: it splits one transition into two code paths and leaves UI in the decision class; the tray dependency was the review's headline finding.
- **Make the pipeline own the side effects** — rejected: `PluginExecutionPipeline` is also a decision/execution module; attaching telemetry + UI there would re-create the coupling one level down.

## Consequences

- **The breaker is now a plain state machine**: instantiate with zero arguments, drive `RecordFailure`/`RecordSuccess`/`CheckAvailability`, subscribe to transitions. Unit tests no longer need UI or telemetry doubles to exercise trip/recovery logic.
- **Side effects compose in exactly one place**: `PluginBreakerNotificationService` is the single mapping from "circuit opened/closed" to telemetry + toast; adding a future observer (log sink, analytics event, settings badge) is a new subscriber, not a policy edit.
- **Behavior parity**: same localization keys, same notification title/body/template and error icon, same trip→telemetry and recovery→telemetry ordering. Full suite: 996/996 (baseline 993 + 3 new adapter tests); `dotnet build` at 0 warnings / 0 errors.
- **Activation is now load-bearing**: if `AppStartupCoordinator` ever stops resolving the adapter, trip telemetry and tray toasts silently stop while the breaker itself keeps working. Mitigation: the startup log line "Circuit breaker notification relay activated" makes the activation observable, and the DI registration is colocated with the policy it observes.
- **Lifecycle note**: the policy and the adapter are both container singletons (application lifetime); a singleton subscribing to a singleton's events cannot leak. If the adapter were ever made transient/scoped, constructor subscription would need rework.
- Follow-up (candidate E from the same review, addressed the same day): the unload path's `GC.Collect()`×3 sequence was folded into `PluginLoader.TryUnloadExternalContext`, which now owns the entire collectible-ALC teardown (Unload initiation + forced GC pump) and documents the "caller severs pins, loader completes teardown" split; the kernel only orchestrates reference severing before the call.

---

**Change History**:
- v1.0.0 (2026-09-04): Initial version — implements architecture-review candidate D (circuit-breaker policy held UI services; notifications moved behind an observation seam). Also fixes a seam-injection gap surfaced by the 0-warning gate: `PluginManagerViewModel` declared `IPluginRuntimeOps` but never received it (regression from ADR-012 migration); the constructor now takes and assigns it.
