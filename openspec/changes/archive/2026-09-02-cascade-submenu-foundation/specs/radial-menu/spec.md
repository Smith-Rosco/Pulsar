## ADDED Requirements

### Requirement: Submenu entry SHALL support descriptor-driven cascades
The radial menu SHALL allow a root slot to open a cascade described by that slot's own `SubSlots` collection, in addition to the existing window-group drill-in path.

#### Scenario: Slot opens its declared cascade
- **WHEN** the user left-clicks a root slot that declares sub-slots
- **THEN** Pulsar SHALL open a submenu configured from the slot's `SubSlots` descriptors
- **AND** the session SHALL route the descriptors to the matching submenu strategy

#### Scenario: Window-group drill-in is unchanged
- **WHEN** the user left-clicks a grouped process slot (multiple eligible windows)
- **THEN** Pulsar SHALL open the window-group submenu exactly as before this change
- **AND** the slot SHALL NOT require a persisted `SubSlots` collection to do so

### Requirement: Grouped root-slot modifier-release SHALL remain direct-switch
The existing grouped-slot modifier-release semantics SHALL be preserved regardless of the descriptor-driven refactor.

#### Scenario: Modifier release on grouped slot
- **WHEN** the user highlights a grouped process slot and releases the execution modifier
- **THEN** Pulsar SHALL resolve and activate a default target window directly
- **AND** SHALL NOT open a submenu

#### Scenario: Modifier release on cascade slot
- **WHEN** the user highlights a root slot that declares sub-slots and releases the execution modifier
- **THEN** Pulsar SHALL execute the slot's own action as before (no submenu auto-open)
