## ADDED Requirements

### Requirement: Onboarding state is preserved across startup lifecycle transitions
The system SHALL preserve onboarding-related fields (`OnboardingState`, `HasCompletedTutorial`, `LastTutorialStep`, `TutorialCrashedAt`) across startup, wizard, tutorial, reset, and background detection flows unless the specific flow holds clear authority to change them.

#### Scenario: Smart detection does not reset onboarding state
- **WHEN** smart detection completes and saves configuration
- **THEN** the value of `OnboardingState` SHALL match the value read from disk before detection started

#### Scenario: Wizard skip persists as skipped after all startup flows
- **WHEN** the user skips the wizard AND other startup flows later execute
- **THEN** `OnboardingState` SHALL remain `Skipped`

#### Scenario: Wizard finish persists as complete after all startup flows
- **WHEN** the user completes the wizard AND other startup flows later execute
- **THEN** `OnboardingState` SHALL remain `SetupWizardComplete`

#### Scenario: Config reset re-enters first-launch path
- **WHEN** configuration reset is invoked
- **THEN** `OnboardingState` SHALL be set to `NotStarted` and all other onboarding fields SHALL be reset to first-run defaults

### Requirement: HasCompletedInitialDetection has a single unambiguous meaning
The system SHALL treat `HasCompletedInitialDetection` as a signal that automatic application detection has completed or has been intentionally considered complete by an explicit policy.

#### Scenario: Wizard finish may lock detection as complete
- **WHEN** implementation policy defines that wizard-generated slots satisfy initial detection
- **THEN** the system SHALL set `HasCompletedInitialDetection` to `true` during the wizard finish save and SHALL NOT schedule further background detection for that lifecycle

#### Scenario: Wizard skip does not lock detection as complete
- **WHEN** the user skips the wizard
- **THEN** `HasCompletedInitialDetection` SHALL remain `false` until smart detection completes

#### Scenario: Normal startup compensates incomplete detection
- **WHEN** the app starts normally AND `HasCompletedInitialDetection` is `false` AND the onboarding lifecycle permits
- **THEN** the system SHALL schedule non-destructive smart detection
