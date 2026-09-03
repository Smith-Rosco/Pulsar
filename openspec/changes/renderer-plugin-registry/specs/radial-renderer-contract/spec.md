## MODIFIED Requirements

### Requirement: Active renderer SHALL be resolved from configuration through a registry

The radial menu SHALL resolve its active renderer from the configured renderer id via a renderer registry/factory, rather than a DI-fixed single instance. Resolution SHALL consider plugin-contributed renderers first, then the built-in renderer set, and SHALL fall back safely to the Default renderer when the id is unknown or its owning plugin has been disabled or unloaded.

#### Scenario: Configured renderer id resolves
- **WHEN** the configured `RadialRenderer` value matches a registered renderer
- **THEN** the registry SHALL return that renderer
- **AND** the radial menu SHALL use it for slot highlighting and decorative painting

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
