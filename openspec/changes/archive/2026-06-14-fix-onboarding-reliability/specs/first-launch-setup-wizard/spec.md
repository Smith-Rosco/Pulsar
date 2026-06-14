## MODIFIED Requirements

### Requirement: First launch offers a guided setup wizard
The system SHALL present a first-launch setup wizard for new users before they are expected to manually configure slots.

#### Scenario: New user sees setup wizard
- **WHEN** the application starts with no persisted onboarding completion state and no prior user setup
- **THEN** the system opens the first-launch setup wizard instead of assuming the user will configure Pulsar manually

#### Scenario: Existing user bypasses setup wizard
- **WHEN** the application starts for a user with existing onboarding completion or skip state
- **THEN** the system does not automatically show the first-launch setup wizard

#### Scenario: User closes wizard window
- **WHEN** the user closes the setup wizard via window chrome (X button or Alt+F4) without clicking Skip or Finish
- **THEN** the system persists onboarding state as skipped and does not re-show the wizard on next launch

## ADDED Requirements

### Requirement: Wizard display failure shows tray notification
The system SHALL notify the user via tray notification when the first-launch setup wizard cannot be displayed due to an error, and SHALL continue normal startup.

#### Scenario: Wizard fails to display
- **WHEN** the deferred startup attempts to show the setup wizard and the dialog operations fail
- **THEN** the system shows a tray notification to the user and proceeds with normal startup
