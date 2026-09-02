## Purpose

Defines the data model and invocation contract that let a root radial-menu slot carry its own cascading submenu, independent of the window-switching use case.

## ADDED Requirements

### Requirement: Slot SHALL expose a sub-slot collection
The `SlotViewModel` SHALL expose an observable `SubSlots` collection of `SubSlotDescriptor` so any slot can declare a cascade of child actions without runtime specialization.

#### Scenario: Root slot carries declared sub-slots
- **WHEN** a root radial-menu slot is configured with sub-slots
- **THEN** the slot's `SubSlots` collection SHALL contain one `SubSlotDescriptor` per configured child
- **AND** the descriptors SHALL be observable (added/removed at runtime are reflected)

#### Scenario: Slot without sub-slots
- **WHEN** a slot has no cascade configured
- **THEN** its `SubSlots` collection SHALL be present and empty

### Requirement: PluginSlot SHALL persist optional sub-actions
The `PluginSlot` configuration model SHALL support an optional list of sub-actions that is persisted with the slot and deserialized tolerantly when absent.

#### Scenario: Sub-actions are persisted
- **WHEN** a user saves a slot that declares sub-actions
- **THEN** `Profiles.json` SHALL contain those sub-actions under the slot
- **AND** reloading the profile SHALL restore them

#### Scenario: Legacy slot without sub-actions
- **WHEN** a profile is loaded that predates sub-actions
- **THEN** the slot SHALL load with an empty sub-action list
- **AND** existing slot behavior SHALL be unchanged

### Requirement: Submenu entry SHALL be driven by a descriptor
Submenu entry SHALL be requested through a `SubMenuDescriptor` that identifies the strategy and carries the strategy-specific payload, rather than through a window-list-specific signature.

#### Scenario: Session opens a descriptor-driven submenu
- **WHEN** a slot requests a submenu via `IMenuSession.EnterSubMenuAsync(SubMenuDescriptor)`
- **THEN** the session SHALL route the descriptor to the matching submenu strategy
- **AND** the session SHALL configure the center slot and child slots according to that strategy

#### Scenario: Descriptor carries window payload
- **WHEN** the descriptor is a window-switching descriptor
- **THEN** it SHALL carry the process name and eligible `ProcessWindowInfo` list
- **AND** the resulting submenu SHALL behave identically to the pre-existing window-group submenu
