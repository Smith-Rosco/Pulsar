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
