# slot-wheel-editor-visualization

## ADDED Requirements

### Requirement: Occupied slots SHALL display a position identity badge
Each occupied slot on the wheel SHALL display a small position badge (the slot's ring position, 1-based) so users can correlate wheel positions with slot numbers in the runtime menu and in "Move to" flows.

#### Scenario: Position badge on a filled slot
- **WHEN** the wheel renders an occupied slot at ring position 3
- **THEN** the slot SHALL show a position badge labeled "3" (localized via `Settings.Slots.PositionFormat` where applicable)
- **AND** the badge SHALL remain legible against both light and dark themes

#### Scenario: Position badge on an empty slot
- **WHEN** the wheel renders an empty slot placeholder at ring position 5
- **THEN** the placeholder SHALL show its position number ("5") alongside the add affordance, so the user knows exactly which position they are filling

### Requirement: Empty slots SHALL present a clear add affordance
Empty slot placeholders SHALL communicate "click to add a slot here" through a dashed accent ring, a plus glyph, the position number, and a tooltip, rather than the current bare dashed circle with a plus.

#### Scenario: Empty slot renders
- **WHEN** the wheel renders an empty slot
- **THEN** it SHALL show a dashed ring, a centered plus icon, and the position number
- **AND** its tooltip SHALL be a localized "click to add slot at position N" message

#### Scenario: Empty slot hover
- **WHEN** the user hovers over an empty slot
- **THEN** the dashed ring SHALL highlight with the accent color and the cursor SHALL indicate the slot is interactive

### Requirement: Slots SHALL provide hover/active visual feedback
Slots SHALL respond to mouse-over with a subtle accent ring and scale so the wheel feels as responsive as the runtime radial menu.

#### Scenario: Hovering an occupied slot
- **WHEN** the pointer moves over an occupied slot
- **THEN** the slot SHALL show an accent-colored ring and a subtle scale-up (≤ 1.1×) without shifting layout

#### Scenario: Exiting hover
- **WHEN** the pointer leaves the slot
- **THEN** the ring and scale SHALL animate back to the resting state over ~150-250ms

### Requirement: The center state SHALL be visually represented
The wheel center SHALL show a recognizable center-state visual (a small center orb with a label) instead of the current raw "Center" text hint, so it reads as the menu's center/back area.

#### Scenario: Center renders
- **WHEN** the wheel renders its center area
- **THEN** it SHALL display a subtle center orb (or equivalent visual) with a localized label
- **AND** it SHALL remain non-interactive (the center is not editable in settings)

### Requirement: Guide ring and pager SHALL be visually cohesive
The dashed guide ring and the pager SHALL use shared slot styles and present a clear visual hierarchy consistent with the page's design language.

#### Scenario: Guide ring
- **WHEN** the wheel renders the guide ring
- **THEN** it SHALL use a consistent dashed/accent style from `SlotStyles.xaml` and remain non-interactive

#### Scenario: Pager
- **WHEN** the pager renders current page, total slots, and slots-per-page
- **THEN** it SHALL use the existing Wpf.Ui button styles and readable text colors with proper hierarchy (primary page count vs. secondary meta info)

### Requirement: All new user-facing strings SHALL be localized
Every new or changed user-facing string introduced by this redesign SHALL go through `ILocalizationService` (or `{lex:Locale}`), with matching entries in `Strings.resx` and `Strings.zh-CN.resx`.

#### Scenario: New tooltip string
- **WHEN** an empty slot tooltip is displayed
- **THEN** the text SHALL come from a localized resource, not a hardcoded literal

## Non-Requirements

- No change to the wheel layout math or slot positioning behavior.
- No change to the drag-reorder, pager, or right-click move-to interaction models.
- No change to the runtime radial menu itself.
