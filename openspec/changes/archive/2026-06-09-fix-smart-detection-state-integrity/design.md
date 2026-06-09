## Context

Smart detection was introduced to improve the first-launch fallback configuration by detecting installed applications and generating useful slots. The onboarding reliability work moved detection scheduling out of default config creation and into later lifecycle points, but the underlying detection implementation still assumes it can compare and replace an entire `Profiles.json` fallback snapshot.

That assumption is now unsafe. `Profiles.json` also stores onboarding state, tutorial progress, language, themes, input settings, plugin settings, and user-created profiles. Smart detection does not own those fields. A full generated config can reset `OnboardingState` to `NotStarted`, clear tutorial crash markers, drop user preferences, or abort because the persisted file differs from a freshly serialized fallback with a different timestamp.

Current risk shape:

```text
ConfigService.LoadAsync()
  -> CreateFallbackConfig()
  -> SaveAsync(fallback)

FirstLaunchSetupWizard.Skip()
  -> MarkOnboardingSkippedAsync()
  -> ScheduleSmartDetection()

ScheduleSmartDetection()
  -> CreateFallbackConfig()
  -> byte-for-byte compare persisted JSON
  -> CreateSmartConfig()
  -> SaveAsync(full generated config)
```

The fix should keep smart detection useful while narrowing its authority over persisted configuration.

## Goals / Non-Goals

**Goals:**

- Smart detection must preserve onboarding and tutorial state when it applies results.
- Smart detection must use semantic eligibility checks instead of byte-for-byte fallback JSON comparison.
- Smart detection must patch the latest persisted config rather than replacing it with a newly generated full config.
- Wizard skip and normal startup compensation must be able to complete initial detection when detection has not yet run.
- Wizard finish must not allow background detection to overwrite explicit user-selected onboarding slots unless an intentional policy allows it.
- Tests must cover the first-launch skip path, wizard finish path, stale cache path, and reset path.

**Non-Goals:**

- Redesigning the entire onboarding state machine.
- Changing the tutorial step content or overlay UX.
- Replacing `Profiles.json` persistence with a database or transaction log.
- Rewriting `BackgroundWorkScheduler`.
- Adding external dependencies.

## Decisions

### Decision 1: Replace byte-for-byte fallback comparison with semantic detection eligibility

Smart detection should decide whether to run by inspecting meaningful fields on the latest persisted config instead of comparing serialized JSON strings.

Eligibility should be based on:

- `HasCompletedInitialDetection == false`
- The current lifecycle path permits detection, such as wizard skip, reset fallback, or normal startup compensation
- The config is not in a state where detection would overwrite explicit wizard-generated user choices

Byte-for-byte comparison is rejected because fallback config includes volatile values such as `ConfigCreatedAt`, and because legitimate onboarding transitions change `OnboardingState` from `NotStarted` to `Skipped` or `SetupWizardComplete`.

### Decision 2: Apply detection as a narrow patch to the latest persisted config

Detection results should be applied by loading the latest config from disk, mutating only detection-owned fields, then saving that config.

Detection-owned fields are limited to:

- Automatically generated default app slots where policy allows replacing fallback/default slots
- `HasCompletedInitialDetection = true`

Detection must preserve:

- `OnboardingState`
- `HasCompletedTutorial`
- `LastTutorialStep`
- `TutorialCrashedAt`
- `ConfigCreatedAt`
- `Language`
- theme and input settings
- plugin configuration
- user-created profiles not owned by first-launch fallback generation

This rejects the current full replacement model where `CreateSmartConfig()` returns a full root config with default settings.

### Decision 3: Treat wizard finish and wizard skip differently

Wizard finish means the user explicitly selected a usage profile and apps. Smart detection must not overwrite those slots by default. The finish path may mark initial detection complete if the product decision is that wizard-generated selections are sufficient, or it may leave detection incomplete for a future non-destructive detection pass, but it must not schedule a destructive replacement pass.

Wizard skip means the user declined manual setup. In that case smart detection should run as the primary way to improve the fallback configuration, while preserving `OnboardingState = Skipped`.

Normal startup compensation should only schedule detection when `HasCompletedInitialDetection == false` and the current onboarding/tutorial state permits a non-destructive detection patch.

### Decision 4: Keep `HasCompletedInitialDetection` tied to automatic detection completion

`HasCompletedInitialDetection` should mean automatic app detection has completed or has been intentionally considered complete by an explicit policy. It should not accidentally mean “the app has some usable initial profile”.

If wizard finish marks this field true, the implementation should document that wizard-generated slots intentionally satisfy initial detection for that path. If not, wizard finish should leave it false until a non-destructive detection pass completes.

### Decision 5: Preserve existing public service shape where possible

Prefer internal helper methods on `ConfigService` over broad interface expansion. If a new public method is needed, keep it specific to detection scheduling or application, and update tests around `IConfigService` consumers.

## Risks / Trade-offs

- [Risk] Semantic eligibility may allow detection in a case where a user has already customized fallback slots -> Mitigation: only replace slots that match known fallback/default signatures or only fill empty/reserved slots unless the path explicitly permits replacement.
- [Risk] Preserving user config while patching slots is more complex than full replacement -> Mitigation: isolate patching logic and cover it with unit tests against representative config states.
- [Risk] Wizard finish semantics are ambiguous -> Mitigation: make the implementation choose one explicit policy and encode it in tests and comments.
- [Risk] Loading latest config before patching adds disk I/O -> Mitigation: smart detection is low-frequency first-launch/reset work, so correctness outweighs the small cost.
- [Risk] The existing in-progress onboarding reliability change may touch the same files -> Mitigation: apply this change after reconciling that change's final state, and avoid reverting unrelated onboarding fixes.

## Migration Plan

- Existing configs should continue loading without schema migration.
- Configs with `HasCompletedInitialDetection = false` should become eligible for non-destructive compensation on next startup if their lifecycle state permits it.
- Configs already marked as completed should not be forced through detection again.
- Rollback is safe at the data level because no new required fields are introduced.

## Open Questions

- Should wizard finish mark `HasCompletedInitialDetection = true` because the user selected apps, or should it leave the field false for a later non-destructive detection pass?
- Should smart detection replace known fallback slots or only fill empty/reserved slots after onboarding has been skipped?
