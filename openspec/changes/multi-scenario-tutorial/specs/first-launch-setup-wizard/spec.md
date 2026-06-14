## MODIFIED Requirements

### Requirement: First launch offers a guided setup wizard
The system SHALL present a first-launch setup wizard for new users that includes scenario selection with prerequisite validation.

#### Scenario: New user sees setup wizard with scenario options
- **WHEN** the application starts with no persisted onboarding completion state
- **THEN** the system opens the first-launch setup wizard
- **AND** the wizard SHALL display available `TutorialScenario` instances as selectable cards
- **AND** each card SHALL show the scenario title, description, and prerequisite status

#### Scenario: Existing user bypasses setup wizard
- **WHEN** the application starts for a user with existing onboarding completion or skip state
- **THEN** the system does not automatically show the first-launch setup wizard

#### Scenario: User closes wizard window
- **WHEN** the user closes the setup wizard via window chrome without clicking Skip or Finish
- **THEN** the system persists onboarding state as skipped and does not re-show the wizard on next launch

#### Scenario: Scenario card shows prerequisite status
- **WHEN** a scenario card is displayed
- **THEN** each prerequisite SHALL show its status indicator (✅/⚠️/🛑/⏳)
- **AND** the status SHALL update in real-time as prerequisite checks complete

#### Scenario: Required prerequisite failure blocks Finish
- **WHEN** a scenario is selected and at least one Required prerequisite has `Status = NotMet`
- **THEN** the Finish button SHALL be disabled
- **AND** a tooltip SHALL indicate: "Prerequisite not met: [software name]"

#### Scenario: Finish creates scenario-appropriate slots
- **WHEN** the user clicks Finish with a specific scenario selected
- **THEN** `BuildInitialConfig()` SHALL use the selected `TutorialScenario`'s `CommandSlotTemplates`
- **AND** SHALL generate the correct plugin slots for that scenario
- **AND** the tutorial SHALL use the scenario's steps JSON when it starts

#### Scenario: Scenario selection resets on language change
- **WHEN** the user changes the selected language
- **THEN** the scenario cards SHALL update their localized text
- **AND** prerequisite checks SHALL re-run (results are language-independent)
