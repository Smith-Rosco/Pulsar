## Purpose

Defines the strategy-based coordinator that hosts concrete submenu strategies, so window switching is just one implementation and future cascade forms plug in without touching the session.

## ADDED Requirements

### Requirement: Submenu rendering SHALL be strategy-driven
The submenu coordinator SHALL select a concrete strategy from a `SubMenuDescriptor` and delegate slot configuration (center slot, child slots, pagination) to that strategy.

#### Scenario: Known strategy is routed
- **WHEN** a `SubMenuDescriptor` names a registered strategy id
- **THEN** the coordinator SHALL instantiate that strategy
- **AND** SHALL configure the submenu through it

#### Scenario: Unknown strategy id
- **WHEN** a `SubMenuDescriptor` names an unregistered strategy id
- **THEN** the coordinator SHALL NOT crash
- **AND** SHALL fall back to the root menu with a logged warning

### Requirement: Window switching SHALL be one concrete strategy
The pre-existing window-group submenu behavior SHALL be preserved as a concrete `WindowSwitchSubMenuStrategy` that reads its payload from the descriptor.

#### Scenario: Window strategy configures window slots
- **WHEN** the window strategy is invoked with a process name and window list
- **THEN** the center slot SHALL show the process name with back-navigation
- **AND** child slots SHALL display window titles, icons, thumbnails, and per-window color tokens as before
- **AND** pagination SHALL split windows exactly as the pre-existing behavior did

#### Scenario: Restore root preserves behavior
- **WHEN** the user navigates back from a strategy-driven submenu
- **THEN** the coordinator SHALL restore the root menu
- **AND** SHALL clear all per-slot submenu state (thumbnails, colors, animation offsets)

### Requirement: Strategies SHALL be registered and resolvable
Concrete submenu strategies SHALL be registered in dependency injection and resolvable by their descriptor id through the coordinator.

#### Scenario: Strategy resolution via DI
- **WHEN** the coordinator needs a strategy for a descriptor
- **THEN** it SHALL resolve the strategy instance from the registered set by id
- **AND** the window strategy SHALL be registered under its documented id

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
