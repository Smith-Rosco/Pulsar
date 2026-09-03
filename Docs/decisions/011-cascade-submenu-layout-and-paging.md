# ADR-011: Cascade Submenu Ring/Fan Layout and Paging Semantics

**Status**: Accepted (2026-09-03)
**Date**: 2026-09-03
**Deciders**: Pulsar Development Team
**Supersedes**: the open question "Paging 与子环叠加时的分页语义需明确定义" from the roadmap (Direction 3 risks)
**Implementation**: `Services/SubMenuLayoutEngine.cs`, `ViewModels/Strategies/CascadeSubMenuStrategy.cs`, `MenuSession.cs`

---

## Context

Direction 3 of the roadmap generalized the submenu from "window switcher only" to a strategy-based system
(`SubMenuDescriptor` / `StrategyId`), adding the `cascade` strategy: a root slot can carry its own
`SubSlotDescriptor` list, laid out as a concentric sub-ring (**Fan**, ≤3 wings at ±30°) or a fallback
**Ring** band (>3 children), mirroring StarPie's `HitTestFanSubs` model.

Two concerns collide inside the sub-ring, and until now their interaction lived only in code:

1. **Paging** — Pulsar's root wheel is a paginated 4–12-slot disc (`IPagingController`, `drag-session-wheel-paging`).
2. **Fan geometry** — Fan caps at three wings; anything more must fall back to Ring.

Both are live at the same time once a cascade exceeds one page, and neither the roadmap nor any spec fixed
what "page" means down there, which layout form wins on which page, or what the hit-test and flick-out
radii are measured from. This ADR records the semantics as implemented and ratifies them.

## Decision

1. **Two independent page states.** The root wheel keeps its own page (`IPagingController`);
   the sub-ring has a separate `_subMenuPage` / `_subMenuTotalPages`. Entering any submenu resets the
   sub-page to 0; leaving it does not disturb the root page. Paging never crosses the boundary.

2. **One shared `SlotsPerPage`.** The sub-ring reuses the root's `SlotsPerPage` setting — there is no
   separate "submenu page size". A cascade's total pages are `ceil(SubSlots.Count / SlotsPerPage)`
   (`MenuSession`, line ~1964). Rationale: one mental model of "how many slots fit on a disc".

3. **Wheel is routed by menu state, not shared.** `HandleMouseWheel` dispatches to
   `HandleSubMenuMouseWheel` when `_menuState == SubMenu`, else to root paging. While a submenu is open
   the root page is suspended — it neither advances nor wraps. First/last-page wheel events at the
   sub-ring boundary emit boundary feedback (`OnPagingBoundaryFeedbackRequested`) instead of wrapping.

4. **Fan is a preference; Ring is the floor.** `CascadeSubMenuDescriptor.LayoutStyle` is the user's
   declared form. `SubMenuLayoutEngine` applies Fan **only when the current page's child count is ≤ 3**
   (`FanMaxSlots`); more children on that page fall back to Ring.
   **Accepted consequence**: a Fan-preferred cascade with more than 3 children can render page 1 as a
   Ring (4 children) and page 2 as a Fan (1 wing, centered). The form is per-page, not per-descriptor.
   Rationale: Fan with 4+ wings is unreadable and un-hit-testable at radial angles; a per-page fallback
   keeps every page legible without forcing users to give up Fan for small cascades.

5. **Hit-testing is page-scoped.** `HitTestCascadeSubMenu` computes the child count from
   `GetCascadePageChildCount` — the same page window the strategy filled. Children on other pages are
   never hit-testable; there is no "scroll into view" while hovering.

6. **Sub-ring radius derives from the root ring and clamps to the canvas.**
   `subRingRadius = clamp(_currentRadius * SubMenuRingRadiusRatio, 20, canvasSafeArea - halfSlot)`,
   where the safe area is the minimum distance from the menu center to each canvas edge minus half a slot
   (`BuildCascadeParentPose`). The parent direction is the polar angle from root center to submenu origin.

7. **Flick-out cancel stays root-radius-based at every depth.**
   `UpdateFlickOutEscapeState` multiplies the **root** wheel radius
   (`_slotLayoutEngine.CalculateOptimalRadius(_slotsPerPage, _currentSlotSize)`) by
   `GestureFlickOutRadiusMultiplier` — never the sub-ring radius. Flicking out therefore always means
   "abandon the entire menu", regardless of whether a submenu is open, and the escape radius does not
   jump around as the user drills in and out. Hotkey-summoned menus and disabled flick-out never
   participate, as before.

8. **Center label carries the page indicator.** `UpdateSubMenuCenterLabel` appends the localized
   `RadialMenu.SubMenuPageFormat` ("page x/y") whenever `_subMenuTotalPages > 1`, for cascades and
   window submenus alike. The cascade center slot itself remains the Back action.

## Considered Options

- **A dedicated submenu page size setting** — rejected: doubles configuration surface for a case the
  shared `SlotsPerPage` already covers; a second number invites "why do my two rings differ" bugs.
- **Fan that wraps or re-anchors for 4+ children** (StarPie-style adaptive re-anchor) — rejected: needs
  a second parent anchor and a second hit-test model, and a 4-wing Fan degenerates to a Ring anyway.
- **Root-radius-independent flick-out** (measure from the sub-ring while inside it) — rejected: the
  escape gesture would change meaning with depth and become non-deterministic to the muscle memory
  that flick-out exists to serve.
- **Let children hit-test across pages** (virtual scroll) — rejected: radial menus have no scroll
  affordance; off-disc children are invisible, and an invisible hit target is a misfire.

## Consequences

- The roadmap's Direction-3 risk "paging semantics undefined" is closed; the behavior is now citable.
- Per-page form switching (Decision 4) is visible and intentional; UI copy and the manual QA checklist
  (cascade-submenu-layout task 5.2) should expect Ring on dense pages of Fan-preferred cascades.
- `SlotsPerPage` changes affect the sub-ring's page count immediately, with no extra migration.
- Future renderers / layout engines must honour the same page-scoped hit-test contract; the seam is
  `ISubMenuLayoutEngine.ComputeChildPositions` / `HitTestChild` receiving the **page's** child count.
- No code change required by this ADR — it ratifies shipped behavior and the tests already covering it
  (`SubMenuLayoutEngine`, `CascadeSubMenuStrategy`, `CascadeSubMenuEntry`, `SubMenuCoordinatorStrategy`).

---

**Change History**:
- v1.0.0 (2026-09-03): Initial version — ratifies the semantics implemented by openspec changes
  `2026-09-02-cascade-submenu-foundation` / `-layout` / `-editor` / `-entry`.
