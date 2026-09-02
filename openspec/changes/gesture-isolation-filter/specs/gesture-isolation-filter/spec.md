## Purpose

Defines the pre-takeover isolation decision for the right-drag summon gesture: whether the gesture may swallow a right-button press depends on the foreground window's context (fullscreen state and process allow/block lists), so the gesture never hijacks right-clicks in apps or surfaces where it should not.

## ADDED Requirements

### Requirement: Gesture isolation evaluation gates right-button takeover

Before the system swallows a right-button down for the right-drag summon gesture, it SHALL evaluate the foreground window at the moment of button-down. When the isolation filter denies the gesture, the system SHALL treat the right-button press as a normal right-click and pass it through to the foreground application untouched.

#### Scenario: Gesture allowed on eligible foreground window
- **WHEN** the user presses the right button with a configured gesture modifier
- **AND** the isolation filter evaluates the foreground window as eligible for the gesture
- **THEN** the system SHALL swallow the right-button down and proceed with the existing gesture state machine (immediate summon or threshold tracking)

#### Scenario: Gesture denied passes the click through
- **WHEN** the user presses the right button with a configured gesture modifier
- **AND** the isolation filter evaluates the foreground window as NOT eligible for the gesture
- **THEN** the system SHALL NOT swallow the right-button down
- **AND** the right-button down SHALL be delivered to the foreground application
- **AND** the gesture state machine SHALL NOT enter a pressed/summoned state for this press

### Requirement: Fullscreen foreground window blocks the gesture by default

The system SHALL detect whether the foreground window is a fullscreen application. When the foreground is fullscreen and the fullscreen-protection option is enabled, the gesture SHALL be denied. Shell surfaces (`Progman`, `WorkerW`, `Shell_TrayWnd`) SHALL NOT be classified as fullscreen by this check, so a gesture over the desktop or taskbar is evaluated only by the process allow/block lists.

#### Scenario: Fullscreen app denies the gesture
- **WHEN** the user holds a configured gesture modifier and presses the right button
- **AND** the foreground window is a fullscreen application
- **AND** the fullscreen-protection option is enabled
- **THEN** the system SHALL deny the gesture
- **AND** the right-click SHALL pass through to the fullscreen application

#### Scenario: Shell surfaces are not treated as fullscreen
- **WHEN** the foreground window class name is `Progman`, `WorkerW`, or `Shell_TrayWnd`
- **THEN** the system SHALL NOT classify the surface as fullscreen
- **AND** the gesture evaluation SHALL fall through to the process allow/block lists

### Requirement: Process isolation supports allow-list and block-list modes

The system SHALL support two isolation modes, configured by `IsolationMode`: `Allowlist` and `Blocklist`, matched against the foreground process name.

- `Allowlist`: the gesture SHALL be allowed only when the foreground process is on the list.
- `Blocklist`: the gesture SHALL be allowed only when the foreground process is NOT on the list.
- An empty allow-list SHALL deny all gestures in `Allowlist` mode; an empty block-list SHALL deny none in `Blocklist` mode.

#### Scenario: Allow-list permits a listed process
- **WHEN** `IsolationMode` is `Allowlist`
- **AND** the foreground process name is present on the allow list
- **THEN** the isolation filter SHALL allow the gesture

#### Scenario: Allow-list blocks an unlisted process
- **WHEN** `IsolationMode` is `Allowlist`
- **AND** the foreground process name is NOT present on the allow list
- **THEN** the isolation filter SHALL deny the gesture

#### Scenario: Block-list blocks a listed process
- **WHEN** `IsolationMode` is `Blocklist`
- **AND** the foreground process name is present on the block list
- **THEN** the isolation filter SHALL deny the gesture

#### Scenario: Block-list permits an unlisted process
- **WHEN** `IsolationMode` is `Blocklist`
- **AND** the foreground process name is NOT present on the block list
- **THEN** the isolation filter SHALL allow the gesture

### Requirement: Isolation filter is opt-in and default-preserving

The gesture isolation filter SHALL be disabled by default so existing behavior is unchanged until a user enables it in settings. When disabled, every right-button press with a configured gesture modifier SHALL be treated as eligible and proceed through the existing gesture state machine.

#### Scenario: Filter disabled preserves current behavior
- **WHEN** the gesture isolation filter is disabled
- **AND** the user presses the right button with a configured gesture modifier
- **THEN** the system SHALL allow the gesture without any fullscreen or process-list evaluation
