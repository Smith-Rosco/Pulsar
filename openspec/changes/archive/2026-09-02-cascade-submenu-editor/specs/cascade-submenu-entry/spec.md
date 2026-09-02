## Purpose

Defines the left-click drill-in path that opens a cascade submenu from a root slot carrying sub-actions, keeping window-group drill-in and modifier-release semantics intact.

## ADDED Requirements

### Requirement: Left-click on a cascade slot SHALL open the cascade submenu
When the user left-clicks a root slot whose `SubSlots` is non-empty, Pulsar SHALL open the cascade submenu using the slot's descriptor.

#### Scenario: Root slot has sub-actions
- **WHEN** the user left-clicks a root slot with non-empty `SubSlots`
- **THEN** Pulsar SHALL construct a `CascadeSubMenuDescriptor` from the slot's `SubSlots` and layout style
- **AND** SHALL route it through the coordinator to the `cascade` strategy

#### Scenario: Root slot has no sub-actions
- **WHEN** the user left-clicks a root slot whose `SubSlots` is empty
- **THEN** Pulsar SHALL execute the slot's own action as before
- **AND** SHALL NOT attempt to open a cascade

### Requirement: Window-group drill-in SHALL be unchanged
The pre-existing window-group submenu entry path SHALL NOT be affected by the cascade entry.

#### Scenario: Grouped process slot still drills into window submenu
- **WHEN** the user left-clicks a grouped process slot (multiple windows)
- **THEN** Pulsar SHALL open the window-group submenu exactly as before

### Requirement: Modifier-release semantics SHALL be preserved
Modifier-release on a cascade slot SHALL execute the slot's own action; it SHALL NOT auto-open the cascade.

#### Scenario: Modifier release on cascade slot
- **WHEN** the user highlights a cascade slot and releases the execution modifier
- **THEN** Pulsar SHALL execute the slot's own action
- **AND** SHALL NOT open the cascade submenu
