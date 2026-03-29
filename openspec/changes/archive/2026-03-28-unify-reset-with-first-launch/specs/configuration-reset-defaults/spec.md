## ADDED Requirements

### Requirement: Reset configuration SHALL restore first-launch default behavior
The system SHALL treat `Reset Configuration` as a request to return the application to the same configuration lifecycle state as a fresh installation. After reset, default configuration generation SHALL follow the same entry point and behavior used when `Profiles.json` does not exist at first launch.

#### Scenario: Reset enters first-launch configuration path
- **WHEN** the user confirms `Reset Configuration`
- **THEN** the system SHALL stop using any reset-specific clean-slate configuration constructor
- **THEN** the system SHALL re-enter the same default configuration path used when no configuration file exists

#### Scenario: Reset produces first-launch baseline content
- **WHEN** reset completes and the configuration is reloaded
- **THEN** the active configuration SHALL contain the same baseline profiles, settings, and default slots that first launch would generate before any user customization

### Requirement: Reset SHALL clear tutorial progress and resume fresh-start onboarding semantics
Reset SHALL discard prior tutorial progress so the application returns to the same onboarding state as a new installation.

#### Scenario: Completed tutorial is cleared by reset
- **WHEN** the user resets configuration after previously completing the tutorial
- **THEN** `HasCompletedTutorial` SHALL be `false`
- **THEN** the application SHALL treat the user as eligible for first-launch onboarding again

#### Scenario: In-progress tutorial resume state is cleared by reset
- **WHEN** the user resets configuration while `LastTutorialStep` contains a saved step
- **THEN** `LastTutorialStep` SHALL be cleared
- **THEN** the system SHALL NOT resume the old tutorial position after reset

### Requirement: Reset SHALL re-enable first-launch detection and smart-default evolution
If first launch would schedule fallback configuration followed by background application detection and smart configuration generation, reset SHALL restore the same behavior.

#### Scenario: Reset restores fallback-then-smart flow
- **WHEN** reset is triggered on a machine where first launch would schedule background application detection
- **THEN** the system SHALL first expose a usable fallback configuration
- **THEN** the system SHALL allow the same background detection flow to run again

#### Scenario: Reset returns detection metadata to fresh state
- **WHEN** reset completes
- **THEN** configuration metadata SHALL indicate that initial detection is no longer considered completed until the new first-launch detection flow finishes

### Requirement: Reset SHALL preserve recoverability while replacing active configuration state
Before replacing the active configuration state, reset SHALL preserve a backup of the prior persisted configuration so the user's previous setup can be recovered manually if needed.

#### Scenario: Reset creates backup before replacing configuration
- **WHEN** a persisted `Profiles.json` exists and the user confirms reset
- **THEN** the system SHALL create or overwrite a backup copy before replacing the active configuration state

#### Scenario: Reset no longer leaves an empty clean-slate config as the final result
- **WHEN** reset completes successfully
- **THEN** the final active configuration SHALL NOT be a bare `new ProfilesConfig()` state unless first launch itself would produce that same result
