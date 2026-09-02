## ADDED Requirements

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
