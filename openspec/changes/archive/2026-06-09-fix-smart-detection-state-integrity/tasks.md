## 1. Clarify onboarding state field semantics and invariants

- [x] 1.1 Document the canonical state combinations for `OnboardingState` / `HasCompletedTutorial` / `LastTutorialStep` / `TutorialCrashedAt` / `HasCompletedInitialDetection` in code (XML comments on `ProfileSettings`)
- [x] 1.2 Add a static invariant validation helper in `ConfigService` or `ProfileSettings` that detects illegal state combinations and logs warnings (non-blocking)

## 2. Replace byte-for-byte eligibility check with semantic eligibility

- [x] 2.1 Extract `IsSmartDetectionEligible(ProfilesConfig config)` into `ConfigService` using the fields defined in the design (checks `HasCompletedInitialDetection == false` and compatible lifecycle state)
- [x] 2.2 Replace the `IsExpectedPersistedFallbackAsync` call in `ScheduleSmartDetection` with `IsSmartDetectionEligible` loaded from the latest persisted config
- [x] 2.3 Remove code that snapshots `CreateFallbackConfig()` and serializes it for byte comparison

## 3. Narrow smart detection to patch the latest config

- [x] 3.1 Extract `ApplyDetectionResults(ProfilesConfig config, List<AppDefinition> detectedApps)` into an internal `ConfigService` method that mutates only detection-owned fields and preserves onboarding/tutorial/user state
- [x] 3.2 In `ApplyDetectionResults`, detect which slots match known fallback/default signatures and only replace those slots or fill empty/reserved ones
- [x] 3.3 Update `ScheduleSmartDetection` to load the latest config, call `ApplyDetectionResults` on it, then save the patched config
- [x] 3.4 Remove the full-config generation path in `CreateSmartConfig` that returns a new root `ProfilesConfig` with default `Settings` values; keep detection-only slot generation logic

## 4. Align wizard finish / skip detection semantics

- [x] 4.1 In `FirstLaunchSetupWizardViewModel.Finish`, decide policy: either (a) mark `HasCompletedInitialDetection = true` without scheduling detection, or (b) leave it `false` for a future non-destructive pass. Implement the chosen policy
- [x] 4.2 In `FirstLaunchSetupWizardViewModel.Skip`, ensure `HasCompletedInitialDetection` remains `false` so that `ScheduleSmartDetection` may run
- [x] 4.3 In `FirstLaunchSetupWizardViewModel.CanCloseAsync`, ensure the X-close path also sets `HasCompletedInitialDetection = false` so detection may run
- [x] 4.4 In `OnboardingTemplateService.BuildInitialConfig`, remove `HasCompletedInitialDetection = true` from the generated config; delegate this responsibility to `Finish`'s chosen policy
- [x] 4.5 In `StartupCoordinator.HandleStartupAsync`, only schedule detection when `HasCompletedInitialDetection == false` AND the lifecycle path permits (keep existing guard)

## 5. Update interface and tests

- [x] 5.1 Update `IConfigService` if any new public methods are added (e.g., `ApplyDetectionResults`); keep the change minimal
- [x] 5.2 Write unit tests for `IsSmartDetectionEligible` covering: not-yet-detected, already-detected, skip lifecycle, finish lifecycle, normal startup, and reset
- [x] 5.3 Write unit tests for `ApplyDetectionResults` verifying preservation of `OnboardingState`, `HasCompletedTutorial`, `LastTutorialStep`, `TutorialCrashedAt`, `Language`, theme, input, plugin settings, and user-created profiles
- [x] 5.4 Write unit tests for `StartupCoordinator.HandleStartupAsync` verifying detection scheduling behavior across all startup action paths

## 6. Integration validation

- [x] 6.1 Run `dotnet build` to verify no compilation errors
- [x] 6.2 Run full unit test suite `dotnet test` and fix any regressions introduced by detection changes
- [x] 6.3 Delete `Profiles.json` → launch app → skip wizard → verify smart detection runs AND persisted `OnboardingState` remains `Skipped`
- [x] 6.4 Delete `Profiles.json` → launch app → complete wizard → verify wizard-generated slots persist AND onboarding state is not overwritten
- [x] 6.5 Restart app after tutorial crash → verify tutorial resume works AND `HasCompletedInitialDetection` does not trigger destructive detection
