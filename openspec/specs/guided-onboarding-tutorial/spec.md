## ADDED Requirements

### Requirement: First-run tutorial guides users to first successful actions
The system SHALL provide a guided onboarding tutorial that teaches the user how to trigger Pulsar, understand the difference between Switch Mode and Command Mode, and complete at least one successful action in each mode.

#### Scenario: User starts onboarding tutorial
- **WHEN** a new user finishes first-launch setup and chooses to continue onboarding
- **THEN** the system starts a guided tutorial flow that introduces the two Pulsar modes and advances through tracked steps

#### Scenario: User completes first successful switch action
- **WHEN** the tutorial reaches the switch exercise and the user successfully triggers the configured Switch Mode action
- **THEN** the tutorial records the milestone and advances to the next step

#### Scenario: User completes first successful command action
- **WHEN** the tutorial reaches the command exercise and the user successfully triggers the configured Command Mode action
- **THEN** the tutorial records the milestone and marks the tutorial flow complete

#### Scenario: User skips tutorial
- **WHEN** the user chooses to skip the tutorial from an onboarding tutorial step
- **THEN** the system persists the skipped state and does not automatically restart the tutorial on the next launch

### Requirement: Tutorial progress survives app restarts
The system SHALL persist onboarding tutorial state so that incomplete progress, completed milestones, and explicit skip decisions survive application restart.

#### Scenario: Incomplete tutorial resumes after restart
- **WHEN** the user exits the application after starting but not completing the tutorial
- **THEN** the system resumes from the saved tutorial state instead of restarting from the first step
