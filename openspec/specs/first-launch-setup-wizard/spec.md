## ADDED Requirements

### Requirement: First launch offers a guided setup wizard
The system SHALL present a first-launch setup wizard for new users before they are expected to manually configure slots.

#### Scenario: New user sees setup wizard
- **WHEN** the application starts with no persisted onboarding completion state and no prior user setup
- **THEN** the system opens the first-launch setup wizard instead of assuming the user will configure Pulsar manually

#### Scenario: Existing user bypasses setup wizard
- **WHEN** the application starts for a user with existing onboarding completion or skip state
- **THEN** the system does not automatically show the first-launch setup wizard

### Requirement: Setup wizard generates a usable default configuration
The system SHALL generate a valid default slot configuration from the user's selected profile and chosen common applications.

#### Scenario: User selects a profile and apps
- **WHEN** the user completes the setup wizard with a supported usage profile and a set of common apps
- **THEN** the system writes a valid initial configuration that includes working default slots without requiring manual plugin configuration

#### Scenario: Generated configuration includes both mode entry points
- **WHEN** the system generates the default configuration for onboarding
- **THEN** the generated configuration includes at least one Switch Mode starter action and one Command Mode starter action or example path

### Requirement: Setup wizard choices remain editable after generation
The system SHALL generate standard editable slot configuration rather than locking users into a special onboarding-only configuration model.

#### Scenario: User edits generated slot after setup
- **WHEN** the user opens settings after the setup wizard has generated defaults
- **THEN** the generated slots appear in the normal settings authoring flow and can be modified using standard editing tools
