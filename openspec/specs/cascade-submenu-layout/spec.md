## Purpose

Defines the geometry and hit-testing rules for StarPie-style cascade submenu layouts — Ring (concentric sub-ring) and Fan (sector fan) — computed in window-relative DIP coordinates so child slots stay predictable and clickable across DPI scales.

## ADDED Requirements

### Requirement: Cascade submenu SHALL support Ring layout
The system SHALL lay child slots of a cascade submenu on a concentric sub-ring centered on the parent slot, distributing them radially when there are two or more children.

#### Scenario: Single child renders at parent direction
- **WHEN** a cascade submenu has exactly one child in Ring layout
- **THEN** the child slot SHALL be positioned on the sub-ring along the parent slot's direction from the wheel center

#### Scenario: Multiple children distribute around the sub-ring
- **WHEN** a Ring layout has N (>= 2) children
- **THEN** the children SHALL be distributed at even angular intervals starting from the parent slot's direction

#### Scenario: Ring geometry stays inside the menu canvas
- **WHEN** the Ring sub-layout computes a child position
- **THEN** the position SHALL remain within the 500x500 menu canvas bounds given the configured sub-ring radius

### Requirement: Cascade submenu SHALL support Fan layout
The system SHALL lay child slots of a cascade submenu as a Fan: up to three wings (upper, center-tip, lower) arranged along the parent slot's radial direction.

#### Scenario: One child uses the center tip
- **WHEN** a Fan layout has exactly one child
- **THEN** the child SHALL be placed at the center-tip position along the parent direction

#### Scenario: Two children use symmetric wings
- **WHEN** a Fan layout has exactly two children
- **THEN** the two children SHALL be placed on the upper and lower wings symmetrically about the parent direction

#### Scenario: Three children use all wings
- **WHEN** a Fan layout has three children
- **THEN** the children SHALL be placed on the upper wing, center tip, and lower wing respectively

#### Scenario: More than three children fall back to Ring
- **WHEN** a Fan layout has more than three children
- **THEN** the system SHALL lay them out in Ring form instead (Fan caps at three wings)

### Requirement: Sub-layout hit-testing SHALL return the child index
The sub-layout engine SHALL determine which child slot (if any) corresponds to a given window-relative DIP point.

#### Scenario: Ring hit test outside dead zone
- **WHEN** a point lies beyond the cascade dead zone and inside the sub-ring band
- **THEN** the engine SHALL return the ring child whose sector contains the point's angle
- **AND** SHALL return 0 for the center region and -1 for outside the band

#### Scenario: Fan hit test picks nearest wing
- **WHEN** a point lies in the Fan band
- **THEN** the engine SHALL select the wing (upper/tip/lower) whose direction has the smallest angular difference from the point
- **AND** SHALL return -1 when the point is inside the dead zone or beyond the fan extent

#### Scenario: Hit tests use DIP coordinates
- **WHEN** a point is expressed in window-relative DIP units (from `MouseTrackingService`)
- **THEN** the engine SHALL compute distances and angles in those units without applying a second DPI transform

### Requirement: Sub-layout engine SHALL be independent of the root layout
The sub-layout SHALL be computed from a parent slot pose (center, radius, slot size, direction) rather than from the root ring's slot count.

#### Scenario: Parent pose drives geometry
- **WHEN** the engine receives a parent slot pose and a layout style
- **THEN** it SHALL produce child positions deterministically from that pose alone
- **AND** the root `ISlotLayoutEngine` slot-count logic SHALL NOT be involved

#### Scenario: Deterministic and repeatable
- **WHEN** the same parent pose, style, and child count are requested twice
- **THEN** the engine SHALL return identical positions both times
