## Why

The onboarding flow (setup wizard + interactive tutorial) is the first experience every new Pulsar user sees. Log analysis and code audit revealed multiple failure modes where the wizard silently never appears, background smart detection is killed by a race condition, and the tutorial permanently disables itself on error. These failures are invisible to users and leave no recovery path short of manually editing Profiles.json.

## What Changes

- **Isolate wizard display from deferred warmup exception swallowing** — move the wizard dialog invocation to a dedicated try/catch that provides user-visible fallback (notification) instead of silent skip
- **Fix background smart detection race condition** — prevent the deferred warmup's config save from aborting the scheduled first-launch app detection
- **Fix stale cache reads in OnboardingStateService** — `GetState()` now reads from persisted config rather than the in-memory cache that may never refresh
- **Fix tutorial error handling** — crash during tutorial no longer marks `HasCompletedTutorial = true`, preventing permanent tutorial disable
- **Treat wizard X-close as Skip** — closing the wizard via window chrome now persists `OnboardingState = "Skipped"` instead of leaving "NotStarted"
- **Add re-trigger path** — user can re-launch onboarding from Settings without requiring a full config reset
- **Fix double-layer Task in Dispatcher.InvokeAsync** — make the wizard dialog call explicitly double-awaited for clarity and future-proofing

## Capabilities

### New Capabilities
- `onboarding-error-resilience`: Wizard display failures produce user-visible feedback (tray notification) and allow normal app startup; tutorial crashes do not permanently disable the tutorial
- `onboarding-state-integrity`: Onboarding state machine reads from persisted config; wizard X-close is treated as Skip; background detection is not killed by deferred warmup config save

### Modified Capabilities
- `first-launch-setup-wizard`: Closing the wizard via window chrome (Alt+F4, X button) now persists `OnboardingState = "Skipped"` instead of leaving state as "NotStarted" which causes infinite re-prompting
- `guided-onboarding-tutorial`: Error handling (`HandleErrorAsync`) no longer sets `HasCompletedTutorial = true`; a separate `TutorialCrashedAt` marker preserves the distinction between crash and genuine completion
- `staged-startup-coordination`: Deferred startup failures that affect onboarding-specific work MUST produce user-visible feedback rather than silent log-only suppression

## Impact

- Affected code: `AppStartupCoordinator.cs`, `StartupCoordinator.cs`, `OnboardingState.cs`, `OnboardingStateService`, `TutorialOrchestrator.cs`, `FirstLaunchSetupWizardViewModel.cs`, `ConfigService.cs`
- No breaking API changes; all modifications are internal behavior fixes
- Settings UI needs a new "Restart Onboarding" button (or re-use existing Reset Config with onboarding-only scope)
- No new dependencies
