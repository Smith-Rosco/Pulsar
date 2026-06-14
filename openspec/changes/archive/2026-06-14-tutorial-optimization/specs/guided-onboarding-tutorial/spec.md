## ADDED Requirements

### Requirement: WaitForAction steps hide Next button until timeout
When a step type is `WaitForAction`, the Next/Continue button SHALL be hidden for the first 30 seconds to force the user to perform the intended action. After the 30-second hint timeout fires, the Next button SHALL reappear alongside the manual Continue button.

#### Scenario: Next button is hidden on WaitForAction step load
- **WHEN** a step with `type: "WaitForAction"` is displayed
- **THEN** the Next button SHALL have `Visibility = Collapsed`

#### Scenario: Next button appears after 30-second timeout
- **WHEN** the 30-second wait hint timeout fires on a WaitForAction step
- **THEN** the Next button SHALL become visible

#### Scenario: Next button appears when trigger fires early
- **WHEN** the completion trigger fires before the 30-second timeout
- **THEN** the tutorial SHALL auto-advance immediately (no need to show Next)

### Requirement: Tutorial demonstrates concrete app-switch-then-command workflow
The tutorial SHALL walk the user through a concrete scenario: switch to Notepad via Switch Mode, then run a command via Command Mode that inserts text into Notepad.

#### Scenario: Welcome step explains the workflow
- **WHEN** the tutorial starts
- **THEN** the welcome copy SHALL name Notepad as the target app and explain the two-step workflow

#### Scenario: Switch-to-Notepad step
- **WHEN** the user reaches the Switch Mode step
- **THEN** the instruction SHALL say "Press Ctrl+Q and click the Notepad icon to switch to it"
- **AND** the step SHALL auto-advance when Pulsar reports a successful Switch action

#### Scenario: Insert-text command step
- **WHEN** the user reaches the Command Mode step after successfully switching to Notepad
- **THEN** the instruction SHALL say "Press Ctrl+Shift+Q and click 'Insert Sample Text' to type into Notepad"
- **AND** the step SHALL auto-advance when Pulsar reports a successful Command action

#### Scenario: Notepad not available fallback
- **WHEN** the system does not detect Notepad on the current machine
- **THEN** the tutorial SHALL fall back to the first generated app slot and use that app for the demonstration
- **AND** the step copy SHALL reference that app by name instead of Notepad

### Requirement: Step auto-advance shows visual success feedback
When a trigger fires and the tutorial auto-advances to the next step, the card SHALL briefly flash green border to signal that the action was successful and the step transition is happening.

#### Scenario: Green border flash on auto-advance
- **WHEN** a WaitForAction step completion trigger fires
- **THEN** the card border SHALL animate: color changes to green (`#27AE60`) immediately, holds for 150ms, then fades to transparent over 300ms

#### Scenario: No flash on manual Next click
- **WHEN** the user clicks Next/Continue manually
- **THEN** no green border flash SHALL play

### Requirement: Step transition uses smooth crossfade
When transitioning between tutorial steps, the overlay window SHALL remain open and the card content SHALL crossfade (old content fades out while new content fades in) to eliminate visual flicker or blank-gap flash.

#### Scenario: Crossfade executes on next step
- **WHEN** `NextStepAsync()` is called
- **THEN** the current card SHALL fade from opacity 1 to 0 over 200ms
- **AND** the new card SHALL fade from opacity 0 to 1 over 200ms simultaneously
- **AND** the overlay window SHALL NOT close or re-show during this process

### Requirement: Tutorial can be restarted from Settings
The system SHALL provide a "Reset Tutorial" button accessible from within the application after the tutorial has been completed or skipped.

#### Scenario: Reset tutorial from Settings
- **WHEN** the user clicks "Reset Tutorial" in Settings
- **THEN** a confirmation dialog SHALL appear: "This will restart the tutorial. Continue?"
- **AND** on confirm, `OnboardingState` SHALL be set to `"SetupWizardComplete"`, `HasCompletedTutorial` SHALL be cleared
- **AND** the tutorial SHALL start from step 1 on the next trigger of the tutorial start sequence

### Requirement: Tutorial copy uses warm, human tone
All user-facing tutorial text SHALL be rewritten to use warm, encouraging language that speaks directly to the user (you/your), describes what the user will experience, and avoids corporate jargon ("minimum onboarding path", "accelerate workflow", "useful wins").

#### Scenario: English copy is rewritten
- **WHEN** a user views the tutorial in English
- **THEN** all `Tutorial.*` string resources SHALL use conversational tone, first-name basis ("you"), and concrete action descriptions

#### Scenario: Chinese copy is rewritten
- **WHEN** a user views the tutorial in Chinese
- **THEN** all translated strings SHALL be reviewed for machine-translation artifacts and rewritten as natural Chinese
