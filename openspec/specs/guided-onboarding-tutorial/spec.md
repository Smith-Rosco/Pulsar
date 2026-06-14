## MODIFIED Requirements

### Requirement: Tutorial demonstrates concrete app-switch-then-command workflow
The tutorial SHALL walk the user through a concrete scenario: switch to the target app via Switch Mode, then run a command via Command Mode that demonstrates the chosen plugin.

#### Scenario: Welcome step explains the scenario-specific workflow
- **WHEN** the tutorial starts
- **THEN** the welcome copy SHALL name the target app (Notepad, Excel, or browser) and explain the two-step workflow
- **AND** the target app name SHALL be determined by the active `TutorialScenario`

#### Scenario: Switch-to-app step
- **WHEN** the user reaches the Switch Mode step
- **THEN** the instruction SHALL reference the scenario's target app (e.g., "Press Ctrl+Q and click the Notepad icon")
- **AND** the step SHALL auto-advance when Pulsar reports a successful Switch action

#### Scenario: Command step references scenario-specific slot
- **WHEN** the user reaches the Command Mode step
- **THEN** the instruction SHALL reference the scenario's primary command slot label (e.g., "'Insert Sample Text' or 'Run VBA Demo'")
- **AND** the step SHALL auto-advance when Pulsar reports a successful Command action

#### Scenario: App not available fallback
- **WHEN** the scenario's target app is not detected on the current machine
- **THEN** the tutorial SHALL fall back to the first generated app slot for that scenario
- **AND** the step copy SHALL reference that app by name instead of the original target

## ADDED Requirements

### Requirement: Step 2→3 transition is optimized to reduce context switching
When a WaitForAction step completes via ActionExecuted trigger, the system SHALL skip the subsequent Instruction-only confirmation step and show a brief toast notification instead.

#### Scenario: Switch mode success skips confirmation step
- **WHEN** step 2 (ActionExecuted/Switch) trigger fires
- **THEN** the system SHALL skip step 3 (the Instruction-only success confirmation)
- **AND** SHALL show a toast notification: "Switched to [app name]!" with a green checkmark
- **AND** SHALL advance directly to step 4 (Command Mode)

#### Scenario: Manual Next click does not trigger skip
- **WHEN** the user manually clicks "Next" on a WaitForAction step
- **THEN** the system SHALL NOT skip the subsequent step
- **AND** SHALL advance to the next step normally

#### Scenario: Step 3 is reached when manually navigated
- **WHEN** the user navigates to step 3 via tutorial restart or step selector
- **THEN** step 3 SHALL display normally as an Instruction step

### Requirement: Tutorial shows slot-missing fallback guidance
When entering a WaitForAction step that requires a specific plugin slot, the system SHALL check whether the required slot exists in the current config. If missing, it SHALL display inline guidance instead of hanging indefinitely.

#### Scenario: Required slot missing shows guidance card
- **WHEN** `TutorialOrchestrator` enters a step with `ActionExecuted` trigger
- **AND** the target slot (matching `IsTutorialPrimary` criteria) is not found in the current config
- **THEN** the tutorial SHALL display an inline guidance message: "Your Pulsar slots are not configured yet. Go to Settings → Slots to add one, or restore default slots."
- **AND** the "Next" button SHALL remain visible to let the user advance past the step

#### Scenario: Slot exists proceeds normally
- **WHEN** the target slot is found in the current config
- **THEN** the tutorial SHALL proceed with normal WaitForAction behavior (hidden Next button, wait for trigger)

#### Scenario: Slot appears after guidance is shown
- **WHEN** the guidance card is displayed and the user subsequently adds the required slot
- **THEN** the SlotAdded trigger SHALL fire and the tutorial SHALL auto-advance
