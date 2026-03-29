## ADDED Requirements

### Requirement: Plugin icons SHALL render as neutral circles on all settings surfaces
The system SHALL render plugin icons using the same neutral-background circular `SlotOrb` visual treatment on both the Plugins settings page and the Add Slot picker, so that a user encountering the same plugin on either surface sees a visually consistent icon representation.

#### Scenario: Plugin icon on Plugins settings page uses neutral background
- **WHEN** the Plugins settings page renders a plugin card
- **THEN** the plugin icon container SHALL use the standard neutral theme fill (`ControlFillColorSecondaryBrush`) as its background color and SHALL NOT apply a per-plugin accent color to the icon background

#### Scenario: Plugin icon on Plugins settings page renders as a circle
- **WHEN** the Plugins settings page renders a plugin card
- **THEN** the `SlotOrb` inside the icon container SHALL render its own circular shape (i.e. `IsTransparent` SHALL be `False`), so the icon appears as a circle consistent with radial menu slots

#### Scenario: Plugin icon in Add Slot picker renders as a circle without double border
- **WHEN** the Add Slot picker renders a plugin type card
- **THEN** the plugin icon SHALL be rendered directly by `SlotOrb` without an additional outer `Border` container, so no square-inside-circle or double-border artifact is visible

#### Scenario: Same plugin appears consistently across both surfaces
- **WHEN** a user views the same plugin on the Plugins settings page and in the Add Slot picker
- **THEN** the icon shape and background treatment SHALL be visually consistent between the two surfaces
