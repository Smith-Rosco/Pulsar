# mouse-tracking-service

## Purpose
Define the mouse-tracking and coordinate-conversion behaviors that allow the radial menu to resolve dead zones and hovered slots from global cursor movement.

## Requirements

### Requirement: Mouse position tracking
The system SHALL provide mouse position tracking relative to the radial menu center, enabling hit testing for slot activation.

#### Scenario: Track global cursor position
- **WHEN** the radial menu is visible and the cursor moves globally
- **THEN** the service SHALL publish the cursor's position relative to the menu center within 16ms

#### Scenario: Dead zone detection
- **WHEN** the cursor is within the dead zone radius (60-65% of center orb size based on slot count)
- **THEN** `IsInDeadZone` SHALL return true and `HoveredSlotIndex` SHALL return 0

#### Scenario: Slot hit testing
- **WHEN** the cursor is outside the dead zone
- **THEN** `HoveredSlotIndex` SHALL return the 1-based index of the hovered slot (1-12)

### Requirement: Tracking lifecycle management
The system SHALL allow start/stop tracking to optimize performance when menu is hidden.

#### Scenario: Start tracking
- **WHEN** `StartTracking()` is called
- **THEN** the service SHALL begin publishing mouse position updates

#### Scenario: Stop tracking
- **WHEN** `StopTracking()` is called
- **THEN** the service SHALL cease publishing updates and release resources

### Requirement: Coordinate space conversion
The system SHALL convert screen coordinates to menu-local coordinates for hit testing.

#### Scenario: Convert screen to local coordinates
- **WHEN** cursor is at screen position `(sx, sy)` and menu window is at `(wx, wy)`
- **THEN** the relative position SHALL be `(sx - wx, sy - wy)`
