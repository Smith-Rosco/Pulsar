## Context

See proposal.md — Why. Current state that shapes the approach:

- Change A shipped the foundation: `CascadeSubMenuDescriptor` (`Models/CascadeSubMenuDescriptor.cs:12`, id `cascade`, carries `SubSlots`, `TotalSlotsHint`) is an explicit placeholder — "Reserved for Change B — no strategy ships in this change". `RadialMenuSubMenuCoordinator` (`RadialMenuSubMenuCoordinator.cs:33`) routes `ISubMenuStrategy` by `StrategyId` and falls back to root on unknown ids.
- The coordinator's `ConfigureSubMenu(SubMenuDescriptor, slotsPerPage, pageIndex, centerSlot, slots)` already passes `SubMenuContext` (center, slots, slotsPerPage, pageIndex) to each strategy.
- Root layout lives in `SlotLayoutEngine` (`GetSlotPosition` evenly spaces on a ring; `HitTest` returns sector index). Sub-layout must NOT disturb it.
- Window-relative DIP coordinates are already produced by `MouseTrackingService.ScreenToRelative` (`TransformFromDevice`) and `MenuViewportService` — hit-testing inputs are DIP, so no second DPI transform is needed. StarPie's high-DPI pitfall was doing pixel math outside this seam.
- `MenuSession.HitTest` (`MenuSession.cs:1407`) is the single hit-test entry the mouse path uses; submenu paging reads `_subMenuPage`/`_subMenuTotalPages`.
- StarPie reference geometry: `FanSubmenuSlotCount=3`, `GetFanSubOffset` (upper/tip/lower wing unit offsets), `GetFanSlotIndex` (1→tip, 2→wings, 3→all), `GetFanExtentRadius`, and `HitTestFanSubs` (nearest-angle selection in a rotated u/v basis).

## Goals / Non-Goals

**Goals:**
- Add `ISubMenuLayoutEngine` with pure Ring + Fan geometry (positions + hit tests) that takes a parent slot pose, not the root slot count.
- Wire `CascadeSubMenuStrategy` (id `cascade`) to render `SubSlots` children and route through the coordinator without touching the window path.
- Keep hit-testing DIP-relative end-to-end.

**Non-Goals:**
- Editor UI / smart default injection / sub-ring theming — Change C.
- Changing `ISlotLayoutEngine` or the window-switch strategy behavior.
- StarPie's full renderer machinery (HexagonHive shapes, glow presets) — only geometry + hit rules transfer.

## Decisions

### D1 — New `ISubMenuLayoutEngine`, not an extension of `ISlotLayoutEngine`
`SlotLayoutEngine` is the root ring's contract (slot-count-driven); cascade geometry is parent-pose-driven (parent center, direction, sub-ring radius, slot size). Two different inputs, two seams. The new engine is a separate interface + impl registered in DI, mirroring how `StyleRendererFactory`/`ISubMenuStrategy` plug in.

- Alternative: overload `SlotLayoutEngine` — rejected, couples root ring density math to cascade geometry and risks the "interface default arg drift" pitfall documented in `ISlotLayoutEngine.cs`.

### D2 — Fan geometry ported from StarPie with normalization
Port `GetFanSubOffset` (upper/tip/lower), `GetFanSlotIndex` (1/2/3 mapping), and `HitTestFanSubs` (u/v basis + nearest-angle) into `SubMenuLayoutEngine`, but:
- accept the parent direction as an explicit angle (radians) instead of reading a global profile's `SectorCount`;
- cap Fan at 3 children; >3 falls back to Ring (spec: `cascade-submenu-layout`);
- hit-test band = `[deadZone, fanExtent]` from the parent pose, rejecting points outside with -1.

### D3 — Ring layout uses angular distribution from parent direction
Ring child positions distribute evenly over 360° starting at the parent direction angle, on a sub-ring radius derived from the parent pose (`subRingRadius = parentDirection-scaled`, clamped inside the 500×500 canvas). Hit-test = sector index within the sub-ring band, mirroring the root engine's angle math but centered on the cascade origin.

### D4 — `CascadeSubMenuStrategy` mirrors `WindowSwitchSubMenuStrategy`
New `ISubMenuStrategy` impl with id `cascade`: center slot → `BackActionStrategy` + cascade label; children → child `PluginActionStrategy` built from each `SubSlotDescriptor` (plugin/action/args/label/icon); empty slots → `NoOpStrategy`. Pagination from `SubSlots.Count` reusing the coordinator's `slotsPerPage`/`pageIndex`. It reads `LayoutStyle` from the descriptor to pick Ring vs Fan engine calls.

- Child execution reuses the existing `PluginActionStrategy` (`SlotStrategies.cs:39`) so a child executes its sub-action with full plugin pipeline (usage tracking, circuit breaker) — no new execution path.
- Alternative: a bespoke child strategy — rejected, duplicates the plugin dispatch pipeline.

### D5 — DIP discipline as an invariant
All sub-layout inputs (parent pose, cursor point) are window-relative DIP. `MenuSession` converts the global mouse point once via the existing `MouseTrackingService`/`MenuViewportService` seam before calling `ISubMenuLayoutEngine`; the engine never multiplies by a DPI factor. This is the explicit StarPie pitfall-avoidance rule.

### D6 — Session integration seam
`MenuSession.HitTest`/submenu morph checks `_menuState == SubMenu` + the active strategy; when the active submenu is a cascade, it dispatches to `ISubMenuLayoutEngine` (style from descriptor) instead of the root `HitTest`. Pagination readout for cascades uses the coordinator's page state (Change A's D4 seam) — session reads, never mutates.

## Risks / Trade-offs

- [Fan >3 children silently becomes Ring] → spec'd and unit-tested; user-visible only for >3 sub-actions, which Change C's editor will cap.
- [New engine seams diverge from root layout] → Ring hit math intentionally mirrors `SlotLayoutEngine` so hover feels consistent; parity asserted in tests.
- [Cascade children need plugin metadata at config time] → `PluginActionStrategy` resolves metadata lazily via the registry; a child whose plugin/action is unknown is marked not-enabled rather than throwing.
- [Session hit-test branch grows] → keep the branch small (state + style → engine call); no layout knowledge leaks into `MenuSession`.

## Migration Plan

1. Add `SubMenuLayoutStyle` enum + `LayoutStyle` on `CascadeSubMenuDescriptor` (default `Fan`).
2. Add `ISubMenuLayoutEngine` + `SubMenuLayoutEngine` (Ring + Fan geometry + hit tests) with headless unit tests.
3. Add `CascadeSubMenuStrategy` (id `cascade`), register in `App.xaml.cs`.
4. Route cascade descriptors in `MenuSession` hit-test/paging via the coordinator seam.
5. Extend `SubMenuCoordinatorStrategyTests` (cascade routing, window unaffected, unknown-id fallback intact); full suite green.
6. Rollback: removing the cascade strategy registration alone restores Change A behavior; window path never changes.

## Open Questions

- None blocking. Sub-ring radius tuning (ratio/absolute) is a Change C visual decision and does not alter geometry contracts here.
