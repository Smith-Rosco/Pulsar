## Purpose

Defines the multi-style radial renderer system: a factory that resolves the active renderer from configuration, two additional visual forms (ClassicRing and Glassmorphism) on top of the existing Default renderer, per-renderer decorative-layer painting, and settings-page selection for renderer style and theme preset.

## ADDED Requirements

### Requirement: Active renderer SHALL be resolved from configuration through a factory

The radial menu SHALL resolve its active renderer via a `StyleRendererFactory`-equivalent registry driven by the configured renderer id, instead of a DI-fixed single instance. An unknown or missing renderer id SHALL resolve safely to the Default renderer without error.

#### Scenario: Configured renderer style resolves
- **WHEN** `ProfileSettings.RadialRenderer` is set to a registered renderer id
- **THEN** the factory SHALL return the renderer for that id
- **AND** the radial menu SHALL use it for slot highlighting and decorative painting

#### Scenario: Unknown renderer falls back safely
- **WHEN** the configured renderer id is not registered
- **THEN** the factory SHALL return the Default renderer
- **AND** the radial menu SHALL render without error using the Default visuals

### Requirement: Multiple renderer styles SHALL be available

Beyond the Default renderer, the system SHALL ship at least two additional renderer styles — a ClassicRing form and a Glassmorphism form — each providing its own active-slot highlight treatment and decorative layer, so users can switch the menu's visual form from settings.

#### Scenario: ClassicRing style provides ring visuals
- **WHEN** the ClassicRing renderer is active
- **THEN** the active slot SHALL be highlighted with a ring-style treatment
- **AND** the menu SHALL paint ring-style decorations via the decorative pass

#### Scenario: Glassmorphism style provides glass visuals
- **WHEN** the Glassmorphism renderer is active
- **THEN** the active slot SHALL be highlighted with a glassmorphism-style treatment (translucent layered surface)
- **AND** the menu SHALL paint glass-style decorations via the decorative pass

### Requirement: Renderer decorative layer SHALL be theme-driven

Each renderer SHALL paint its decorative layer from brushes supplied through the renderer contract (tokens / per-renderer resource pack), never from hard-coded colors embedded in the slot template. Decorations SHALL not intercept pointer input.

#### Scenario: Decorations derive from tokens
- **WHEN** a renderer paints its decorative layer
- **THEN** the brushes SHALL come from the renderer's token/resources, not inline hex values in the slot XAML

#### Scenario: Decorations do not block input
- **WHEN** the decorative layer is rendered over the radial canvas
- **THEN** pointer events SHALL reach the slots beneath as if the decorations were absent

### Requirement: Renderer style SHALL be selectable in settings

The settings page SHALL expose a control to choose the radial renderer style. A selection SHALL take effect on the next menu open (and on config update while the app is running) without restarting.

#### Scenario: Renderer style changed in settings
- **WHEN** the user selects a different renderer style in settings and saves
- **THEN** subsequent menu opens SHALL render with the newly selected style
- **AND** the mode tone (cool Task / warm Action) SHALL remain applied

## Non-Requirements

- No plugin-published third-party renderers (deferred to a future change).
- No change to the existing Default renderer's visual output.
- No change to slot layout math, paging, or hit-testing.
