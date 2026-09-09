# cascade-submenu-editor Delta

## MODIFIED Requirements

### Requirement: Editor SHALL expose a sub-action list

The slot editor (transient page) SHALL present an editable list of sub-actions for the slot being edited, populated from the slot's `SubActions`. Each sub-action SHALL render as a compact summary row (plugin/action label, arguments summary, icon, color, label) that expands in place — one expanded row at a time — into a full editing form; the summary list SHALL allow the user to add, remove, and reorder rows.

#### Scenario: Slot has configured sub-actions

- **WHEN** the user opens the slot editor for a slot whose `SubActions` is non-empty
- **THEN** the editor shows each sub-action as an expandable row that can be expanded into a full editing form

#### Scenario: Only one row expanded at a time

- **WHEN** the user expands a second sub-action row while one is already expanded
- **THEN** the previously expanded row collapses to its summary form

#### Scenario: Slot has no sub-actions

- **WHEN** the slot editor opens for a slot without sub-actions
- **THEN** the sub-action section SHALL show an empty state with an "add" affordance
- **AND** the section SHALL be collapsed/compact so it does not dominate the Behavior area

### Requirement: Sub-action rows SHALL reuse the parameter field surface

Each sub-action's arguments SHALL be edited through the same field/picker machinery used for the root slot's parameters, not a bespoke text-only form. The plugin and action selectors SHALL be owned by the view model (selected item held as a view-model property and synchronized explicitly) so that rebuilding the option lists — on plugin change or action change — SHALL NOT reset or swallow the user's selection; a selection made once SHALL take effect immediately.

#### Scenario: Sub-action has a picker-backed parameter

- **WHEN** a sub-action exposes a parameter with a picker intent (process/file/secret)
- **THEN** the sub-action row SHALL expose the same browse/pick affordance as the root slot parameter fields

#### Scenario: Sub-action has a text parameter

- **WHEN** a sub-action exposes a plain text parameter
- **THEN** the sub-action row SHALL render a text box bound to the descriptor's `Args` entry

#### Scenario: Selecting a plugin reveals its actions without re-selection

- **WHEN** the user selects a plugin in a sub-action row
- **THEN** the action selector SHALL list that plugin's actions immediately, with no intermediate reset requiring a second selection

#### Scenario: Selecting an action reveals its parameters without re-selection

- **WHEN** the user selects an action in a sub-action row
- **THEN** the parameter fields for that action SHALL appear immediately; the action selector SHALL keep showing the selected action
