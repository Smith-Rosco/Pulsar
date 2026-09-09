# slot-editor-transient-page Specification

## Purpose

把 slot 编辑器（主 slot 编辑与新建，含子动作配置）作为设置窗口的临时页（transient tab）承载：实体 id 命名与按实体单例激活、tab 内两步新建向导、live 编辑接入统一保存流、实体删除联动关 tab，使全部 slot 配置界面共享一套编辑逻辑与交互。

## ADDED Requirements

### Requirement: Slot editor SHALL open as an entity-scoped transient tab

The settings shell SHALL offer a transient page template `slot-editor`. Opening the editor for a specific slot SHALL register a navigation entry whose id is the composite `slot-editor:<contextKey>:<slotNo>` (e.g. `slot-editor:Global:1`); opening the create flow SHALL register `slot-editor:<contextKey>:draft`. Triggering the editor for an entity that already has an open tab SHALL activate the existing tab instead of creating a duplicate. The tab title SHALL identify the entity (slot label and context).

#### Scenario: Editing a slot opens its dedicated tab

- **WHEN** the user triggers "edit" on a slot (context Global, slot number 1)
- **THEN** a transient tab with id `slot-editor:Global:1` is appended (if absent) and activated
- **THEN** the tab shows the full editor for that slot

#### Scenario: Re-triggering the same entity activates the existing tab

- **WHEN** the user triggers edit on slot `slot-editor:Global:1` while its tab is already open
- **THEN** no additional entry or instance is created
- **THEN** the existing tab is activated and displayed

#### Scenario: Different entities get different tabs

- **WHEN** the user has `slot-editor:Global:1` open and then triggers edit on Global slot 2
- **THEN** a second tab `slot-editor:Global:2` is appended; the first remains open

#### Scenario: One draft tab per context

- **WHEN** the user triggers "add slot" in context Global while `slot-editor:Global:draft` is open
- **THEN** the existing draft tab is activated instead of creating a second draft

### Requirement: Create flow SHALL be a two-step wizard inside the tab

The create tab SHALL present step 1 as a slot-type picker and step 2 as the configuration form for the chosen type. The draft SHALL only enter the context's slot list when the user explicitly commits it (the "add to slots" action). After a successful commit the tab SHALL convert to edit mode for the newly created slot (id re-registered from `draft` to the assigned slot number) instead of closing.

#### Scenario: Type selection is step one

- **WHEN** the user opens the create tab
- **THEN** step 1 shows the slot type picker before any configuration fields

#### Scenario: Going back to re-choose a type

- **WHEN** the user returns to step 1 from step 2
- **THEN** the configuration is reset for the newly chosen type and no slot is committed

#### Scenario: Commit adds the slot and converts the tab

- **WHEN** the user completes configuration and commits
- **THEN** the draft slot is appended to the context's slot list (subject to the same validation as the previous dialog flow)
- **THEN** the tab id is re-registered to `slot-editor:<contextKey>:<slotNo>` and continues as an edit tab

### Requirement: Edit mode SHALL edit the live draft with the unified save flow

The editor tab SHALL bind to the slot's live draft object; edits SHALL arm the settings dirty state through the existing dirty-tracking chain. The tab SHALL NOT present its own save button; persistence goes through the settings window's unified save action and its guard flow (dirty tabs stay open on navigation leave; unsaved changes surface once at window close).

#### Scenario: Editing arms the global dirty state

- **WHEN** the user changes any editable field on the editor tab (including sub-actions)
- **THEN** the settings window's unsaved-changes indicator becomes active and the save action becomes available

#### Scenario: Dirty editor tab survives navigation

- **WHEN** the user navigates away from a dirty editor tab
- **THEN** the tab stays open and unrecycled, consistent with transient-page dirty semantics

#### Scenario: Clean editor tab recycles on navigation leave

- **WHEN** the user navigates away from an editor tab with no unsaved changes
- **THEN** the tab is removed and its entity can be re-opened fresh later

### Requirement: Deleting an entity SHALL close its editor tab

When the slot being edited is removed (individually or by deleting its profile/context), the corresponding editor tab SHALL be unregistered and closed without prompting, provided it contributed no unsaved changes of its own; if the settings session has unsaved changes, the existing window-level guard flow governs.

#### Scenario: Slot removed while its editor tab is open

- **WHEN** the user deletes Global slot 1 while `slot-editor:Global:1` is open
- **THEN** the tab is removed from the sidebar and its instance destroyed
