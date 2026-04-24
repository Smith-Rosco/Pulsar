## ADDED Requirements

### Requirement: Grouped root-slot modifier release SHALL use direct-switch semantics
When the highlighted root radial-menu slot represents multiple eligible windows for one process, modifier-release execution SHALL resolve and activate a default target window directly instead of opening the submenu.

#### Scenario: Modifier release executes grouped root slot
- **WHEN** the user highlights a grouped process slot in the root radial menu
- **AND** the user releases the execution modifier
- **THEN** Pulsar SHALL resolve a default target window for that process group
- **AND** Pulsar SHALL activate that resolved window
- **AND** Pulsar SHALL dismiss the menu

#### Scenario: Left click still drills into grouped slot submenu
- **WHEN** the user highlights a grouped process slot in the root radial menu
- **AND** the user left-clicks while the menu is visible
- **THEN** Pulsar SHALL open the grouped slot submenu instead of activating a default target window directly
