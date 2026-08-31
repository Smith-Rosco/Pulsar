# right-drag-release-race Specification

## Purpose
TBD - created by archiving change fix-right-drag-release-passthrough. Update Purpose after archive.

## Requirements

### Requirement: Config refresh SHALL NOT clear an in-flight gesture

Applying a configuration change (which currently calls `RightDragGestureDetector.Reset()`) SHALL NOT discard an active gesture's pressed/summoned state. The new configuration SHALL take effect after the current gesture completes.

#### Scenario: Config change during a held gesture
- **WHEN** a right-drag gesture is in progress (button pressed)
- **AND** the configuration is updated (e.g. `ConfigUpdated` fires, settings saved)
- **THEN** the detector's pressed/summoned state SHALL be preserved
- **AND** the gesture SHALL complete normally on release
- **AND** the updated configuration SHALL be applied at the next gesture boundary

#### Scenario: Config change while idle
- **WHEN** no gesture is in progress
- **AND** the configuration is updated
- **THEN** the detector SHALL be reset immediately
- **AND** the updated configuration SHALL take effect immediately

### Requirement: Right-button release SHALL NOT leak while the menu is visible

A right-button release observed while the radial menu is visible SHALL be swallowed and routed to menu handling, regardless of detector state. A visible menu must never leak its release to the source application.

#### Scenario: Release routed to menu even if detector state lost
- **WHEN** the radial menu is visible
- **AND** a right-button release is intercepted
- **AND** the gesture detector reports `None` (state lost or reset by an external path)
- **THEN** the release SHALL still be swallowed
- **AND** the release SHALL be routed to `HandleGestureRightReleaseAsync`
- **AND** the source application SHALL NOT receive the release

#### Scenario: Release passthrough when no menu and no gesture
- **WHEN** the radial menu is not visible
- **AND** no gesture press is tracked
- **THEN** the right-button release SHALL pass through to the source application normally
