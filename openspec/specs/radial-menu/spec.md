## MODIFIED Requirements

### Requirement: Mouse Interaction Model
The radial menu SHALL handle mouse clicks globally when visible, regardless of whether the cursor is within the physical bounds of the `RadialMenuWindow`. It SHALL NOT rely on WPF `CaptureMouse`.

#### Scenario: Left clicking outside window bounds
- **WHEN** the radial menu is visible
- **AND** the user left-clicks outside the 500x500 window bounds
- **THEN** the global hook SHALL swallow the click
- **AND** the menu SHALL trigger the action associated with the currently highlighted slot (if any)
- **AND** the background application SHALL NOT receive the click

#### Scenario: Dragging to invoke slot
- **WHEN** the radial menu is invoked via hotkey
- **AND** the user drags the mouse to highlight a slot (even if the cursor goes outside the window)
- **AND** the user left-clicks
- **THEN** the slot action SHALL be executed
- **AND** the menu SHALL dismiss

## ADDED Requirements

### Requirement: Right-Click Navigation
The radial menu SHALL use right-clicks as a navigation mechanism.

#### Scenario: Navigating back from a sub-menu
- **WHEN** the user is viewing a sub-menu (e.g., Process Group)
- **AND** the user right-clicks anywhere on the screen
- **THEN** the global hook SHALL swallow the right-click
- **AND** the menu SHALL navigate back to the root menu

#### Scenario: Right-clicking at the root menu
- **WHEN** the user is viewing the root menu
- **AND** the user right-clicks anywhere on the screen
- **THEN** the global hook SHALL swallow the right-click
- **AND** the menu SHALL display a visual indication (e.g., a "bounce" or "shake" animation on the center slot) that they cannot go back further
- **AND** the menu SHALL NOT close

### Requirement: Non-Interference with Window Focus
The radial menu SHALL maintain its interaction state without strictly requiring WPF Window Focus for mouse clicks, as the global hook bypasses standard hit testing.

#### Scenario: Menu remains active during global clicks
- **WHEN** the radial menu is visible
- **AND** the user clicks outside the window (handled by the hook)
- **THEN** the menu SHALL remain visible and interactive until explicitly dismissed (via hotkey release or a successful slot action).
