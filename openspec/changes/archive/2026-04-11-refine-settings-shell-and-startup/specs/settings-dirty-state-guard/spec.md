## MODIFIED Requirements

### Requirement: Unsaved Changes Guard During Settings Navigation
The system SHALL protect unsaved settings edits when the user attempts to leave the current editing context, including shell-level page navigation and settings window close actions.

#### Scenario: User navigates away with unsaved changes
- **WHEN** the user attempts to switch to another settings page while the current editing context has unsaved changes
- **THEN** the system prompts the user to save, discard, or cancel before completing navigation

#### Scenario: Last-opened page restoration does not bypass guard
- **WHEN** the settings shell restores the last-opened page on window open
- **THEN** restoration occurs only as initial shell state and does not bypass unsaved-change protection during later user-driven navigation
