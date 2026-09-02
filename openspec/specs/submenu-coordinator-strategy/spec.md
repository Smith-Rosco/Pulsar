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
