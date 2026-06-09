## ADDED Requirements

### Requirement: Smart detection uses semantic eligibility checks
The system SHALL determine whether smart detection may run by inspecting the latest persisted configuration's `HasCompletedInitialDetection` field and the current lifecycle path, rather than comparing serialized fallback JSON strings.

#### Scenario: Detection runs when not yet completed and lifecycle permits
- **WHEN** `HasCompletedInitialDetection` is `false` AND the lifecycle path is wizard skip, reset fallback, or normal startup compensation
- **THEN** the system SHALL schedule and execute smart detection

#### Scenario: Detection does not run when already completed
- **WHEN** `HasCompletedInitialDetection` is `true`
- **THEN** the system SHALL NOT schedule smart detection regardless of lifecycle path

#### Scenario: Wizard finish does not trigger destructive detection replacement
- **WHEN** the user completes the wizard by selecting a usage profile and apps
- **THEN** the system SHALL NOT schedule a background detection pass that would overwrite user-selected wizard slots

### Requirement: Smart detection patches the latest persisted configuration
The system SHALL apply detection results by loading the latest persisted `ProfilesConfig` from disk, mutating only detection-owned fields, and saving the result, rather than creating a full replacement config.

#### Scenario: Detection preserves onboarding state
- **WHEN** smart detection applies results after a wizard skip
- **THEN** the persisted `OnboardingState` field SHALL remain `Skipped`

#### Scenario: Detection preserves tutorial state
- **WHEN** smart detection applies results while tutorial is in progress or crashed
- **THEN** the persisted `HasCompletedTutorial`, `LastTutorialStep`, and `TutorialCrashedAt` fields SHALL remain unchanged

#### Scenario: Detection preserves user settings
- **WHEN** smart detection applies results
- **THEN** the persisted `Language`, theme, input, and plugin configuration fields SHALL remain unchanged

#### Scenario: Detection preserves user-created profiles
- **WHEN** smart detection applies results
- **THEN** user-created process profiles not owned by first-launch fallback generation SHALL remain in the persisted configuration

### Requirement: Detection updates only detection-owned fields
The system SHALL restrict smart detection writes to automatically generated default app slots where policy allows replacement and the `HasCompletedInitialDetection` field set to `true`.

#### Scenario: Detection marks completion
- **WHEN** smart detection successfully applies its results
- **THEN** the persisted `HasCompletedInitialDetection` field SHALL be set to `true`

#### Scenario: Detection only fills reserved or empty slots
- **WHEN** smart detection generates app slots
- **THEN** it SHALL only fill slots that match known fallback/default signatures or empty/reserved slots, unless the lifecycle path explicitly permits full replacement
