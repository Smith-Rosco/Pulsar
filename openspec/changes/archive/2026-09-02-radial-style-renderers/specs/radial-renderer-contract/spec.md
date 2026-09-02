## ADDED Requirements

### Requirement: Active renderer SHALL be resolved from configuration through a registry

The radial menu SHALL resolve its active renderer from the configured renderer id via a renderer registry/factory, rather than a DI-fixed single instance. An unknown renderer id SHALL resolve safely to the Default renderer without error.

#### Scenario: Configured renderer id resolves
- **WHEN** the configured `RadialRenderer` value matches a registered renderer
- **THEN** the registry SHALL return that renderer
- **AND** the radial menu SHALL use it for slot highlighting and decorative painting

#### Scenario: Unknown renderer id falls back to Default
- **WHEN** the configured `RadialRenderer` value is not a registered renderer
- **THEN** the registry SHALL return the Default renderer
- **AND** the radial menu SHALL render without error

#### Scenario: Default configuration unchanged
- **WHEN** `RadialRenderer` is left at its default value (`"Default"`)
- **THEN** the resolved renderer SHALL behave exactly as before this change
