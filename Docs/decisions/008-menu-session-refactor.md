# ADR-008: Menu Session Refactor (Collapse the Radial Menu Interaction Cluster)

**Status**: Accepted (implemented 2026-08-18)
**Date**: 2026-08-18
**Deciders**: Pulsar Development Team

---

## Context

The Radial Menu interaction contract was split across a 1830-line `RadialMenuViewModel`, four shallow coordinators (Input, Layout, SubMenu, VisualState), two code-behind files, and a rendering sampler. Hover state existed in three places, paging in three counters, hit-testing in five paths, and the submenu morph choreography (~300 lines) never left the VM. `GroupedSlotInteractionTests` had to construct the VM via `RuntimeHelpers.GetUninitializedObject` + reflection `SetField` for 12 private fields and a live `Application` instance — the strongest evidence the interaction surface was untestable through its own interface.

## Decision

1. **Extract a `MenuSession` module** — a pure logic class owning the Radial Menu session state machine: visibility, active Slot, paging, submenu morph, transition guard, input decisions, hit-testing, and the menu watchdog. It is registered as a DI singleton and injected into the ViewModel.
2. **`RadialMenuViewModel` becomes a thin binding-projection layer** — it subscribes to input-source events (hotkey, global mouse, mouse tracking, config updated) on the Dispatcher and calls synchronous `HandleXxx` methods on `MenuSession`; it listens to `MenuSession.PropertyChanged` and forwards state changes onto the existing ~20 binding properties. It no longer owns interaction logic.
3. **Strategy seam narrows** — strategies depend on a new narrow `IMenuSession` interface (`IsVisible`, `SetActionExecuted`, `EnterSubMenuAsync`, `RestoreRootMenu`) instead of the whole ViewModel.
4. **Submenu morph choreography moves into `MenuSession`** — `MenuSession` holds `IAnimationController` (an existing mockable interface); the WPF animation implementation stays behind it.
5. **Hit-testing and dead-zone logic move into `MenuSession`** — `MouseTrackingService` keeps only sampling (rendering-loop cursor capture, DPI/screen-to-relative conversion); hit-testing becomes a pure method on `MenuSession`.
6. **Paging counters collapse** — `MenuSession` owns the single page counter; `PagingController`'s duplicate and the VM's submenu counter are folded in.
7. **The ViewModel remains the DataContext** — `SlotOrb.xaml` and `RadialMenuWindow.xaml` bindings are unchanged; `SlotViewModel` stays as-is (domain/visual state split deferred).
8. **Pure visuals stay in the view** — parallax rendering in `SlotOrb`, summon/dismiss and paging-nudge animations in `RadialMenuWindow.xaml.cs` remain code-behind and are out of scope.

## Considered Options

- **Fold coordinators back into the VM (no `MenuSession`)** — rejected: would grow the VM past 2000 lines and keep the reflection-based test construction. The deletion test failed: folding shallow modules into a shallow host only moves complexity.
- **Move all input routing into the view layer** — rejected: input decisions are behavior, not rendering; keeping them in the view makes the paging/quick-switch policy untestable.
- **Split `SlotViewModel` into domain + visual models** — rejected for this round: `SlotOrb.xaml` binds its properties directly; splitting would force view changes. Tracked as a future candidate.

## Consequences

- `GroupedSlotInteractionTests` remains as a regression baseline during migration; new `MenuSessionTests` construct the session directly with mocks, no WPF, no reflection.
- `RadialMenuViewModel` constructor dependency count drops from 15 to a few (Session + input sources + logger).
- The next UX change to the Radial Menu has one module to edit and test.
- The `IMenuSession` narrow interface becomes the seam between strategies and the session; test mocks target four members instead of the full VM surface.

---

**Change History**:
- v1.0.0 (2026-08-18): Initial version
