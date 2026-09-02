## MODIFIED Requirements

### Requirement: Sub-threshold release SHALL replay a synthetic click to the source application

When a right-button gesture is active but the displacement never reached `GestureDragThreshold` at release, the system SHALL replay a synthetic right-button down/up to the source application so the native context menu still appears.

#### Scenario: Plain click with modifier still shows native context menu
- **WHEN** the user presses the right button with the configured modifier
- **AND** releases it without dragging beyond `GestureDragThreshold`
- **AND** `GestureSummonMode` is `OnThreshold`
- **THEN** the system SHALL NOT summon the Pulsar menu
- **AND** the system SHALL synthesize a right-button down/up at the current cursor position
- **AND** the native context menu SHALL appear as if the user had right-clicked normally

#### Scenario: Gesture release executes the selection
- **WHEN** the menu was summoned by the gesture (either mode)
- **AND** the gesture passed the isolation filter at button-down (the isolation filter is enabled and evaluated the foreground window as eligible, or the filter is disabled)
- **AND** the user releases the right button
- **THEN** the release SHALL resolve to the menu selection
- **AND** the release SHALL NOT be delivered to the source application

#### Scenario: Gesture denied by isolation never enters the state machine
- **WHEN** the isolation filter is enabled
- **AND** it denies the gesture at right-button down
- **THEN** the right-button press SHALL pass through to the foreground application
- **AND** the release SHALL NOT be swallowed and SHALL NOT resolve to any menu selection
