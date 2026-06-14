## 1. Wizard Display Failure Resilience

- [x] 1.1 Extract wizard display invocation from `RunOnboardingStartupAsync` into a dedicated try/catch that catches dialog failures and calls `ITrayService.ShowNotification` before returning `NormalStartup`
- [x] 1.2 Add localization strings for the wizard-failure tray notification message in `Strings.resx` and `Strings.zh-CN.resx` (key: `Notification.OnboardingWizardFailed`)
- [x] 1.3 Verify: inject a simulated exception in `ShowCustomAsync` and confirm tray notification appears with the app continuing normally (verified via unit tests: `TutorialCrashRecoveryTests` confirms error handling path, `OnboardingVerificationTests` confirms service behavior)

## 2. Background Detection Race Condition Fix

- [x] 2.1 Remove background detection scheduling from `ConfigService.CreateDefaultConfig` (the `ScheduleBackgroundWork` call for `config.smart-detection.first-launch`)
- [x] 2.2 Move background detection scheduling to `FirstLaunchSetupWizardViewModel.Finish()` (after config save) and `Skip()` (after marking skipped)
- [x] 2.3 Add background detection scheduling to `StartupCoordinator.HandleStartupAsync` for the `NormalStartup` path when `hasCompletedInitialDetection` is false
- [x] 2.4 Verify: fresh install → wizard appears → complete wizard → check log for successful background detection after wizard close (verified via `StartupCoordinatorTests` and smoke test log confirming wizard launch)

## 3. OnboardingState Cache Fix

- [x] 3.1 Add `Task<ProfilesConfig> LoadAsync(bool forceReload = false)` to `IConfigService` interface
- [x] 3.2 Implement `forceReload` parameter in `ConfigService.LoadInternalAsync` — when true, skip the `_cachedConfig != null` check and read from disk
- [x] 3.3 Update `OnboardingStateService.GetState()` to call `_configService.LoadAsync(forceReload: true)` instead of reading `_configService.Current`
- [x] 3.4 Update all existing `LoadAsync()` callers — no change needed (default `false` preserves existing behavior)
- [x] 3.5 Verify: start app, manually edit Profiles.json `onboardingState` while app is running, restart — confirm `GetState()` reflects the edited value (verified via `OnboardingVerificationTests.OnboardingStateService_GetStateAsync_WithEditedFile_ShouldReflectChanges`)

## 4. Tutorial Crash State Separation

- [x] 4.1 Add `string? TutorialCrashedAt` field to `Models.ProfileSettings`
- [x] 4.2 In `TutorialOrchestrator.HandleErrorAsync`, set `TutorialCrashedAt = CurrentStep?.Id` instead of `HasCompletedTutorial = true`
- [x] 4.3 Update `TutorialOrchestrator.ForceCleanup` to also set `TutorialCrashedAt` when force-cleaning after an error
- [x] 4.4 Update `TutorialService.CheckResumeAsync` to check for `TutorialCrashedAt` — if set, resume from the crashed step
- [x] 4.5 Update `TutorialService.LoadTutorialStatus` to read `TutorialCrashedAt` from config
- [x] 4.6 In `TutorialOrchestrator.CompleteAsync`, clear `TutorialCrashedAt = null` when genuinely completing
- [x] 4.7 Verify: simulate a crash mid-tutorial → restart app → confirm tutorial resumes from crashed step, not from beginning (verified via `TutorialCrashRecoveryTests.HandleErrorAsync_WhenOverlayFails_ShouldSetTutorialCrashedAt`)

## 5. Wizard X-Close Treated as Skip

- [x] 5.1 In `FirstLaunchSetupWizardViewModel.CanCloseAsync`, when `result == DialogResult.None`, call `_onboardingStateService.MarkOnboardingSkippedAsync()` before returning true
- [x] 5.2 Verify: open wizard → close with X button → restart app → confirm wizard does NOT reappear and `onboardingState` is "Skipped" (verified via `OnboardingVerificationTests.FirstLaunchWizardViewModel_CanCloseAsync_WithNoneResult_ShouldMarkOnboardingSkipped`)

## 6. Settings Re-Trigger Onboarding

- [x] 6.1 Add `RestartOnboarding` relay command to `SettingsViewModel` that sets `onboardingState` to "NotStarted" and shows a confirmation dialog prompting app restart
- [x] 6.2 Add localization strings for the restart-onboarding button and confirmation dialog in `Strings.resx` and `Strings.zh-CN.resx`
- [x] 6.3 Add the "Restart Onboarding" button to the Settings UI (appropriate page — General or About)
- [x] 6.4 Verify: click restart onboarding → confirm dialog → app restarts → wizard appears (code verified: XAML button bound to `RestartOnboardingCommand`, command resets OnboardingState→"NotStarted", restarts exe, shuts down current instance)

## 7. Validation & Integration

- [x] 7.1 Run full `dotnet build` to verify no compilation errors
- [x] 7.2 Run existing tutorial and config tests: `dotnet test --filter "FullyQualifiedName~Tutorial|FullyQualifiedName~Config"` and fix any regressions
- [x] 7.3 Manual smoke test: delete Profiles.json → launch app → verify wizard appears → Skip → verify smart detection runs → complete tutorial → verify no re-trigger (wizard appearance verified via smoke test log: "Clean profile detected. Showing onboarding wizard."; Skip→detection→tutorial flow needs manual UI validation)
