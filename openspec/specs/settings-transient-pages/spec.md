# settings-transient-pages Specification

## Purpose

定义设置窗口临时页（动态标签页）的生命周期契约：按需注册到侧边栏、按类型单例激活、干净页导航离开自动回收、脏页保留与关闭确认、视觉区分，保证侧边栏 Tab 数量有界且不随使用持续扩张。

## Requirements

### Requirement: Transient Pages SHALL Open as Per-Type Singletons
The system SHALL allow triggering a transient settings page (e.g., via a card's detail button) to append a single navigation entry for that transient page in the sidebar. Transient pages that are entity-scoped SHALL register under a composite id (`<templateId>:<entityId>`), and triggering the same entity SHALL NOT create an additional entry or instance — it SHALL activate the existing instance instead. For entity-scoped transient pages the number of open transient tabs SHALL be bounded by the number of distinct entities opened; for non-entity transient pages it remains bounded by the number of registered transient page types. Tab titles for entity-scoped pages SHALL identify the entity.

#### Scenario: First trigger opens a transient tab
- **WHEN** a transient page type that is not currently open is triggered
- **THEN** one navigation entry is appended at the end of the transient page's semantic group in the sidebar
- **THEN** the shell navigates to that transient page

#### Scenario: Repeated trigger reuses the open instance
- **WHEN** a transient page type that is already open is triggered again, including from another page after the user navigated elsewhere
- **THEN** no new navigation entry or page instance is created
- **THEN** the existing instance is activated and displayed

#### Scenario: Tab count is bounded by registered types
- **WHEN** non-entity transient pages are triggered repeatedly across a settings session
- **THEN** the sidebar SHALL NOT accumulate more transient entries than the number of registered transient page types

#### Scenario: Entity-scoped pages are singletons per entity
- **WHEN** an entity-scoped transient page (composite id `templateId:entityId`) is triggered for an entity whose tab is open
- **THEN** the existing tab is activated; no duplicate entry is created
- **WHEN** it is triggered for a different entity of the same template
- **THEN** a separate tab for that entity is appended

### Requirement: Clean Transient Pages SHALL Be Recycled on Navigation Leave
The system SHALL recycle an open transient page (removing its navigation entry and destroying its page instance) when the user navigates away from it while there are no unsaved changes. If unsaved changes exist, the transient page SHALL remain open and unrecycled. A recycled transient page type SHALL be re-openable later with a freshly constructed instance reflecting the current configuration.

#### Scenario: Leaving a clean transient page recycles it
- **WHEN** the user navigates from an open transient page to another page and there are no unsaved changes
- **THEN** the transient page's navigation entry is removed from the sidebar
- **THEN** the transient page instance is destroyed and not restored by back-navigation

#### Scenario: Leaving a dirty transient page keeps it open
- **WHEN** the user navigates away from an open transient page while unsaved changes exist
- **THEN** the transient page's navigation entry remains in the sidebar
- **THEN** returning to it later in the same settings session shows the page with its state intact

#### Scenario: Re-opening a recycled transient page reflects current configuration
- **WHEN** a transient page type that was previously recycled is triggered again
- **THEN** a new page instance is constructed from the current configuration state

### Requirement: Transient Pages SHALL Offer an Explicit Close Action
Each open transient page's navigation entry SHALL provide a close affordance. Closing a transient page with no unsaved changes SHALL recycle it immediately. Closing a transient page with unsaved changes SHALL require explicit user confirmation before recycling.

#### Scenario: Closing a clean transient page
- **WHEN** the user invokes the close affordance on a transient page entry with no unsaved changes
- **THEN** the transient page is recycled immediately and the shell displays a valid current page

#### Scenario: Closing a dirty transient page asks for confirmation
- **WHEN** the user invokes the close affordance on a transient page entry while unsaved changes exist
- **THEN** the system prompts the user to save, discard, or cancel
- **THEN** cancelling keeps the transient page open unchanged
- **THEN** saving or discarding resolves the unsaved state and then recycles the transient page

### Requirement: Transient Entries SHALL Be Visually Distinguished
Open transient page entries SHALL be visually distinguishable from permanent navigation entries (e.g., italic title styling) and SHALL expose their close affordance, so users can identify which tabs are temporary.

#### Scenario: Transient entry is visually distinct
- **WHEN** a transient page is open in the sidebar
- **THEN** its entry is rendered with the transient visual treatment (distinct from permanent entries) and shows a close affordance

### Requirement: Transient Tabs SHALL Be Session-Scoped
Open transient pages SHALL exist only for the lifetime of the settings window session. They SHALL NOT be restored when the settings window is closed and reopened.

#### Scenario: Transient tabs are not restored after reopening settings
- **WHEN** the settings window is closed with transient pages open and then reopened
- **THEN** the sidebar contains only permanent navigation entries
- **THEN** any transient page types can be triggered again on demand

### Requirement: Entity-scoped transient tabs SHALL close when their entity disappears
When the entity backing an open entity-scoped transient tab is deleted (directly or via deletion of its containing profile/context), the transient page service SHALL unregister that tab's navigation entry and destroy its instance without additional prompting; deletion confirms are owned by the flow that deletes the entity.

#### Scenario: Entity deleted while tab open
- **WHEN** the entity backing an open transient tab is deleted
- **THEN** the tab's navigation entry is removed and its instance destroyed
