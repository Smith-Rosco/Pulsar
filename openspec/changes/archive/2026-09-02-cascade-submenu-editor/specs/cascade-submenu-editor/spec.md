## Purpose

Defines how a root slot's sub-actions (cascade children) are authored inside the unified slot editor, including layout-style selection and persistence to `PluginSlot.SubActions`.

## ADDED Requirements

### Requirement: Editor SHALL expose a sub-action list
The slot configuration dialog SHALL present an editable list of sub-actions for the slot being edited, populated from the slot's `SubSlots` (which maps persisted `SubActions`).

#### Scenario: Slot has configured sub-actions
- **WHEN** the user opens the slot editor for a slot whose `SubActions` is non-empty
- **THEN** the dialog SHALL show each sub-action as an editable row (plugin/action label, arguments summary, icon, color, label)
- **AND** SHALL allow the user to add, remove, and reorder rows

#### Scenario: Slot has no sub-actions
- **WHEN** the slot editor opens for a slot without sub-actions
- **THEN** the sub-action section SHALL show an empty state with an "add" affordance
- **AND** the section SHALL be collapsed/compact so it does not dominate the Behavior area

### Requirement: Editor SHALL support add/remove/reorder of sub-actions
The sub-action editor SHALL provide commands to append a new sub-action, remove an existing one, and move a sub-action up or down in order.

#### Scenario: User adds a sub-action
- **WHEN** the user clicks add on an empty or existing list
- **THEN** a new `SubSlotDescriptor` SHALL be appended with empty fields
- **AND** the new row SHALL be focused/selected for immediate editing

#### Scenario: User removes a sub-action
- **WHEN** the user removes a sub-action row
- **THEN** the descriptor SHALL be removed from the slot's `SubActions`
- **AND** remaining rows SHALL retain their relative order

#### Scenario: User reorders sub-actions
- **WHEN** the user moves a sub-action up or down
- **THEN** the descriptor order SHALL change accordingly
- **AND** the order SHALL be persisted (it determines cascade layout order)

### Requirement: Sub-action rows SHALL reuse the parameter field surface
Each sub-action's arguments SHALL be edited through the same field/picker machinery used for the root slot's parameters, not a bespoke text-only form.

#### Scenario: Sub-action has a picker-backed parameter
- **WHEN** a sub-action exposes a parameter with a picker intent (process/file/secret)
- **THEN** the sub-action row SHALL expose the same browse/pick affordance as the root slot parameter fields

#### Scenario: Sub-action has a text parameter
- **WHEN** a sub-action exposes a plain text parameter
- **THEN** the sub-action row SHALL render a text box bound to the descriptor's `Args` entry

### Requirement: Editor SHALL let the user pick the cascade layout style
The Behavior section SHALL expose a Fan/Ring selector for the slot's cascade layout.

#### Scenario: User selects Fan layout
- **WHEN** the user chooses Fan
- **THEN** the slot's cascade SHALL render children in Fan form when drilled in

#### Scenario: User selects Ring layout
- **WHEN** the user chooses Ring
- **THEN** the slot's cascade SHALL render children in Ring form when drilled in

### Requirement: Sub-action edits SHALL persist
Saving the slot dialog SHALL persist the edited sub-action list under the slot in `Profiles.json`.

#### Scenario: Save persists sub-actions
- **WHEN** the user saves the slot dialog after editing sub-actions
- **THEN** `Profiles.json` SHALL contain the updated `subActions` list under the slot
- **AND** reloading the profile SHALL restore the exact list

#### Scenario: Cancel discards sub-action edits
- **WHEN** the user cancels the slot dialog after editing sub-actions
- **THEN** the persisted `subActions` SHALL remain unchanged
