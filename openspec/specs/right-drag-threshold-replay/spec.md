# right-drag-threshold-replay Specification

## Purpose
TBD - created by archiving change fix-right-drag-release-passthrough. Update Purpose after archive.

## Requirements

### Requirement: Right-drag gesture SHALL support displacement-threshold activation

The right-drag gesture SHALL track cursor displacement from the button-down position. When `GestureSummonMode` is `OnThreshold`, the menu SHALL be summoned only after the displacement exceeds `GestureDragThreshold` (default 25 DIPs), matching the reference implementation's "menu appears once you drag" behavior.

#### Scenario: OnThreshold summon on drag
- **WHEN** `GestureSummonMode` is `OnThreshold`
- **AND** the user presses the right button with the configured modifier
- **AND** drags beyond `GestureDragThreshold` DIPs from the down position
- **THEN** the menu SHALL be summoned at the down position
- **AND** the summon SHALL happen exactly once per gesture

#### Scenario: Immediate summon preserved by default
- **WHEN** `GestureSummonMode` is `Immediate` (the default)
- **AND** the user presses the right button with the configured modifier
- **THEN** the menu SHALL be summoned at button-down (current behavior unchanged)

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
- **AND** the user releases the right button
- **THEN** the release SHALL resolve to the menu selection
- **AND** the release SHALL NOT be delivered to the source application

### Requirement: Replayed clicks SHALL NOT be re-intercepted

The low-level hook SHALL suppress its own replayed input so it does not loop back through gesture detection.

#### Scenario: Replayed click passes through the hook
- **WHEN** the system replays a synthetic right-click after a sub-threshold release
- **THEN** the synthetic down/up SHALL pass straight through `CallNextHookEx`
- **AND** they SHALL NOT be raised to gesture or menu subscribers as user input
- **AND** no second replay SHALL be triggered by the replayed events
