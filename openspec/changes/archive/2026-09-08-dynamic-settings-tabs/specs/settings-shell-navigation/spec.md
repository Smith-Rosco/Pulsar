## ADDED Requirements

### Requirement: Sidebar SHALL Reflect Transient Registration Changes Dynamically
The settings shell navigation layer SHALL add a navigation item to the sidebar when a transient page is registered (opened) and remove it when the transient page is recycled, without requiring a shell restart or full window rebuild. Transient navigation items SHALL be appended at the end of the transient page's semantic group.

#### Scenario: Opening a transient page adds a navigation item
- **WHEN** a transient page is opened
- **THEN** a navigation item for it appears in the sidebar at the end of its semantic group

#### Scenario: Recycling a transient page removes its navigation item
- **WHEN** a transient page is recycled
- **THEN** its navigation item is removed from the sidebar and the remaining items keep their order

### Requirement: Language-Driven Navigation Rebuild SHALL Preserve Open Transient Pages
The system SHALL rebuild sidebar navigation items on language change (existing behavior) while preserving the navigation entries and selection state of currently open transient pages.

#### Scenario: Language change keeps open transient entries
- **WHEN** the UI language changes while a transient page is open
- **THEN** the rebuilt sidebar still contains a localized navigation entry for the open transient page
- **THEN** the currently displayed page is unchanged

## MODIFIED Requirements

### Requirement: Centralized Settings Page Registration
The system SHALL define a centralized registration mechanism for settings pages so that page identifiers, page types, and navigation metadata are not duplicated across the shell. The registration mechanism SHALL distinguish permanent pages from transient pages and SHALL support runtime registration and removal of transient page registrations.

#### Scenario: A page is registered once
- **WHEN** a settings page is added to the application
- **THEN** it can be referenced by a single registration source for navigation and restoration purposes

#### Scenario: Invalid page selection is rejected safely
- **WHEN** the shell is asked to navigate to an unknown settings page identifier
- **THEN** the system rejects the request safely and preserves a valid current page

#### Scenario: A transient page is registered at runtime
- **WHEN** a transient page type is registered while the settings window is open
- **THEN** it becomes navigable by its identifier through the same centralized registration source as permanent pages

#### Scenario: A transient page registration is removed
- **WHEN** a transient page registration is removed after its page is recycled
- **THEN** the registration source no longer exposes it and navigating to its identifier is rejected safely
