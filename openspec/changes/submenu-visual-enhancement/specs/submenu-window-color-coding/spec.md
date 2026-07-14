## ADDED Requirements

### Requirement: Sub-menu slots for same-process windows SHALL receive distinct color tokens
When a sub-menu displays multiple windows from the same process, each slot SHALL receive a distinct border/stroke color assigned deterministically from a fixed palette based on the window's sort order.

#### Scenario: Three Chrome windows enter sub-menu
- **WHEN** a process group with 3 windows opens a sub-menu
- **AND** windows are sorted by `FirstSeenTime` (ascending)
- **THEN** window at index 0 SHALL receive palette color #1
- **AND** window at index 1 SHALL receive palette color #2
- **AND** window at index 2 SHALL receive palette color #3
- **AND** the color assignment SHALL be stable: re-entering the sub-menu SHALL produce the same color for each window

#### Scenario: Only one window in sub-menu
- **WHEN** a sub-menu contains exactly one window (no ambiguity)
- **THEN** no color token SHALL be applied (default theme stroke is sufficient)

#### Scenario: More windows than palette colors
- **WHEN** the sub-menu contains more windows than the available palette colors (8)
- **THEN** color assignment SHALL cycle: index 8 SHALL reuse color #1, index 9 SHALL reuse color #2, etc.

### Requirement: Color token SHALL be applied as slot stroke
The assigned color SHALL be rendered as a colored border/stroke on the `SlotOrb` control, distinct from the slot's custom fill color (if any).

#### Scenario: Color applied to slot rendering
- **WHEN** a sub-menu slot has a color token assigned
- **THEN** the slot's `CustomStrokeBrush` SHALL be set to a `SolidColorBrush` with the assigned palette color at 90% opacity
- **AND** the slot's `CustomFillBrush` SHALL remain unchanged (theme default or custom slot color)
- **AND** the color SHALL be visible alongside any existing type badge or health badge

#### Scenario: Slot already has a custom color from config
- **WHEN** a sub-menu slot has both a user-configured custom color AND a sub-menu color token
- **THEN** the sub-menu color token SHALL take precedence for the stroke
- **AND** the user-configured fill color SHALL remain on the fill layer

### Requirement: Color palette SHALL provide visual distinction in both themes
The color palette SHALL use hues that are visually distinguishable in both the light theme and dark theme.

#### Scenario: Dark theme color visibility
- **WHEN** the dark theme is active
- **THEN** all palette colors SHALL have sufficient contrast against the dark `Theme.Orb.Fill` background (#2D2D2D)

#### Scenario: Light theme color visibility
- **WHEN** the light theme is active
- **THEN** all palette colors SHALL have sufficient contrast against the light `Theme.Orb.Fill` background (#FFFFFF)

### Requirement: Color tokens SHALL clear on return to root menu
When exiting the sub-menu and returning to the root menu, all color tokens applied by the sub-menu system SHALL be cleared.

#### Scenario: Restore root menu
- **WHEN** `RestoreRootMenu()` is called
- **THEN** all slots with sub-menu color tokens SHALL have their `CustomStrokeBrush` and `CustomFillBrush` reset to the root menu page provider's configured values
- **AND** no residual sub-menu color SHALL leak into the root menu display
