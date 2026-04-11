## 1. Baseline: Onboarding State And Startup Coordination

This baseline is already complete and should remain the foundation for the follow-up slices below.

- [x] 1.1 Create an onboarding state model and persistence service for first-run, skipped, partial, and completed states
- [x] 1.2 Add startup coordination logic that decides whether to show onboarding for a clean user profile
- [x] 1.3 Ensure existing configured users bypass automatic onboarding
- [x] 1.4 Add tests or verification coverage for first-run, skipped, resumed, and existing-user startup paths

## 2. Slice A: First-Launch Setup Wizard And Deterministic Defaults

Goal: get a clean-profile user through a short setup flow that produces editable starter configuration using existing profile and slot models.

- [x] 2.1 Define the first-launch setup wizard view model, dialog content, and supported usage profiles
- [x] 2.2 Implement selectable common-app inputs and validation for the supported onboarding templates
- [x] 2.3 Implement deterministic template generation for default Switch Mode starter slots
- [x] 2.4 Implement deterministic template generation for at least one Command Mode starter example
- [x] 2.5 Persist generated configuration using existing slot and profile models without introducing a separate config format
- [x] 2.6 Register the wizard in the dialog system and verify theme/style integration follows existing WPF rules
- [x] 2.7 Verify a clean profile reaches a usable generated default configuration after setup completion

## 3. Slice B: Guided Onboarding Tutorial Flow

Depends on Slice A.

Goal: guide the user from setup completion to the first successful Switch Mode action and first successful Command Mode action, with restart-safe progress.

- [x] 3.1 Complete the tutorial orchestrator flow for the minimum onboarding path from setup completion to first successful actions
- [x] 3.2 Implement or finish the tutorial steps needed to teach hotkeys, Switch Mode, and Command Mode
- [x] 3.3 Wire tutorial milestone tracking to successful switch and command execution events
- [x] 3.4 Persist tutorial progress so partial completion resumes after restart
- [x] 3.5 Implement skip behavior so skipped tutorials do not auto-restart on next launch
- [x] 3.6 Verify the tutorial flow works against the generated onboarding defaults on a clean profile

## 4. Slice C: Scenario-Based Slot Authoring

Can proceed independently of Slice B after Slice A is stable.

Goal: make the default add-slot path intent-first for common cases while preserving advanced plugin-first editing.

- [x] 4.1 Add a scenario-based add-slot entry flow as the default settings entry point for common slot types
- [x] 4.2 Implement intent mapping for switch app to canonical WinSwitcher slot configuration
- [x] 4.3 Implement intent mapping for open program, file, folder, or URL to canonical Command Runner `run` configuration
- [x] 4.4 Implement intent mapping for send keys or insert text to canonical Command Runner `sendkeys` configuration
- [x] 4.5 Implement intent mapping for fill credential to canonical PKI `fill` configuration
- [x] 4.6 Preserve an advanced plugin-first editing path for unsupported or power-user scenarios
- [x] 4.7 Add save-time validation and error states for the supported scenario-based flows
- [x] 4.8 Verify generated onboarding slots remain editable in normal settings flows
- [x] 4.9 Verify advanced slot editing remains accessible after scenario-based authoring is introduced

## 5. Slice D: User-Facing Action Feedback Normalization

Can proceed independently, but should be complete before final onboarding polish.

Goal: normalize common plugin execution outcomes into consistent user-facing feedback without exposing raw exception details or secrets.

- [x] 5.1 Define a normalized user-facing feedback model for success, recoverable failure, configuration error, and temporary unavailability
- [x] 5.2 Implement feedback mapping for common WinSwitcher outcomes
- [x] 5.3 Implement feedback mapping for common Command Runner outcomes
- [x] 5.4 Implement feedback mapping for PKI outcomes without exposing sensitive values
- [x] 5.5 Wire normalized feedback into the primary execution surfaces used during onboarding and normal action execution
- [x] 5.6 Verify identical outcome types present consistent severity and messaging across supported UI surfaces
- [x] 5.7 Verify action feedback does not expose secrets or raw exception details

## 6. Slice E: End-To-End Verification And Release Readiness

Depends on Slices A-D.

Goal: validate the integrated onboarding path and ensure the build is stable before considering this change complete.

- [x] 6.1 Run a clean-profile onboarding pass and verify the user can reach one successful Switch Mode action and one successful Command Mode action
- [x] 6.2 Verify existing configured users still bypass automatic onboarding
- [x] 6.3 Verify reset-to-first-launch returns the app to onboarding-eligible state with recoverable backup behavior intact
- [x] 6.4 Run `dotnet build Pulsar/Pulsar/Pulsar.csproj` and resolve build issues
