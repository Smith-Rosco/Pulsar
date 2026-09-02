## Purpose

Defines the data model and invocation contract that let a root radial-menu slot carry its own cascading submenu, independent of the window-switching use case.

## Requirements

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

### Requirement: CascadeSubMenuDescriptor SHALL carry a layout style
The cascade submenu payload SHALL declare which sub-layout form (Ring or Fan) renders its children, and SHALL be a first-class routable descriptor like the window payload.

#### Scenario: Descriptor declares Fan layout
- **WHEN** a `CascadeSubMenuDescriptor` is created with `LayoutStyle = Fan`
- **THEN** the session SHALL route it to the cascade strategy
- **AND** the strategy SHALL lay the children out using Fan geometry

#### Scenario: Descriptor declares Ring layout
- **WHEN** a `CascadeSubMenuDescriptor` is created with `LayoutStyle = Ring`
- **THEN** the session SHALL route it to the cascade strategy
- **AND** the strategy SHALL lay the children out using Ring geometry

#### Scenario: Style defaults to Fan
- **WHEN** a `CascadeSubMenuDescriptor` is created without specifying a layout style
- **THEN** the style SHALL default to Fan

### Requirement: Cascade pagination SHALL derive from sub-slot count
When a cascade has more children than the configured slots per page, the system SHALL page the remaining children using the existing submenu pagination behavior.

#### Scenario: Children exceed slots per page
- **WHEN** a cascade has more `SubSlots` than the current `SlotsPerPage`
- **THEN** the cascade SHALL present only the first page's children
- **AND** the total page count SHALL be derived from `SubSlots.Count`
- **AND** paging SHALL reuse the existing submenu page navigation

#### Scenario: Children fit in one page
- **WHEN** a cascade has fewer or equal `SubSlots` than `SlotsPerPage`
- **THEN** all children SHALL be visible on a single page
- **AND** the total page count SHALL be one

### Requirement: Runtime SubSlots SHALL populate from persisted SubActions
A root slot's runtime `SubSlots` collection SHALL be populated from the slot's persisted `SubActions` when the menu is built, so the cascade entry and editor operate on the same source of truth.

#### Scenario: Menu build maps SubActions to SubSlots
- **WHEN** the radial menu builds root slots from config
- **THEN** each slot's `SubSlots` SHALL contain one `SubSlotDescriptor` per persisted `SubActions` entry, in persisted order
- **AND** a slot without `SubActions` SHALL expose an empty `SubSlots` collection

### Requirement: Editor mutations SHALL flow to the same collection
Sub-action edits made in the editor SHALL be reflected in the slot's persisted `SubActions`, which the next menu build maps back into `SubSlots`.

#### Scenario: Editor adds a sub-action then menu rebuilds
- **WHEN** the user adds a sub-action in the editor and saves
- **THEN** the next menu build SHALL include the new descriptor in the slot's `SubSlots`
- **AND** the cascade SHALL render it as a child

#### Scenario: Editor removes a sub-action then menu rebuilds
- **WHEN** the user removes a sub-action in the editor and saves
- **THEN** the next menu build SHALL omit it from the slot's `SubSlots`
- **AND** the cascade SHALL no longer render it
