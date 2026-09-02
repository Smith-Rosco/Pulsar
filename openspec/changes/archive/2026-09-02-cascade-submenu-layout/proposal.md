## Why

Change A (`cascade-submenu-foundation`) delivered the data model (`SubSlots`, `SubSlotDescriptor`) and the strategy-based coordinator, but `CascadeSubMenuDescriptor` remains an unwired placeholder: there is no geometry to lay child slots out and no way to hit-test them. The roadmap's Direction 3 second step is exactly this — Ring/Fan sub-layouts plus their hit-testing. Without it, a configured cascade slot has nothing to render, so the entire feature is dead weight.

## What Changes

- **New sub-layout engine** (`ISubMenuLayoutEngine`): computes Ring (concentric sub-ring) and Fan (sector-fan) child-slot positions from a parent slot pose, independent of the root `SlotLayoutEngine`.
- **Hit-testing for sub-layouts**: Ring hit-test by angle+distance against the sub-ring; Fan hit-test by nearest-angle among the fan wings (StarPie `HitTestFanSubs` geometry), returning the child index.
- **`CascadeSubMenuDescriptor` becomes real**: it now carries a `LayoutStyle` (Ring/Fan) and is routed by the coordinator to a new `CascadeSubMenuStrategy` (id `cascade`) that fills slots from `SubSlots` and reads the descriptor's pagination window.
- **Pagination semantics for cascades**: sub-ring pagination reuses the existing submenu paging surface; when child count exceeds slots-per-page, later children are paged exactly like window submenus.
- **DPI-safe hit-testing**: sub-layout hit tests operate on window-relative DIP coordinates already produced by `MouseTrackingService`/`MenuViewportService`, avoiding StarPie's high-DPI pixel-miss pitfalls.

## Capabilities

### New Capabilities
- `cascade-submenu-layout`: Ring/Fan sub-layout geometry (slot positioning from a parent pose) and the hit-testing rules for both forms — DIP-relative, StarPie-inspired.

### Modified Capabilities
- `cascade-submenu-model`: `CascadeSubMenuDescriptor` gains a `LayoutStyle` (Ring/Fan) and becomes a first-class, routable submenu payload instead of a placeholder; `SubSlots` count drives pagination.
- `submenu-coordinator-strategy`: a concrete `CascadeSubMenuStrategy` (id `cascade`) is registered and routed; window switching remains unchanged; unknown-id fallback behavior is preserved.

## Impact

- **Affected code**:
  - New `Services/Interfaces/ISubMenuLayoutEngine.cs` + `Services/SubMenuLayoutEngine.cs` (pure geometry, testable headless)
  - `ViewModels/Strategies/CascadeSubMenuStrategy.cs` (new `ISubMenuStrategy` impl, id `cascade`)
  - `Models/CascadeSubMenuDescriptor.cs` — add `LayoutStyle` enum (`Ring`/`Fan`, default `Fan`), keep `StrategyIdValue = "cascade"`
  - `ViewModels/RadialMenuSubMenuCoordinator.cs` — route cascade descriptors; register strategy in `App.xaml.cs` `ConfigureServices`
  - `ViewModels/MenuSession.cs` — submenu hit-test path selects Ring/Fan geometry for cascade submenus; pagination readout from `SubSlots.Count`
  - `Models/` — `SubMenuLayoutStyle` enum
- **APIs**: `ISlotLayoutEngine` untouched; new `ISubMenuLayoutEngine` interface. No breaking changes to existing consumers.
- **Tests**: new `SubMenuLayoutEngineTests` (Ring/Fan positions + hit tests, incl. DIP rounding); `SubMenuCoordinatorStrategyTests` extended for cascade routing; `GroupedSlotInteractionTests`/window suites must stay green (window path untouched).
- **Dependencies**: none new. Reuses existing submenu morph/animation/paging controllers in `MenuSession`.

**Out of scope (Change C)**: second-level action editor UI, smart default injection, theme/palette for sub-ring, editor integration.
