## Purpose
Define the settings shell navigation contract so page registration, selection, and restoration are owned by a dedicated shell layer instead of being coupled to configuration editing state.

## Requirements

### Requirement: Dedicated Settings Shell Navigation
The system SHALL provide a dedicated settings shell navigation layer that manages page registration, current page selection, and shell-level navigation state independently from configuration editing logic.

#### Scenario: Settings shell selects an initial page
- **WHEN** the settings window is opened
- **THEN** the shell selects a valid initial page without requiring configuration editing logic to determine navigation state

#### Scenario: Settings shell changes pages
- **WHEN** the user selects a different page in the settings navigation
- **THEN** the shell updates the current page and loads the corresponding page content through a centralized page mapping

#### Scenario: Shell state is separate from edit state
- **WHEN** configuration editing state changes within a page
- **THEN** shell navigation state remains owned by the settings shell layer rather than by the configuration editor ViewModel

### Requirement: Centralized Settings Page Registration
The system SHALL define a centralized registration mechanism for settings pages so that page identifiers, page types, and navigation metadata are not duplicated across the shell.

#### Scenario: A page is registered once
- **WHEN** a settings page is added to the application
- **THEN** it can be referenced by a single registration source for navigation and restoration purposes

#### Scenario: Invalid page selection is rejected safely
- **WHEN** the shell is asked to navigate to an unknown settings page identifier
- **THEN** the system rejects the request safely and preserves a valid current page
