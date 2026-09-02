# flick-out-cancel Specification

## Purpose
Defines the real-time escape (flick-out) state for a gesture-summoned radial menu: tracking cursor displacement from the menu center while visible, dimming the menu when the cursor exits the flick-out radius, and resolving a release inside the escape state as a cancel instead of a selection.

## Requirements

### Requirement: Gesture menu tracks escape state from cursor displacement

While a gesture-summoned menu is visible, the system SHALL track the cursor's displacement from the menu center. When the displacement exceeds the flick-out cancel radius (default: 1.5 × the current wheel radius), the menu SHALL enter the escape state. While in the escape state the menu SHALL be visually dimmed as a cancel preview. When the cursor re-enters the radius, the escape state SHALL be cleared and the menu visuals SHALL return to normal.

#### Scenario: Enter escape state on flick-out
- **WHEN** a gesture-summoned menu is visible
- **AND** the cursor displacement from the menu center exceeds the flick-out radius (1.5 × wheel radius)
- **THEN** the menu SHALL enter the escape state
- **AND** the menu visuals SHALL dim (fade to a reduced-opacity cancel preview)

#### Scenario: Escape state clears on re-entry
- **WHEN** the menu is in the escape state
- **AND** the cursor moves back within the flick-out radius
- **THEN** the menu SHALL leave the escape state
- **AND** the menu visuals SHALL return to normal (undim)

### Requirement: Escape-state release cancels the gesture

When the user releases the right button while the menu is in the escape state, the system SHALL cancel the gesture: the menu SHALL close without executing any selection, without quick-switching, and without delivering the release to the source application.

#### Scenario: Flick-out release cancels without action
- **WHEN** the gesture-summoned menu is in the escape state
- **AND** the user releases the right button
- **THEN** the menu SHALL close
- **AND** no slot selection SHALL be executed
- **AND** no quick-switch SHALL be performed
- **AND** the release SHALL NOT be delivered to the source application

### Requirement: Non-escape release resolves by spatial position

A release outside the escape state SHALL resolve exactly as today: releasing in the center zone quick-switches, releasing over a slot selects it, releasing over empty space dismisses. The escape state SHALL NOT alter this behavior.

#### Scenario: Normal release inside the radius resolves by position
- **WHEN** the gesture-summoned menu is visible and NOT in the escape state
- **AND** the user releases the right button
- **THEN** the release SHALL resolve by cursor position as before the change (center quick-switch / slot selection / empty-space dismiss)

### Requirement: Flick-out cancel is configurable and gesture-path-only

The flick-out cancel behavior SHALL be configurable in settings (enable toggle and radius multiplier, default 1.5). It SHALL apply only to gesture-summoned menus; hotkey-summoned menus SHALL be unaffected.

#### Scenario: Disabled keeps current behavior
- **WHEN** the flick-out cancel option is disabled
- **AND** a gesture-summoned menu is visible
- **AND** the cursor moves beyond the wheel radius
- **THEN** the menu SHALL NOT enter the escape state
- **AND** releases SHALL resolve by spatial position as before

#### Scenario: Hotkey menu unaffected
- **WHEN** a menu is summoned by hotkey (not by gesture)
- **AND** the cursor moves beyond the wheel radius
- **THEN** the menu SHALL NOT enter the escape state
