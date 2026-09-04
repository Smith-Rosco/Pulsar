# ADR-017: AppStartupCoordinator 混合注入（删 IServiceProvider 字段）

**Status**: Accepted (2026-09-04)
**Date**: 2026-09-04
**Deciders**: Pulsar Development Team
**Related**: architecture review 2026-09-04 (candidate K, "strong"); ADR-013 (circuit breaker observation seam — must preserve mid-tray-init resolution); `--ui-debug` input-capture invariant at `Services/AppStartupCoordinator.cs:101-109`

## Problem

`AppStartupCoordinator` exposed a 2-method interface (`RunBlockingInitializationAsync`, `StartDeferredInitialization`) but its implementation pulled **24 dependencies through a stored `IServiceProvider` field**. Constructor took 4 args, one of which was the container itself. The module could not be constructed in tests — full仓 had 0 test files referencing `AppStartupCoordinator`. The interface was a leaky seam masquerading as a unit.

Worse, the late-bound resolution silently encoded three real invariants:
- **ADR-013**: `PluginBreakerNotificationService` must be resolved **after** `trayService.Initialize()` (it subscribes to `PluginCircuitBreakerPolicy` events in its ctor).
- **`--ui-debug` input capture**: `GlobalKeyboardHook` (default ctor installs a real low-level keyboard hook) and `IHotkeyService` must not be resolved in a debug run unless `--ui-debug-hooks` is passed.
- **Transient → captive**: `FirstLaunchSetupWizardViewModel` is `AddTransient` (App.xaml.cs:353); injecting it would silently promote it to a singleton inside the coordinator.

A naïve "全部构造注入" rewrite would regress all three invariants simultaneously (full keyboard-hook capture in `--ui-debug`, broken ADR-013 timing, captive transient VM).

## Decision

**Hybrid injection**: A-class dependencies are constructor-injected; B/C-class dependencies are wrapped in `Lazy<T>` or `Func<T>` and injected as such. The `_services` field is deleted entirely.

### Class partition (from the architecture review)

| Tier | Type | Wrapping |
|---|---|---|
| A — eager-safe | `IConfigService`, `DebugModeOptions`, `IPluginRegistry`, `ITrayService`, `IThemeService`, `ILocalizationService`, `Features.Tutorial.Services.StartupCoordinator`, `IDialogService`, `Validation.ConfigValidationPipeline` | direct ctor param |
| B — late-bound, timing-critical | `IProcessRegistryService` (file IO in ctor), `PluginBreakerNotificationService` (ADR-013), `IHotkeyService` + `GlobalKeyboardHook` (native hook), `ITutorialService` (9 deps in ctor) | `Lazy<T>` |
| B — late-bound, WPF-construction-critical | `RadialMenuWindow` (calls `InitializeComponent` + theme init in ctor) | `Func<T>` |
| B — late-bound, transient | `FirstLaunchSetupWizardViewModel` (`AddTransient`) | `Func<T>` |
| C — debug-only | `IDebugStatePublisher`, `IDebugCommandServer` | `Func<T>` registered to throw if invoked outside `IsUiDebug` |

### DI additions

`App.xaml.cs` registers 11 new factories alongside the existing registrations. The two debug factories unconditionally return a no-op lambda in production (so the coordinator's ctor stays free of `if (IsUiDebug)` branches).

## Consequences

Positive:
- Module is now directly constructible with explicit args; tests can mock each dependency.
- All three timing invariants are *enforced by construction*: `Lazy<T>.Value` access happens exactly where the original code resolved the service, preserving order.
- The 24 `_services.GetRequiredService<T>()` calls disappear; the `IServiceProvider` field is gone.
- The transient VM is no longer captive to the coordinator's lifetime.

Negative:
- Constructor parameter count grows from 4 to ~14. Mitigated by the explicit partitioning (comment block at the top of the ctor documents which class each parameter falls into).
- `Lazy<T>` cannot be injected directly by MS.DI without an explicit factory (it requires a parameterless ctor). Each `Lazy<T>` is therefore a one-line factory: `sp => new Lazy<T>(() => sp.GetRequiredService<T>())`.

## Implementation

- `Pulsar/Pulsar/Services/AppStartupCoordinator.cs` (rewritten: 24 service-locator calls → constructor params)
- `Pulsar/Pulsar/App.xaml.cs` (11 new factory registrations)
- Tests: full suite `1031/1031` passing, `0` build warnings added.

## Verification

- `dotnet build Pulsar.sln` → `0 errors`, warning count unchanged from baseline.
- `dotnet test Pulsar.Tests/Pulsar.Tests.csproj` → `1031 / 1031` passing.
- Simulator (`Pulsar.Simulator/Program.cs`) does not depend on `IAppStartupCoordinator`, so no parallel migration needed.