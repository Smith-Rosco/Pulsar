## ADDED Requirements

### Requirement: Sub-menu entry SHALL animate child slots from parent position
When entering a sub-menu, child slots SHALL animate radially outward from the triggering parent slot's canvas position to their final ring positions, establishing a spatial transition that connects parent to children.

#### Scenario: Enter sub-menu with multi-window process group
- **WHEN** the user clicks a process group slot with multiple windows
- **THEN** the system SHALL record the parent slot's current (X, Y) position as the animation origin
- **AND** each child slot SHALL initially appear at the parent slot's position
- **AND** each child slot SHALL animate to its target ring position over 250ms with elastic easing
- **AND** the animation SHALL start after a 120ms delay (allowing content swap to complete)

#### Scenario: Center slot transitions to breadcrumb during entry
- **WHEN** the sub-menu entry animation begins
- **THEN** the center slot SHALL display the parent process name as label text
- **AND** the center slot's action strategy SHALL be `BackActionStrategy` (return to root menu)

#### Scenario: Sub-menu exit SHALL animate slots back to parent position
- **WHEN** the user clicks the center Back button or presses Back key to return to root menu
- **THEN** all child slots SHALL animate from their current positions toward the original parent slot's position over 200ms with ease-in-out easing
- **AND** the center slot SHALL restore its root menu content after the reverse animation completes

#### Scenario: Sub-menu is entered from a scrollable page
- **WHEN** the triggering parent slot is on a page other than the first page
- **THEN** the animation origin SHALL be the parent slot's current rendered position on the visible canvas
- **AND** the animation SHALL NOT trigger a page transition during sub-menu entry

### Requirement: Sub-menu transition SHALL reuse existing animation infrastructure
The sub-menu transition animation SHALL use the existing `AnimateToLayoutAsync` mechanism for radius/center/slot size changes, and SHALL extend `SlotViewModel` offset properties for per-slot position animation.

#### Scenario: Combined layout and position animation
- **WHEN** `EnterSubMenuAsync` is called
- **THEN** the existing radius expansion animation (×1.10), center expansion (×1.16) SHALL play concurrently with per-slot position animations
- **AND** both animation tracks SHALL complete within the same 250ms duration

#### Scenario: Animation respects cancellation
- **WHEN** a sub-menu entry animation is in progress and a new request to dismiss the menu arrives
- **THEN** the animation SHALL be cancellable via CancellationToken
- **AND** the menu SHALL dismiss immediately without waiting for animation completion
