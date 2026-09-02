## Why

The roadmap's Direction 3 (级联子菜单泛化) is the largest remaining gap. Today `RadialMenuSubMenuCoordinator` is hardwired to window switching: `IMenuSession.EnterSubMenuAsync` takes `List<ProcessWindowInfo>`, the coordinator always fills slots with `WindowSwitchStrategy`/`BackActionStrategy`, and `SlotViewModel` has no sub-action tree concept. This makes any second submenu form (StarPie-style Fan/同心子环, plugin-authored cascades) impossible without first generalizing the data model and invocation contract. This change lays that foundation — it must land first, before layout (Change B) and editor (Change C).

## What Changes

- **`SlotViewModel` gains a `SubSlots` collection** (`ObservableCollection<SubSlotDescriptor>`), and `PluginSlot` gains an optional persisted sub-action list, so a root slot can carry its own cascade without inventing one at runtime.
- **`IMenuSession.EnterSubMenuAsync` is generalized** (**BREAKING**): the window-list signature is replaced by a `SubMenuDescriptor` (typed payload + strategy id + metadata), decoupling the session contract from `ProcessWindowInfo`.
- **`RadialMenuSubMenuCoordinator` is strategy-ized**: window switching becomes one `ISubMenuStrategy` implementation (`WindowSwitchSubMenuStrategy`) configured from a `SubMenuDescriptor`; the existing `BackActionStrategy` center-slot behavior is folded into the coordinator contract.
- **Non-window cascade paths become reachable** via the session, even if no concrete non-window strategy ships in this change (layout in Change B, editor in Change C).

## Capabilities

### New Capabilities
- `cascade-submenu-model`: The `SubSlots` data model (`SubSlotDescriptor` on `SlotViewModel`, optional persisted sub-actions on `PluginSlot`) and the generalized `IMenuSession.EnterSubMenuAsync(SubMenuDescriptor)` invocation contract.
- `submenu-coordinator-strategy`: The strategy-based submenu coordinator — `ISubMenuStrategy` contract, `SubMenuDescriptor` routing, and window switching reduced to one concrete strategy.

### Modified Capabilities
- `radial-menu`: Submenu entry is no longer hardwired to window groups; a highlighted slot may open a cascade described by its own `SubSlots`. Window-group drill-in remains behaviorally identical (regression contract).
- `window-switching-architecture`: Window-submenu configuration becomes one implementation of the generic coordinator strategy; the shared window-selection contract (`window-switch-selection-core`) is unchanged.

## Impact

- **Affected code**:
  - `ViewModels/IMenuSession.cs` — `EnterSubMenuAsync` signature change (**BREAKING**; the only breaking surface in this change)
  - `ViewModels/MenuSession.cs` — `EnterSubMenuAsyncCore` / `RestoreRootMenuAsync` adapt to `SubMenuDescriptor` + strategy routing
  - `ViewModels/RadialMenuViewModel.cs` — thin `EnterSubMenuAsync` projection updated
  - `ViewModels/RadialMenuSubMenuCoordinator.cs` — refactored into a strategy host; window path extracted
  - `ViewModels/Strategies/` — new `ISubMenuStrategy`, `WindowSwitchSubMenuStrategy`; `ProcessGroupStrategy` still constructs descriptors
  - `ViewModels/SlotViewModel.cs` — add `SubSlots` collection
  - `Models/` — `PluginSlot` optional `SubActions` list (persisted, backward-compatible deserialization); new `SubMenuDescriptor` / `SubSlotDescriptor`
  - `ConfigService`/config model — tolerant of the new optional `subActions` field (no migration required)
- **APIs**: `IMenuSession` public contract changes; all existing submenu tests must be updated or proven behavior-identical.
- **Tests**: `MenuSessionTests`, `GroupedSlotInteractionTests`, `WindowSwitchStrategyTests` re-anchored on the new descriptor flow; new `SubMenuCoordinatorStrategyTests`.
- **Dependencies**: no new packages. Reuses `ISlotLayoutEngine` (unchanged in this change), existing animation/paging controllers.

**Out of scope (Change B / C)**: Ring/Fan sub-layout geometry in `SlotLayoutEngine`, second-level action editor UI, smart default injection.
