## 1. Unify reset entrypoint

- [x] 1.1 Add a `ConfigService`-owned reset path that clears in-memory configuration state and re-enters the same file-missing first-launch flow used by `LoadAsync()`
- [x] 1.2 Keep the existing backup behavior before reset replaces active configuration state
- [x] 1.3 Update `SettingsViewModel.ResetConfig()` to call the unified config reset path instead of saving `new ProfilesConfig()` directly

## 2. Restore fresh-start state semantics

- [x] 2.1 Ensure reset clears tutorial progress state so `HasCompletedTutorial` becomes false and `LastTutorialStep` becomes null
- [x] 2.2 Ensure reset returns initial-detection metadata to a fresh state so first-launch detection can run again
- [x] 2.3 Verify the reloaded post-reset configuration matches first-launch baseline behavior, including default profiles and fallback slots

## 3. Align runtime flow after reset

- [x] 3.1 Define and implement the post-reset reload sequence so the current session sees the regenerated fallback configuration immediately
- [x] 3.2 Ensure the existing background application detection flow can run again after reset and evolve fallback config into smart config
- [x] 3.3 Review tutorial startup behavior after reset and align it with the chosen fresh-install semantics without conflicting with the settings window lifecycle

## 4. Update UX and guardrails

- [x] 4.1 Update reset confirmation/success messaging to describe that reset restores the first-launch experience and recreates default profiles
- [x] 4.2 Ensure reset no longer leaves the user in a bare clean-slate configuration unless first launch itself would do so
- [x] 4.3 Add logging around unified reset/first-launch transitions so reset-driven regeneration can be diagnosed separately from ordinary startup

## 5. Validate with tests

- [x] 5.1 Add or extend `ConfigService` tests to verify reset re-enters the first-launch default path instead of persisting `new ProfilesConfig()`
- [x] 5.2 Add tests covering tutorial-state clearing and initial-detection metadata reset behavior
- [x] 5.3 Add a view-model or workflow test confirming `ResetConfig()` reloads regenerated defaults rather than an empty configuration
- [ ] 5.4 Run targeted automated tests for config lifecycle behavior and manual verification for reset -> fallback -> smart-config progression
