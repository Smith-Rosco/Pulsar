# ADR-018: 首次启动判定走 OnboardingStateService 读模型（自愈语义）

**Status**: Accepted (2026-09-04)
**Date**: 2026-09-04
**Deciders**: Pulsar Development Team
**Related**: architecture review 2026-09-04 (candidate I); ADR-013 (PluginBreakerNotificationService activation timing); ADR-017 (AppStartupCoordinator hybrid injection — this ADR relies on the explicit constructor surface to add `IOnboardingStateService` without lazy-wrapping); `ProfilesConfig.cs:354-357` (documented illegal invariant `OnboardingState='Complete' ∧ HasCompletedTutorial=false`); `Features/Tutorial/Services/OnboardingState.cs:35-49` (read-model projection).

## Problem

`AppStartupCoordinator.StartDeferredInitialization` opened `Profiles.json` twice — once to drive configuration, once to inline a 3-way first-launch decision at `Services/AppStartupCoordinator.cs:241-246`:

```csharp
var config = await _configService.LoadSnapshotAsync();
if (config.Settings.HasCompletedTutorial
    || string.Equals(config.Settings.LastTutorialStep, "Skipped", StringComparison.OrdinalIgnoreCase)
    || !string.Equals(config.Settings.OnboardingState, "SetupWizardComplete", StringComparison.OrdinalIgnoreCase))
{
    return;
}
```

Three smells:

1. **Read-end leaks write-end vocabulary.** `OnboardingState` is owned by `IOnboardingStateService` (which exposes `MarkOnboardingSkippedAsync` / `MarkSetupCompletedAsync` / `MarkTutorialCompletedAsync` — every writer). The reader here knows three of the four valid string literals (`"NotStarted"` / `"Skipped"` / `"Complete"` are implicitly encoded by the three OR-branches), and re-derives the same flags from raw config. The mapping is duplicated, and any new `OnboardingState` value (or any writer that changes the canonical string) silently breaks this check.
2. **Illegal combinations are silently destructive.** `ProfilesConfig.cs:354-357` documents `OnboardingState='Complete' ∧ HasCompletedTutorial=false` as illegal. The inline check, however, **enters the tutorial** in that state (because the third branch is false, so the AND collapses to false, so we don't return). The illegal invariant — which should never exist — produces user-visible re-onboarding. `IOnboardingStateService.GetStateAsync()`'s projection is already self-healing on this combination (it surfaces `HasCompletedSetup=true` regardless of `HasCompletedTutorial`), because its only mandate is the 4-value → 5-flag mapping.
3. **Coupling between read paths.** `Features/Tutorial/Services/StartupCoordinator.cs` already uses `IOnboardingStateService.HasCompletedSetup` for its own wizard decision. The two start-up coordinators now disagree on the *kind* of read they do for the same concept (one reads `OnboardingState` directly, one reads via the service).

## Decision

Replace the inline check with a single call to `IOnboardingStateService.GetStateAsync()` and let the projection's flags drive the decision:

```csharp
var onboardingState = await _onboardingStateService.GetStateAsync();
if (onboardingState.HasCompletedTutorial
    || onboardingState.HasSkippedTutorial
    || !onboardingState.HasCompletedSetup)
{
    return;
}
```

The new check is **stricter on the return path** (because `HasCompletedSetup` is true for both `SetupWizardComplete` and `Complete`) and **more permissive on the illegal invariant** (because `Complete + HasCompletedTutorial=false` now returns instead of re-onboarding — the user's expected intent is "already complete").

This is the **self-healing** variant explicitly chosen over the byte-equivalent variant (adding a narrow `IsSetupWizardComplete` member). The user's preference was recorded at the 2026-09-04 candidate-I review: prefer projection-level tolerance over leaking the 4-value vocabulary at every read site.

### Why `IOnboardingStateService` is the right seam

- **Already exists** as `AddSingleton<IOnboardingStateService, OnboardingStateService>` in `App.xaml.cs:237`.
- **Already a singleton** — no need for `Lazy<T>` wrapping (the read-only projection has no constructor side effects beyond `_configService.LoadSnapshotAsync(forceReload: true)`; the latter is hit only at the deferred-init point, not at coordinator construction).
- **Has 5 already-tested bool outputs** — `OnboardingVerificationTests.cs` already covers `IsFirstRun` and `HasSkippedOnboarding` for `NotStarted` / `Skipped` / edited-file sequences. The new tests in `OnboardingVerificationTests.cs` add the **missing lock-downs** on `HasCompletedSetup` for `SetupWizardComplete` / `Complete`, the illegal-combination projection, and the `LastTutorialStep='Skipped'` → `HasSkippedTutorial=true` mapping.

### What changes for the user

Normal path (legal `Profiles.json` state): no behaviour change. The 6 legal combinations in `ProfilesConfig.cs:267-273` map onto the 3 return-conditions identically:

| `OnboardingState` | `HasCompletedTutorial` | `LastTutorialStep` | Old behaviour | New behaviour |
|---|---|---|---|---|
| `NotStarted` | false | null | return | return |
| `Skipped` | false | null | return | return |
| `SetupWizardComplete` | false | null / step-id | **enter tutorial** | **enter tutorial** |
| `SetupWizardComplete` | false | `"Skipped"` | return (branch 2) | return (`HasSkippedTutorial`) |
| `Complete` | true | null | return (branch 1) | return (`HasCompletedTutorial`) |
| `Complete` | **false** | null | **enter tutorial** ⚠ | return ✅ (self-heal) |

The 7th state (illegal `Complete + HasCompletedTutorial=false`) is the only behavioural delta — and it goes from a user-visible bug (re-running onboarding on a profile that looks Complete) to the right answer.

## Consequences

Positive:
- Read-end no longer knows about `OnboardingState` string literals; the 4-value vocabulary is fully encapsulated in `OnboardingStateService`.
- Illegal-config behaviour is now consistent with `Features.Tutorial.Services.StartupCoordinator` (which has always used the service-mediated read).
- One fewer `LoadSnapshotAsync()` call in `StartDeferredInitialization` — the projection already does its own `forceReload: true` read.

Negative:
- **Behaviour delta on the illegal combination**, intentionally. The previous code re-entered onboarding; the new code returns. If a user has the illegal config, they will no longer see the tutorial — but the illegal config is also the one that `ValidateOnboardingInvariants` is supposed to log a `[ConfigInvariants]` warning for at startup. The mitigation is: ensure `ValidateOnboardingInvariants` is wired into the config load path (it's currently a public static method on `ProfileSettings`, called nowhere — this is a separate gap, out of scope for ADR-018).
- One extra `IOnboardingStateService` parameter on `AppStartupCoordinator`'s ctor (now 14 explicit args total, plus 8 Lazy/Func). The parameter list is already long; this adds one more, mitigated by the comment block at the top of the ctor that partitions by class.

## Implementation

- `Pulsar/Pulsar/Services/AppStartupCoordinator.cs` — add `IOnboardingStateService onboardingStateService` ctor parameter; replace the 3-line inline check with the projected-flag check above.
- `Pulsar/Pulsar.Tests/Tutorial/OnboardingVerificationTests.cs` — 4 new tests covering: `HasCompletedSetup` for `SetupWizardComplete`/`Complete`, the illegal-combination self-heal, `HasSkippedTutorial` for `LastTutorialStep="Skipped"`, and the unconditional short-circuit on `OnboardingState='Complete'` regardless of `HasCompletedTutorial`.
- No `App.xaml.cs` change — `IOnboardingStateService` is already registered as singleton.
- No new seam introduced — `IOnboardingStateService` was the existing read seam.

## Verification

- `dotnet build Pulsar.sln` → 0 errors, warning count unchanged from baseline.
- `dotnet test Pulsar.Tests/Pulsar.Tests.csproj` → 1031 + 6 (4 new tests; one of them is a `[Theory]` with 2 InlineData rows) = **1037 / 1037** passing.
- The `--ui-debug` startup path is exercised by `Pulsar.E2E/Workflows/radial-menu-open-via-command.json` (no hotkeys, command-driven). The FirstLaunch wizard decision is *not* on the menu-open hot path, but `Fixture/default-profiles.json` (which has `OnboardingState="Complete"`) confirms the new self-heal — the wizard does not re-appear when an E2E run uses that fixture.

## Amendment (2026-09-04, same day): conformance fix — the self-heal now actually heals

Writing the first coordinator-level unit tests for this gate (candidate K coverage, `AppStartupCoordinatorTests.cs`) exposed that the original implementation did **not** deliver the table above. Trace, with three independent sources:

1. **The gate** returns on `HasCompletedTutorial || HasSkippedTutorial || !HasCompletedSetup`. `HasCompletedSetup=true` is a *precondition for running the tutorial* (the `SetupWizardComplete` row), not a return trigger.
2. **The original projection** passed `HasCompletedTutorial` through literally. For the illegal `Complete + HasCompletedTutorial=false` combination the projected flags were therefore `(true, false, false)` — identical to the legal `SetupWizardComplete` row — so the gate **entered the tutorial**, the exact bug this ADR claims to fix. (The old inline check actually *returned* on that combination via its third branch `OnboardingState != "SetupWizardComplete"`; the ADR's problem statement inverted that branch. The net effect of the original refactor was a regression on the illegal combination, not a heal.)
3. **The first-generation lock test** (`OnboardingVerificationTests`) asserted `HasCompletedSetup=true "so AppStartupCoordinator returns"` while simultaneously locking `HasCompletedTutorial` as a literal passthrough ("never silently coerced") — internally inconsistent with the gate's actual semantics, and it passed only because the coordinator gate was never exercised at that level.

### Fix (projection-level tolerance, per the approved variant A)

`OnboardingStateService.GetStateAsync` now heals the terminal state:

```csharp
HasCompletedTutorial = config.Settings.HasCompletedTutorial
    || string.Equals(onboardingState, "Complete", StringComparison.OrdinalIgnoreCase),
```

Rationale: `MarkTutorialCompletedAsync` always writes `OnboardingState="Complete"` **and** `HasCompletedTutorial=true` together; a profile carrying `Complete` + `HasCompletedTutorial=false` is a corrupt or half-written write, and "Complete" is terminal by definition. The illegal combination now projects to `(HasCompletedSetup=true, HasCompletedTutorial=true, HasSkippedTutorial=false)` → the gate returns. All six legal combinations are unchanged (`SetupWizardComplete` is the only state where the raw flag passes through, and the only state where the tutorial may run). The 4-value vocabulary still never leaks to read sites. The replacement lock test (`OnboardingStateService_GetStateAsync_WithIllegalCompleteState_ShouldHealCompletedTutorial`) asserts the healed flag, and the new `StartDeferred_IllegalCombination_SkipsTutorialPath` exercises the coordinator's consumption of the post-heal projection end-to-end (up to the tutorial Lazy staying unresolved).

Consumers audited: the projected `OnboardingState.HasCompletedTutorial` has exactly one consumer (`AppStartupCoordinator`'s gate); `Features.Tutorial.Services.StartupCoordinator` reads only `IsFirstRun` / `HasSkippedOnboarding` / `HasCompletedSetup`. All other `HasCompletedTutorial` references in the codebase read `config.Settings.HasCompletedTutorial` (the raw field), which is untouched.