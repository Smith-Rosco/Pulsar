# slot-wheel-editor-architecture Specification

## Purpose
TBD - created by archiving change refactor-slot-editor-visualization. Update Purpose after archive.

## Requirements

### Requirement: Slot tooltips SHALL be localized
`WheelSlotItem.Tooltip` SHALL be produced through `ILocalizationService` instead of a hardcoded `$"#{Slot.Slot} {Label}"` literal.

#### Scenario: Filled-slot tooltip
- **WHEN** a filled slot's tooltip is requested
- **THEN** it SHALL be composed from localized resources (e.g., position + label), not a C# interpolation literal

#### Scenario: Empty-slot tooltip
- **WHEN** an empty slot's tooltip is requested
- **THEN** it SHALL return the localized add-slot hint described in the visualization spec

### Requirement: Right-click context menu SHALL be built by a reusable, testable component
The imperative ContextMenu construction currently inside `SlotWheelEditor.xaml.cs` SHALL be extracted into a dedicated builder class that accepts the localization service and the wheel ViewModel, so the menu logic is unit-testable and the code-behind stays thin.

#### Scenario: Code-behind delegates menu building
- **WHEN** `WheelItems_ContextMenuOpening` fires
- **THEN** the code-behind SHALL delegate to the extracted builder and only apply theme/placement

#### Scenario: Menu contains Move to / Edit / Delete
- **WHEN** the builder creates a context menu for a slot
- **THEN** it SHALL include the "Move to" nested page/slot items, Edit, and Delete with localized headers and correct click behavior

#### Scenario: Builder is testable
- **WHEN** a unit test constructs the builder with a fake localization service
- **THEN** it SHALL be able to assert the resulting menu items' headers and structure without a running window

### Requirement: SlotWheelEditorViewModel SHALL be constructed via DI
The settings page SHALL obtain `SlotWheelEditorViewModel` through the DI container (registering it and resolving via `IServiceProvider`) rather than `new` in the code-behind.

#### Scenario: DI resolution
- **WHEN** `SettingsSlotsPage` needs the wheel ViewModel
- **THEN** it SHALL resolve it from `App.Current.Services`
- **AND** the ViewModel's `ISlotLayoutEngine` dependency SHALL come from the container

### Requirement: Existing behavior SHALL be preserved
The refactor SHALL not change the wheel layout numbers, paging, reorder semantics, or the concrete `SlotLayoutEngine` behavior. Existing tests in `SlotWheelEditorViewModelTests` and `SlotLayoutEngineTests` SHALL keep passing.

#### Scenario: Existing tests pass
- **WHEN** the test suite runs after the refactor
- **THEN** `SlotWheelEditorViewModelTests` and `SlotLayoutEngineTests` SHALL pass without behavioral edits (only updates needed for changed constructor signatures)
