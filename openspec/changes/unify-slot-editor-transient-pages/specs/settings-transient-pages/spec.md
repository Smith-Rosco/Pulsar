# settings-transient-pages Delta

## MODIFIED Requirements

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

## ADDED Requirements

### Requirement: Entity-scoped transient tabs SHALL close when their entity disappears

When the entity backing an open entity-scoped transient tab is deleted (directly or via deletion of its containing profile/context), the transient page service SHALL unregister that tab's navigation entry and destroy its instance without additional prompting; deletion confirms are owned by the flow that deletes the entity.

#### Scenario: Entity deleted while tab open

- **WHEN** the entity backing an open transient tab is deleted
- **THEN** the tab's navigation entry is removed and its instance destroyed
