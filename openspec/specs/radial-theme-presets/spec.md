# radial-theme-presets Specification

## Purpose
Models the radial menu's theme brushes as typed tokens and resolves the configured theme value (system/dark/light/named preset) to a concrete, fallback-safe token set.

## Requirements

### Requirement: Radial theme brushes SHALL be addressable as typed tokens
The radial menu SHALL expose its theme brushes (`Orb.Fill`, `Orb.Stroke`, `Orb.Text`, `Orb.Active.Glow`, `Orb.Label.*`, `Accent.*`, `Radial.*`) through a typed token model that renderers and theme application can consume without referencing raw resource-key strings.

#### Scenario: Renderer consumes typed tokens
- **WHEN** a renderer requests the active-glow brush
- **THEN** it SHALL receive a brush resolved from the typed token model
- **AND** the resolved brush SHALL match the value in the active theme resources

#### Scenario: Token set reflects the active theme
- **WHEN** the theme switches between Dark and Light
- **THEN** the typed token set SHALL resolve to the brushes of the newly active theme

### Requirement: Configured theme value SHALL resolve through a preset layer
The system SHALL resolve a configured theme/preset value (`System`, `Dark`, `Light`, or a named preset) to a concrete token set before theme application, with an explicit fallback when the value is unknown.

#### Scenario: System resolves from OS
- **WHEN** the configured value is `System`
- **THEN** the resolved theme SHALL follow the current Windows dark/light mode

#### Scenario: Named preset resolves to a token set
- **WHEN** the configured value names a supported preset
- **THEN** the preset SHALL resolve to its defined token set

#### Scenario: Unknown value falls back safely
- **WHEN** the configured value is not a recognized theme or preset
- **THEN** the resolver SHALL fall back to a default token set
- **AND** the radial menu SHALL render with the fallback without error

### Requirement: Default configuration SHALL preserve current visuals
With default settings, the preset layer SHALL resolve to the existing Dark/Light behavior so no visual change occurs for existing users.

#### Scenario: Defaults reproduce current look
- **WHEN** a user opens the radial menu with default renderer and preset settings
- **THEN** the appearance SHALL be equivalent to the pre-change appearance for both Dark and Light themes

### Requirement: Built-in theme dictionaries SHALL remain the source of default tokens
The existing `Theme.Dark.xaml` / `Theme.Light.xaml` resource keys SHALL remain valid as the source values for the two built-in token sets, so other surfaces depending on those keys are unaffected.

#### Scenario: Existing resource keys stay valid
- **WHEN** a surface that is not renderer-aware reads the existing `Theme.*` resource keys
- **THEN** those keys SHALL continue to resolve to valid brushes in both themes
