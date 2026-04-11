## ADDED Requirements

### Requirement: Optimal layout calculation
The system SHALL calculate optimal layout parameters (radius, center size, slot size) based on the number of slots to maintain consistent visual density.

#### Scenario: Calculate optimal radius for 8 slots
- **WHEN** `CalculateOptimalLayout(8)` is called
- **THEN** the returned radius SHALL be 90px, center size 70px, slot size 50px

#### Scenario: Calculate optimal layout for 12 slots
- **WHEN** `CalculateOptimalLayout(12)` is called
- **THEN** the returned radius SHALL be 125px (larger to prevent overlap)
- **AND** slot size SHALL be 42px (smaller to fit more slots)
- **AND** center size SHALL be 60px (smaller to allocate space)

#### Scenario: Calculate optimal layout for 4 slots
- **WHEN** `CalculateOptimalLayout(4)` is called
- **THEN** the returned radius SHALL be 90px (base radius sufficient)
- **AND** slot size SHALL be 58px (larger for fewer slots)
- **AND** center size SHALL be 80px (larger for visual weight)

### Requirement: Slot position calculation
The system SHALL calculate the X/Y position for each slot based on index and layout parameters.

#### Scenario: Calculate position for slot 1 (top)
- **WHEN** `GetSlotPosition(1, 8, 90, 250, 250, 50)` is called
- **THEN** the returned position SHALL be at the top of the circle (250, 160)
- **AND** the value SHALL be the top-left corner of the slot bounding box

#### Scenario: Calculate positions for all slots
- **WHEN** `GetSlotPosition` is called for indices 1-8 with 8 total slots
- **THEN** the slots SHALL be evenly distributed at 45-degree intervals starting from -90 degrees (top)

### Requirement: Visual density validation
The system SHALL calculate a visual density metric to validate layout quality.

#### Scenario: Calculate visual density
- **WHEN** `CalculateVisualDensity(8, 50, 90)` is called
- **THEN** the returned density SHALL be between 0.85 and 1.15 (optimal range)

### Requirement: Hit testing
The system SHALL determine which slot (if any) corresponds to a given point.

#### Scenario: Hit test center dead zone
- **WHEN** `HitTest(point, layout)` is called with point at the exact center
- **THEN** the returned index SHALL be 0 (center slot)

#### Scenario: Hit test slot boundary
- **WHEN** point is exactly on the boundary between two slots
- **THEN** the slot with the closer angle SHALL be returned

### Requirement: Dead zone ratio calculation
The system SHALL calculate the dead zone radius as a ratio of center size based on slot count.

#### Scenario: Calculate dead zone for 8 slots
- **WHEN** `CalculateDeadZoneRatio(8)` is called
- **THEN** the returned ratio SHALL be 0.60 (60% of center size)

#### Scenario: Calculate dead zone for 12 slots
- **WHEN** `CalculateDeadZoneRatio(12)` is called
- **THEN** the returned ratio SHALL be 0.62 (slightly larger for denser layouts)
