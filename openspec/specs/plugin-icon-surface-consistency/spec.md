# plugin-icon-surface-consistency

## Purpose
Define how plugin icons render consistently across all surfaces where plugins are presented to the user, including the Plugins settings page and the Add Slot picker.
## Requirements
### Requirement: Plugin icons SHALL render with neutral background on all settings surfaces
The system SHALL render plugin icons using a neutral-background rounded-rectangle container on both the Plugins settings page and the Add Slot picker, so that a user encountering the same plugin on either surface sees a visually consistent icon representation. Per-plugin accent colors SHALL NOT be applied to the icon background.

#### Scenario: Plugin icon on Plugins settings page uses neutral background
- **WHEN** the Plugins settings page renders a plugin card
- **THEN** the plugin icon container SHALL use the standard neutral theme fill (`ControlFillColorSecondaryBrush`) as its background color and SHALL NOT apply a per-plugin accent color to the icon background

#### Scenario: Plugin icon on Plugins settings page renders inside a rounded-rectangle container
- **WHEN** the Plugins settings page renders a plugin card
- **THEN** the `SlotOrb` inside the icon container SHALL render with `IsTransparent="True"` so no circular frame is drawn inside the rounded-rectangle container

#### Scenario: Plugin icon in Add Slot picker renders inside a rounded-rectangle container without double border
- **WHEN** the Add Slot picker renders a plugin type card
- **THEN** the plugin icon SHALL be rendered by `SlotOrb` with `IsTransparent="True"` inside a rounded-rectangle `Border` container consistent with `SlotOrbContainerStyle`, so no circle-inside-square or double-border artifact is visible

#### Scenario: Same plugin appears consistently across both surfaces
- **WHEN** a user views the same plugin on the Plugins settings page and in the Add Slot picker
- **THEN** the icon shape and background treatment SHALL be visually consistent between the two surfaces

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

