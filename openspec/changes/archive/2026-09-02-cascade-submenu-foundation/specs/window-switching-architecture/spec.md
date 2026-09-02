## ADDED Requirements

### Requirement: Window-switch submenu SHALL be configurable through the generic coordinator
Window-group submenu configuration SHALL be delivered through the generic submenu coordinator as one concrete strategy, without changing the shared window-selection contract.

#### Scenario: Window strategy delegates selection
- **WHEN** the window submenu strategy configures a submenu for a window list
- **THEN** the default target SHALL be resolved through the existing shared `WindowSelectionRequest` contract with submenu intent
- **AND** the `window-switch-selection-core` behavior SHALL be unchanged

#### Scenario: Strategy id is registered
- **WHEN** the coordinator resolves a window-switching descriptor
- **THEN** the concrete window strategy SHALL be resolvable under its registered id
