## ADDED Requirements

### Requirement: Cascade SHALL be a registered concrete strategy
The coordinator SHALL route `cascade` descriptors to a registered `CascadeSubMenuStrategy` that configures child slots from the descriptor's `SubSlots`.

#### Scenario: Cascade descriptor routes to cascade strategy
- **WHEN** the coordinator receives a `CascadeSubMenuDescriptor`
- **THEN** it SHALL select the strategy registered under id `cascade`
- **AND** SHALL configure center back-navigation and child slots from the descriptor's `SubSlots`

#### Scenario: Child slot maps to its sub-action
- **WHEN** the cascade strategy configures a child slot from a `SubSlotDescriptor`
- **THEN** the child slot SHALL carry that sub-action's plugin id, action, arguments, label, and icon
- **AND** the child slot SHALL execute that action when selected

#### Scenario: Empty sub-slot page renders no-op fillers
- **WHEN** a cascade page has fewer children than the slots per page
- **THEN** the remaining slots SHALL be `NoOpStrategy` fillers, matching window-submenu behavior

### Requirement: Window strategy behavior SHALL be unchanged
The window-switch strategy and its routing SHALL NOT be affected by the cascade strategy addition.

#### Scenario: Window descriptor still routes to window strategy
- **WHEN** the coordinator receives a `WindowSubMenuDescriptor`
- **THEN** it SHALL select the `window-switch` strategy and configure window slots exactly as before

#### Scenario: Unknown id still falls back
- **WHEN** the coordinator receives a descriptor with an unregistered id
- **THEN** it SHALL log a warning and fall back to the root menu, as specified in Change A
