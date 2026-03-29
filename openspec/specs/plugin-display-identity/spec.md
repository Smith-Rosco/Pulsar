## ADDED Requirements

### Requirement: Built-in plugins SHALL expose a canonical display identity
Built-in plugins SHALL define a canonical user-facing display identity that is used consistently in plugin metadata, plugin pickers, and plugin documentation.

#### Scenario: Plugin picker renders built-in plugins
- **WHEN** the settings UI loads metadata for built-in plugins
- **THEN** it SHALL receive one canonical display name per plugin rather than conflicting names across metadata and documentation

#### Scenario: Plugin documentation references a built-in plugin
- **WHEN** a built-in plugin documentation page is published or linked from metadata
- **THEN** the page SHALL use the same canonical plugin name presented in the product UI

### Requirement: Built-in plugin descriptions SHALL communicate user intent rather than internal implementation labels
Built-in plugin descriptions SHALL explain what the user can accomplish with the plugin and SHALL avoid relying on ambiguous or overlapping internal terminology.

#### Scenario: Two plugins have adjacent capabilities
- **WHEN** users compare built-in plugins that both appear to launch or control applications
- **THEN** each plugin description SHALL make the intended usage distinction understandable without reading source code

#### Scenario: Plugin metadata is used as fallback documentation
- **WHEN** a slot editor or plugin picker displays only metadata fields
- **THEN** the plugin description SHALL still communicate the plugin's primary purpose clearly enough to guide selection

### Requirement: Plugin display identity SHALL NOT use accent color as a visual icon differentiator
The system SHALL represent plugin identity through icon glyph and name only. Plugin accent color (`UIHints.AccentColor`) SHALL NOT be applied as a background color on icon containers in any settings or picker surface. The `AccentColor` field is retained in the data model but is reserved for future non-icon uses.

#### Scenario: Plugin card on settings page does not show accent-colored icon background
- **WHEN** the Plugins settings page displays a plugin card for any plugin (e.g. WinSwitcher, PKI, VbaRunner)
- **THEN** the icon background SHALL be the standard neutral theme fill and SHALL NOT be the plugin's `AccentColor` hex value

#### Scenario: AccentColor field is present but not visually applied
- **WHEN** a plugin defines a non-empty `AccentColor` in its `UIHints`
- **THEN** the system SHALL retain that value in memory but SHALL NOT render it as an icon background color on any current surface
