## ADDED Requirements

### Requirement: Onboarding state reads from persisted configuration
The system SHALL read onboarding state from the persisted configuration file rather than relying solely on an in-memory cache that may never refresh after initial load.

#### Scenario: Onboarding state reflects latest persisted state
- **WHEN** the onboarding state service queries the current onboarding state
- **THEN** it SHALL bypass the in-memory configuration cache and read directly from the persisted configuration file

#### Scenario: In-memory cache is used for non-onboarding reads
- **WHEN** other components read configuration not related to onboarding state
- **THEN** the system MAY use the in-memory cache as before without forced reload

### Requirement: Background smart detection does not conflict with wizard
The system SHALL schedule background application detection to run after the first-launch setup wizard completes or is skipped, rather than during wizard display.

#### Scenario: User completes wizard then smart detection runs
- **WHEN** the user finishes the first-launch setup wizard
- **THEN** the system schedules background application detection to discover installed applications

#### Scenario: User skips wizard then smart detection runs
- **WHEN** the user skips the first-launch setup wizard
- **THEN** the system schedules background application detection to discover installed applications

#### Scenario: Smart detection does not run during wizard display
- **WHEN** the first-launch setup wizard is displayed and awaiting user input
- **THEN** background application detection SHALL NOT modify the persisted configuration

### Requirement: Closing the wizard window is treated as skip
The system SHALL treat closing the wizard window via window chrome (X button, Alt+F4) as an explicit skip of the onboarding process.

#### Scenario: User closes wizard via X button
- **WHEN** the user closes the first-launch setup wizard dialog using the window close button or Alt+F4
- **THEN** the system persists `OnboardingState = "Skipped"` and does not re-show the wizard on next launch

#### Scenario: User closes wizard and restarts
- **WHEN** the user closes the wizard via window chrome and restarts the application
- **THEN** the system recognizes the skipped state and proceeds with normal startup
