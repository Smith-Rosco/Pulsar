## Why

The onboarding reliability work exposed a deeper configuration integrity risk: smart detection currently verifies and writes whole `Profiles.json` snapshots even though it only owns detected app slot suggestions. This can silently abort first-launch detection, or worse, overwrite onboarding/tutorial state such as `OnboardingState`, `LastTutorialStep`, and `TutorialCrashedAt`.

## What Changes

- Replace byte-for-byte fallback JSON comparison with semantic eligibility checks for smart detection.
- Change smart detection from full-config replacement to narrow patching of the latest persisted configuration.
- Preserve onboarding, tutorial, language, theme, input, plugin, and user-created profile state when applying detection results.
- Clarify when smart detection should run after first launch, wizard skip, setup completion, reset, and normal startup compensation.
- Ensure `HasCompletedInitialDetection` only represents completion of automatic application detection, not merely the presence of a usable initial profile.
- Add regression coverage for skip, finish, reset, and stale-cache scenarios.

## Capabilities

### New Capabilities

- `smart-detection-state-integrity`: Defines smart detection eligibility, narrow configuration patching, and preservation requirements for onboarding/tutorial state.
- `onboarding-state-integrity`: Defines valid onboarding/tutorial state preservation rules when startup, reset, wizard, tutorial, and background detection flows interact.

### Modified Capabilities

- None.

## Impact

- Affected code: `ConfigService`, `IConfigService`, `StartupCoordinator`, `FirstLaunchSetupWizardViewModel`, `OnboardingTemplateService`, onboarding state tests, and config service tests.
- Affected data: `Profiles.json` fields including `OnboardingState`, `HasCompletedInitialDetection`, `HasCompletedTutorial`, `LastTutorialStep`, `TutorialCrashedAt`, `Language`, profile dictionaries, and plugin config dictionaries.
- No new runtime dependencies are expected.
- No breaking external API changes are expected; any interface changes should remain internal to the application service layer.
