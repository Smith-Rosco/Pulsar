## Context

Pulsar's onboarding system consists of two phases:

1. **First-Launch Setup Wizard** — a modal dialog (`FirstLaunchSetupWizardViewModel`) that lets users pick a usage profile and common apps, then generates an initial `Profiles.json`. Triggered by `StartupCoordinator.HandleStartupAsync()` during deferred initialization.
2. **Interactive Tutorial** — a 6-step overlay tutorial (`TutorialOrchestrator`) that guides users through Switch Mode and Command Mode. Auto-launched after the wizard completes.

The decision to show these is governed by three independent config fields: `OnboardingState`, `HasCompletedTutorial`, and `LastTutorialStep`.

Log analysis (pulsar-20260607.log) and code audit identified six distinct failure modes. This design addresses all six while keeping changes minimal and non-breaking.

## Goals / Non-Goals

**Goals:**
- Wizard display failures produce user-visible notification instead of silent skip
- Background smart detection (auto-discover installed apps) is not killed by the deferred warmup's config save
- `OnboardingStateService.GetState()` reads persisted state, not stale cache
- Tutorial crashes do not permanently disable the tutorial
- Wizard X-close is treated as explicit Skip
- Users can re-trigger onboarding from Settings

**Non-Goals:**
- Rewriting the onboarding state machine
- Changing the tutorial step content or UX flow
- Adding a new dependency or framework
- Modifying the `BackgroundWorkScheduler` itself

## Decisions

### Decision 1: Use `ITrayService.ShowNotification` for wizard failure fallback

**Choice**: When `ShowCustomAsync` fails inside `RunOnboardingStartupAsync`, catch the exception there (not in the outer deferred warmup handler) and show a tray notification telling the user onboarding could not start, then proceed with normal startup.

**Alternatives considered**:
- **Dialog**: Would compound the failure — second dialog might also fail. Better to use tray notification which has no XAML/XAML-resource dependency.
- **Log only**: Current behavior, proven problematic. Users never check logs.

**Rationale**: Tray notification is the only UI channel already initialized and proven reliable at this point in startup (tray service initialized in blocking init phase).

### Decision 2: Defer background detection until AFTER wizard completion

**Choice**: Move the "smart detection" scheduling from `CreateDefaultConfig` to the wizard's `Skip`/`Finish` handlers and the `HandleStartupAsync` NormalStartup path. The background detection now only runs when the user has either completed the wizard or explicitly skipped it.

**Alternatives considered**:
- **Mutex/lock**: Adding a synchronization primitive between background detection and wizard would complexify the config service. Better to change the scheduling point.
- **Compare checksum instead of full serialization**: The current `IsExpectedPersistedFallbackAsync` approach is fragile. Moving scheduling eliminates the race entirely.

**Rationale**: The deferred warmup always saves config (`AppStartupCoordicator.cs:97-99`) before the wizard dialog, which changes the file. There is no safe window to run detection during wizard display. Post-wizard is the correct time.

### Decision 3: Add `forceReload` parameter to `IConfigService.LoadAsync`

**Choice**: Add `Task<ProfilesConfig> LoadAsync(bool forceReload = false)` to `IConfigService`. When `forceReload = true`, bypass the cache and read from disk. Use this in `OnboardingStateService.GetState()`.

**Alternatives considered**:
- **Switch GetState to use `LoadAsync()` without cache bypass**: `LoadAsync` currently short-circuits on non-null cache, so this wouldn't help.
- **Invalidate cache on every write**: Too aggressive, would hurt performance of frequent reads in hot paths.

**Rationale**: A targeted `forceReload` flag is minimal and backwards-compatible (default `false` preserves existing behavior). Only onboarding state reads need the forced reload.

### Decision 4: Add `TutorialCrashedAt` field instead of reusing `HasCompletedTutorial`

**Choice**: Add a nullable `string? TutorialCrashedAt` to `ProfileSettings` to record the step ID where a crash occurred. `HandleErrorAsync` sets this field and does NOT set `HasCompletedTutorial`. `CheckResumeAsync` checks for `TutorialCrashedAt` to offer resume from crash.

**Alternatives considered**:
- **Just remove the `HasCompletedTutorial = true` line**: Simplest fix, but loses crash diagnostics. Adding a field preserves useful info.
- **Use `LastTutorialStep` with a prefix**: Fragile parse-and-compare logic. Separate field is clearer.

**Rationale**: `HasCompletedTutorial` is user-facing semantics (the user genuinely completed it). Crash should be a distinct state that enables resume.

### Decision 5: Override `CanCloseAsync` to call Skip when result is `None`

**Choice**: In `FirstLaunchSetupWizardViewModel.CanCloseAsync`, when `result == DialogResult.None` (X button or Alt+F4), call `MarkOnboardingSkippedAsync()` before returning true.

**Rationale**: "NotStarted" with no wizard showing is an ambiguous state — next launch re-triggers the wizard, which is confusing. Treating X-close as explicit Skip gives the user clear expectations.

## Risks / Trade-offs

- **[Risk] Background detection moved to post-wizard means first-launch config is always fallback until user finishes or skips wizard** → Acceptable trade-off. The wizard-generated config replaces the fallback anyway. Users who skip get smart detection immediately after.
- **[Risk] `forceReload` bypasses cache which adds disk I/O to onboarding flow** → Onboarding reads are infrequent (once per startup), so negligible perf impact.
- **[Risk] `TutorialCrashedAt` adds a new field to Profiles.json** → Backwards-compatible — old configs without the field work fine (null = no crash).
- **[Risk] Tray notification may not be visible if user's system suppresses notifications** → This is the best available fallback channel at this point in startup. We also still log the error.
