## ADDED Requirements

### Requirement: Theme preset SHALL be selectable through a settings UI

The settings page SHALL expose a control for choosing the radial theme preset (`System` / `Dark` / `Light` / named presets). A selection SHALL take effect on the next menu open (and on config update while the app is running) without restarting, and SHALL continue to resolve through the existing preset layer with fallback behavior.

#### Scenario: Preset selected in settings
- **WHEN** the user selects a named preset in the settings UI and saves
- **THEN** subsequent menu opens SHALL resolve the radial tokens from that preset
- **AND** the selected value SHALL be persisted in `Profiles.json`

#### Scenario: System preset follows OS
- **WHEN** the user selects the `System` preset in the settings UI
- **THEN** subsequent menu opens SHALL follow the current Windows dark/light mode

#### Scenario: Unknown persisted preset still falls back
- **WHEN** the persisted preset value is not a recognized preset (e.g. stale config)
- **THEN** the resolver SHALL fall back to the active theme default without error
- **AND** the settings UI SHALL still display the persisted value or a safe fallback
