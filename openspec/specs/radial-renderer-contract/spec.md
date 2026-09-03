# radial-renderer-contract Specification

## Purpose
Defines the pluggable rendering seam for the radial menu: slot highlight application and decorative layer painting are injected through a renderer contract instead of being hard-coded in the slot XAML template.

## Requirements

### Requirement: Slot highlight SHALL be applied through an injectable renderer
The radial menu SHALL apply active-slot highlighting through a renderer abstraction rather than inline XAML style triggers, so the highlight treatment is swappable without editing the slot template.

#### Scenario: Active slot highlighted through renderer
- **WHEN** the active slot changes while the radial menu is visible
- **THEN** the renderer SHALL be invoked to apply the highlight treatment to the affected slot element
- **AND** the previous slot SHALL have its highlight treatment removed

#### Scenario: Renderer swappable without template edit
- **WHEN** a different renderer is configured
- **AND** the radial menu is opened
- **THEN** the configured renderer SHALL control the highlight treatment of slots
- **AND** the slot template SHALL NOT contain a hard-coded highlight effect for the active state

### Requirement: Default renderer SHALL preserve current visuals
The default renderer SHALL reproduce the existing active-slot glow appearance so that enabling the renderer layer does not change what users see.

#### Scenario: Default renderer matches current look
- **WHEN** the radial menu opens with the default renderer configured
- **THEN** the active-slot highlight SHALL be visually equivalent to the pre-change appearance

### Requirement: Decorative layer SHALL be renderable through the renderer
The radial menu SHALL support a decorative rendering pass (outside the per-slot template) driven by the renderer, leaving the canvas free of style-embedded decorations.

#### Scenario: Renderer paints decorations
- **WHEN** the radial menu canvas is laid out
- **THEN** the renderer SHALL be given the canvas and center geometry to paint decorations
- **AND** the decorations SHALL not intercept pointer input

### Requirement: Highlight SHALL honor the performance discipline
The default renderer SHALL avoid expensive per-slot effects that degrade frame rate under many slots, consistent with the project's existing radial-window performance constraint.

#### Scenario: No drop-shadow per highlighted slot in default renderer
- **WHEN** the default renderer highlights a slot
- **THEN** the highlight SHALL NOT rely on a per-slot `DropShadowEffect` where a cheaper equivalent (blur/gradient/opacity) preserves the look

### Requirement: Active renderer SHALL be resolved from configuration through a registry

The radial menu SHALL resolve its active renderer from the configured renderer id via a renderer registry/factory, rather than a DI-fixed single instance. Resolution SHALL consider plugin-contributed renderers first, then the built-in renderer set, and SHALL fall back safely to the Default renderer when the id is unknown or its owning plugin has been disabled or unloaded.

#### Scenario: Configured renderer id resolves
- **WHEN** the configured `RadialRenderer` value matches a registered renderer
- **THEN** the registry SHALL return that renderer
- **AND** the radial menu SHALL use it for slot highlighting and decorative painting

#### Scenario: Unknown renderer id falls back to Default
- **WHEN** the configured `RadialRenderer` value is not a registered renderer
- **THEN** the registry SHALL return the Default renderer
- **AND** the radial menu SHALL render without error

#### Scenario: Plugin renderer id resolves ahead of built-ins
- **WHEN** the configured `RadialRenderer` value matches a renderer contributed by an enabled plugin
- **THEN** the factory SHALL return the plugin-contributed renderer

#### Scenario: Disabled plugin renderer falls back to Default
- **WHEN** the configured `RadialRenderer` value matches a renderer whose owning plugin has been disabled or unloaded
- **THEN** the registry SHALL return the Default renderer
- **AND** the radial menu SHALL render without error

#### Scenario: Default configuration unchanged
- **WHEN** `RadialRenderer` is left at its default value (`"Default"`)
- **THEN** the resolved renderer SHALL behave exactly as before this change
