## ADDED Requirements

### Requirement: Radial menu SHALL support wheel paging during drag-session invocation
When Pulsar is invoked while the user is actively performing a Windows drag-and-drop operation, the radial menu SHALL continue to accept mouse-wheel paging input for eligible root-level menus.

#### Scenario: User pages switcher while dragging a file
- **WHEN** the user holds the left mouse button to drag a file, invokes Pulsar, and rotates the mouse wheel while the radial menu is visible at the root level with more than one page available
- **THEN** Pulsar SHALL change pages in the visible radial menu without requiring the drag operation to end first

#### Scenario: User pages action grid while dragging
- **WHEN** the user invokes the radial menu during an active drag session in a mode whose root content spans multiple pages and rotates the mouse wheel
- **THEN** Pulsar SHALL move to the next or previous page using the same direction rules as ordinary non-drag invocation

### Requirement: Pulsar SHALL only consume global wheel input during eligible menu sessions
Pulsar MUST only treat global wheel input as menu paging input while the radial menu is visible and the current menu state is eligible for paging.

#### Scenario: Menu is hidden
- **WHEN** the radial menu is not visible and the user rotates the mouse wheel anywhere in the system
- **THEN** Pulsar SHALL NOT consume or reinterpret that wheel input

#### Scenario: User is inside a submenu
- **WHEN** the radial menu is visible but the current menu state is not the root paging state and the user rotates the mouse wheel
- **THEN** Pulsar SHALL NOT change pages

### Requirement: Existing paging feedback SHALL remain consistent under drag-session input
Wheel gestures received through the drag-session path MUST preserve the same paging feedback semantics already used by the radial menu for ordinary wheel paging.

#### Scenario: Only one page is available
- **WHEN** the radial menu is visible during a drag session and the user rotates the mouse wheel while only one page exists
- **THEN** Pulsar SHALL show the existing single-page feedback behavior instead of changing pages

#### Scenario: User scrolls past the first or last page
- **WHEN** the radial menu is visible during a drag session and the user rotates the mouse wheel past a paging boundary
- **THEN** Pulsar SHALL preserve the existing boundary feedback behavior instead of wrapping to another page

### Requirement: Normal non-drag wheel paging SHALL remain functional
Adding drag-session wheel paging MUST NOT regress ordinary wheel-based paging when Pulsar is invoked outside of a drag-and-drop session.

#### Scenario: User invokes Pulsar normally and pages with the wheel
- **WHEN** the user invokes Pulsar without an active drag session and rotates the mouse wheel on a root menu with multiple pages
- **THEN** Pulsar SHALL continue to page exactly once per handled paging gesture
