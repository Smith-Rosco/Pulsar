## MODIFIED Requirements

### Requirement: Unsaved Changes Guard During Settings Navigation
The system SHALL protect unsaved settings edits when the user attempts to leave the current editing context, including shell-level page navigation and settings window close actions. The prompt-on-navigate behavior SHALL apply when navigating away from a permanent settings page. When navigating away from a transient settings page, the system SHALL NOT prompt; the transient page SHALL remain open with its navigation entry, the unsaved changes SHALL be retained, and the unsaved state SHALL still be surfaced on settings window close.

#### Scenario: User navigates away with unsaved changes
- **WHEN** the user attempts to switch to another settings page from a permanent page while the current editing context has unsaved changes
- **THEN** the system prompts the user to save, discard, or cancel before completing navigation

#### Scenario: User navigates away from a transient page with unsaved changes
- **WHEN** the user attempts to switch to another settings page from a transient page while unsaved changes exist
- **THEN** the system does not prompt and completes the navigation
- **THEN** the transient page's navigation entry remains in the sidebar and its unsaved changes are retained
- **THEN** the Save button unsaved indicator continues to reflect the unsaved state

#### Scenario: Last-opened page restoration does not bypass guard
- **WHEN** the settings shell restores the last-opened page on window open
- **THEN** restoration occurs only as initial shell state and does not bypass unsaved-change protection during later user-driven navigation

#### Scenario: Window close with a dirty transient page still prompts
- **WHEN** the user closes the settings window while a transient page is open and unsaved changes exist
- **THEN** the system prompts the user to save, discard, or cancel before closing
