# right-drag-dispatch-timing Specification

## Purpose
TBD - created by archiving change fix-right-drag-release-passthrough. Update Purpose after archive.

## Requirements

### Requirement: Gesture dispatch SHALL run at input priority

Summon and release handling initiated from the mouse hook SHALL be dispatched to the UI thread at `DispatcherPriority.Input` so the menu appears with hotkey-path latency.

#### Scenario: Summon dispatch priority
- **WHEN** the right-drag gesture triggers a menu summon
- **THEN** the summon work SHALL be queued at `DispatcherPriority.Input` on the UI dispatcher
- **AND** the menu SHALL appear without waiting behind lower-priority queued work

#### Scenario: Release dispatch priority
- **WHEN** a gesture release executes the selection
- **THEN** the release handling SHALL be queued at `DispatcherPriority.Input`

### Requirement: Menu SHALL hide synchronously on gesture release

Releasing the right button SHALL hide the menu immediately (before the selection action completes), rather than awaiting the action before hiding.

#### Scenario: Immediate hide on release
- **WHEN** the user releases the right button over a slot
- **THEN** the menu SHALL be hidden synchronously at the start of release handling
- **AND** the slot selection SHALL execute asynchronously after hiding begins

#### Scenario: Loading-release quick switch hides first
- **WHEN** the gesture is released while the menu page is still loading
- **THEN** the menu SHALL hide immediately
- **AND** the quick-switch to the previous window SHALL proceed without waiting for the page load
