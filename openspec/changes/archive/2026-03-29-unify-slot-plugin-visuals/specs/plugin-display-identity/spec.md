## MODIFIED Requirements

### Requirement: Built-in plugins SHALL expose a canonical display identity
Built-in plugins SHALL define a canonical user-facing display identity that is used consistently in plugin metadata, plugin pickers, plugin management surfaces, and plugin documentation. Create Slot and the Plugins settings page SHALL resolve built-in plugin icon, display name, description, and category from the same canonical identity source rather than maintaining separate page-local overrides.

#### Scenario: Plugin picker renders built-in plugins
- **WHEN** the settings UI loads metadata for built-in plugins
- **THEN** it SHALL receive one canonical display name per plugin rather than conflicting names across metadata and documentation

#### Scenario: Plugin documentation references a built-in plugin
- **WHEN** a built-in plugin documentation page is published or linked from metadata
- **THEN** the page SHALL use the same canonical plugin name presented in the product UI

#### Scenario: Create Slot and Plugins page render the same built-in plugin
- **WHEN** the Create Slot picker and the Plugins settings page both render a built-in plugin
- **THEN** they SHALL use the same canonical icon, display name, description, and category source for that plugin
