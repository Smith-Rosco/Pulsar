## ADDED Requirements

### Requirement: Global Mouse Click Interception
The system SHALL be capable of intercepting `WM_LBUTTONDOWN`, `WM_LBUTTONUP`, `WM_RBUTTONDOWN`, and `WM_RBUTTONUP` events system-wide.

#### Scenario: Intercepting events when enabled
- **WHEN** the global mouse hook is installed and active
- **AND** the system detects a mouse click event
- **THEN** the hook callback SHALL receive the event details before they are dispatched to the target application

### Requirement: Conditional Event Swallowing
The system SHALL provide a mechanism to selectively swallow intercepted mouse events based on application state, preventing them from reaching other applications.

#### Scenario: Swallowing a left click
- **WHEN** a `WM_LBUTTONDOWN` or `WM_LBUTTONUP` event is intercepted
- **AND** the application logic determines the event should be handled (e.g., menu is visible)
- **THEN** the hook SHALL return `1` to `CallNextHookEx` to swallow the event
- **AND** the event SHALL NOT be received by the background application

#### Scenario: Swallowing a right click
- **WHEN** a `WM_RBUTTONDOWN` or `WM_RBUTTONUP` event is intercepted
- **AND** the application logic determines the event should be handled
- **THEN** the hook SHALL return `1` to `CallNextHookEx` to swallow the event
- **AND** the event SHALL NOT be received by the background application

#### Scenario: Passing events through when not handled
- **WHEN** any mouse click event is intercepted
- **AND** the application logic determines the event should NOT be handled (e.g., menu is hidden)
- **THEN** the hook SHALL pass the event to `CallNextHookEx`
- **AND** the event SHALL be received normally by the background application
