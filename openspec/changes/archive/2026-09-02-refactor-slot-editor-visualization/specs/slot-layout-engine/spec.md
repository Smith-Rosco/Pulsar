# slot-layout-engine

## ADDED Requirements

### Requirement: ISlotLayoutEngine SHALL expose slot/center size calculations
The `ISlotLayoutEngine` interface SHALL declare `CalculateOptimalSlotSize(int)` and `CalculateOptimalCenterSize(int)` so callers do not downcast the interface to the concrete `SlotLayoutEngine` to reach these methods.

#### Scenario: Interface exposes size methods
- **WHEN** any consumer needs optimal slot or center size for a slot count
- **THEN** it SHALL be able to call `ISlotLayoutEngine.CalculateOptimalSlotSize(count)` and `ISlotLayoutEngine.CalculateOptimalCenterSize(count)` without casting

#### Scenario: Editor stops downcasting
- **WHEN** `SlotWheelEditorViewModel` computes its scaled slot size
- **THEN** it SHALL use the interface methods instead of `_layoutEngine is SlotLayoutEngine engine`

#### Scenario: Behavioral parity
- **WHEN** the new interface methods are called with the same arguments as the current concrete methods
- **THEN** the returned values SHALL be identical (the concrete implementation is unchanged)
