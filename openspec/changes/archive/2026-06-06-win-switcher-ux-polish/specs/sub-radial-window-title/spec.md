## ADDED Requirements

### Requirement: Sub-menu window slots SHALL display the window title as the slot label
When the radial menu presents a sub-menu of individual windows for a grouped process, each slot SHALL display the actual window title as its primary label instead of a numbered suffix derived from the process name.

#### Scenario: Process has multiple windows and sub-menu is opened
- **WHEN** a grouped root slot is opened to its sub-menu
- **AND** the target process has multiple eligible windows
- **THEN** each sub-menu slot SHALL display the corresponding window's title text as the slot label

#### Scenario: Window title exceeds display length
- **WHEN** a window title is longer than 40 characters
- **THEN** the slot label SHALL be truncated to 40 characters with an ellipsis suffix

#### Scenario: Window title is empty or whitespace
- **WHEN** a window has no title or a whitespace-only title
- **THEN** the slot SHALL display the process name as the fallback label

### Requirement: Window title label SHALL coexist with existing icon and badge
The window title label SHALL NOT interfere with the slot's icon, type badge, or activation count badge.

#### Scenario: Sub-menu slot is rendered
- **WHEN** a sub-menu window slot is displayed
- **THEN** the slot SHALL show the window title label alongside the app icon and type badge
- **AND** the label SHALL NOT overlap with or obscure the icon or badge elements
