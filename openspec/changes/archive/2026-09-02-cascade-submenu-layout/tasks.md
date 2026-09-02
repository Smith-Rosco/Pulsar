## 1. Layout style on descriptor

- [x] 1.1 Add `SubMenuLayoutStyle` enum (`Ring`, `Fan`) in `Models/` and verify it compiles
- [x] 1.2 Add `LayoutStyle` (default `Fan`) to `CascadeSubMenuDescriptor` in `Models/CascadeSubMenuDescriptor.cs` and verify it compiles
- [x] 1.3 Add `SubMenuLayoutStyle` + descriptor tests: default Fan, explicit Ring/Fan round-trip

## 2. SubMenuLayoutEngine (pure geometry)

- [x] 2.1 Add `ISubMenuLayoutEngine` in `Services/Interfaces/` with `ComputeChildPositions(ParentPose, style, childCount)` and `HitTestChild(point, ParentPose, style, childCount)` and verify it compiles
- [x] 2.2 Implement `Services/SubMenuLayoutEngine.cs` — Fan geometry ported from StarPie (`GetFanSubOffset`/`GetFanSlotIndex`, cap at 3, >3 → Ring fallback) and Ring geometry (even angular distribution from parent direction, sub-ring band hit-test)
- [x] 2.3 Add `SubMenuLayoutEngineTests`: Fan 1→tip, 2→wings, 3→all-wings, >3→Ring; Ring single + multi distribution; determinism/repeatability; child positions within 500×500 canvas
- [x] 2.4 Add hit-test tests: Ring dead-zone → 0, band sector index, outside band → -1; Fan nearest-wing selection, dead-zone → -1, beyond fan extent → -1; DIP input used without second transform

## 3. CascadeSubMenuStrategy

- [x] 3.1 Create `ViewModels/Strategies/CascadeSubMenuStrategy.cs` (id `cascade`): center `BackActionStrategy` + cascade label; children from `SubSlotDescriptor` → `PluginActionStrategy` (plugin/action/args/label/icon); empty slots → `NoOpStrategy`; unknown plugin/action child marked not-enabled
- [x] 3.2 Add `CascadeSubMenuStrategyTests`: center back-nav, child strategy mapping, empty-page no-op fillers, unknown child not-enabled, pagination from `SubSlots.Count`
- [x] 3.3 Register `CascadeSubMenuStrategy` in `App.xaml.cs` `ConfigureServices` and verify `dotnet build Pulsar/Pulsar/Pulsar.csproj` resolves with 0 errors

## 4. Coordinator & session routing

- [x] 4.1 Verify `RadialMenuSubMenuCoordinator` routes `CascadeSubMenuDescriptor` to the cascade strategy via existing `StrategyId` dictionary; extend `SubMenuCoordinatorStrategyTests` (cascade routing, window strategy unchanged, unknown-id fallback intact)
- [x] 4.2 In `MenuSession` hit-test path, dispatch to `ISubMenuLayoutEngine` (style from descriptor) when the active submenu is a cascade; verify `dotnet build` 0 errors
- [x] 4.3 Wire cascade pagination through the coordinator's page state seam (session reads, never mutates); verify `HandleSubMenuMouseWheel`/paging behavior compiles and window path unchanged

## 5. Tests & verification

- [x] 5.1 Run `dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj` — full suite green (baseline 827+, no regressions in `GroupedSlotInteractionTests`/window-submenu suites)
- [ ] 5.2 Manual QA (requires human): configure a slot with 2-3 sub-actions, open cascade → Fan layout renders + selects correctly; 4+ sub-actions → Ring fallback; pagination works; both themes
