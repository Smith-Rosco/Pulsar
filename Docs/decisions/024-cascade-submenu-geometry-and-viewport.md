# ADR-024: Cascade Submenu Geometry — Parent-Anchored Fan, Replace-Mode Ring, No Viewport Glide

**Status**: Accepted (implemented 2026-09-06, route A — dedicated `SubMenuSlots` collection; 1098/1098 tests green)
**Date**: 2026-09-06
**Deciders**: Pulsar Development Team
**Related**: `Docs/archive/2026-08-16-FULLSCREEN_VIEWPORT.md`; ADR-008 (Menu Session refactor); ADR-020 (right-drag gesture orchestration); change `2026-09-05-cascade-submenu-fan-qa`

---

## Context

The cascade submenu (a root slot's own sub-actions, `CascadeSubMenuDescriptor`) misbehaves in three observable ways, all traced to the same root: **the submenu geometry is anchored to the main wheel's center instead of the parent slot.**

Verified root causes (all line numbers as of this ADR):

1. **Fan slots overlap the main wheel slots.** `MenuSession.BuildCascadeParentPose` (`MenuSession.cs:2038-2066`) hardcodes the pose center to `CenterX/CenterY` (= 250, the canvas center = main wheel center) and the sub-ring radius to `_currentRadius * SubMenuRingRadiusRatio` where `SubMenuRingRadiusRatio = 0.90` (`:68`). With `R = 90` (the radius `SlotLayoutEngine.CalculateOptimalRadius` returns for the default 8 slots/page, `SlotLayoutEngine.cs:64-75`) children land at radius **81 while the root slots sit at 90** — slot size is 50, so a child at 81 and a root slot at 90 are 9 DIP apart with 25 DIP radii. Overlap is arithmetically guaranteed. The history comment at `:60-67` shows the ratio was walked 0.60 → 0.75 → 0.90, i.e. the team has been tuning a radius that can never be right while the center is wrong.

2. **A shrink animation plays on the main wheel.** Two separate effects, both unconditional for every submenu entry:
   - `AnimateMenuCenterAsync` (`:2490-2508`, called at `:2605`) glides `_menuCenterX/Y` from the summon point to the click point, translating the whole 500×500 `MenuCanvas` across the screen.
   - `EnterSubMenuAsyncCore` (`:2638-2650`) collapses every other slot plus the center to `SubMenuCollapsedScale = 0.45` / `SubMenuCollapsedOpacity = 0.0` (`:57-58`), then in Fan mode blooms the fillers back in (`:2686`). Net perception: "the main wheel shrinks and springs back."

3. **The Fan slot cap is invisible.** `SubMenuLayoutEngine.FanMaxSlots = 3` (`SubMenuLayoutEngine.cs:22`) silently falls back to Ring geometry for `childCount > 3` (`:41`, `:84`). Since `_slotsPerPage = 8` (`:158`) and `GetCascadePageChildCount` (`:2032-2036`) returns `min(_slotsPerPage, remaining)`, a Fan slot configured with 4–8 sub-actions has **always rendered as Ring** — the fallback has never been observable in the UI, and the editor (`SlotEditorViewModel.cs:230-257`, `AddSlotContent.xaml:586-599`) has no count validation at all.

A fourth constraint was discovered while validating the design and it drives decisions D3/D5:

4. **Flick-out cancel is anchored to `_menuCenter`.** `UpdateFlickOutEscapeState` (`:1972-1991`) measures `dist(cursor, _menuCenter)` against `CalculateOptimalRadius(...) × 1.5` = **135 DIP**, and only for `RightDragGesture` invocations with flick-out enabled (`:1975`). When escaped, `HandleGestureRightReleaseAsync` returns at `:1615-1623` and **cancels the entire gesture — no slot executes**. Any design that moves submenu content away from `_menuCenter` without updating this check turns outer sub-slots into dead zones that silently swallow clicks.

### The viewport layering (why this ADR does not resize the canvas)

`Docs/archive/2026-08-16-FULLSCREEN_VIEWPORT.md` records why the window covers the whole monitor work area: to swallow mouse events, extend angular-sector hit-testing across the work area, and replace the old window-translation animation with in-window canvas translation. The 500×500 `MenuCanvas` is a **content coordinate system**, not a rendering boundary:

- `CenterX/CenterY = 250` is hardcoded in three places (`SlotLayoutEngine.cs:23-24`, `MenuSession.cs:46-47`, `RadialMenuLayoutCoordinator.cs:13`).
- `MenuVisualExtentDip = 260` (`RadialMenuWindow.xaml.cs:34`) feeds `ClampMenuCenter` (`MenuViewportService.cs:149-163`) so the menu is never clipped at a screen edge; when clamping displaces the center, `RequiresPointerWarp` (`:165-169`) triggers `SetCursorPos`.
- The repo contains **no `ClipToBounds` anywhere** — content drawn outside 0..500 still renders.

Enlarging the canvas to full-screen would buy nothing (animation mutates element transforms, so canvas size has no performance cost; the compositing cost belongs to the full-screen layered window) while breaking the clamp math and every deterministic geometry test. **Space audit**: with `gap = 70`, Fan children sit at radius 160, outer edge 185 — inside both `maxSafeRadius = 225` (`:2054-2056`) and `MenuVisualExtentDip = 260`. No resizing is required.

---

## Decision

**D1 — Fan geometry: concentric outer arc, parent-anchored direction, sector-constrained wings.**
Fan children keep the **main wheel center `(250,250)`** as their geometric center, sit at radius `R + gap` (larger than the main ring, so overlap becomes impossible), and spread at `parentDirection ± FanWingAngle` (30°). "Anchored to the parent slot" is expressed as *direction*, not as *center*. This satisfies both of the original requirements — "concentric with the main wheel, larger radius" and "fans out from the parent slot" — and leaves `SubMenuParentPose.CenterX/CenterY` untouched.

**D1a — Fan wings must stay inside the parent slot's own sector (v1.2.0, user spec 2026-09-06).**
\FanMaxWingRadians = min(30°, π/slotsPerPage)\ — with 8 root slots each owns a 45° sector, so the wings clamp to ±22.5°. In Fan mode the main wheel is a frozen backdrop (D4); an unclamped ±30° spread visually spilled into the neighbouring root slot's sector. The engine takes the cap from the pose (SubMenuParentPose.FanMaxWingRadians, default 30°), so a 6-slot wheel (sector half 30°) keeps the classic spread.

**D2 — Fan cap: 3, surfaced in the editor, engine fallback retained.**
`FanMaxSlots` stays 3 as an engine-level safety net (never lose data, never render garbage). The editor gains a **live, non-blocking warning** when a Fan slot has more than 3 sub-actions: "more than 3 sub-actions will be displayed as Ring". Saving is never blocked (blocking would interrupt configuration flow); truncation is rejected outright (silent data loss); pagination is rejected (Fan exists for small, high-frequency sets).

**D3 — No viewport glide, for either style.**
`AnimateMenuCenterAsync` is removed from the cascade entry/exit path for both Fan and Ring. `_menuCenterX/Y` stays at the summon point for the whole submenu lifetime.
- **Fan**: children center on `(250,250)` — the pose center is unchanged; only the radius changes.
- **Ring**: the pose center becomes the **parent slot's canvas position** (`_subMenuOriginX/Y`, already recorded at `:2552-2553`). This is the only geometry change Ring needs.

Note that both styles put the sub-wheel at the *same screen position* as today (where the parent slot already was); what changes is that the coordinate system and the exit animation no longer move.

**D4 — Fan interaction: the main wheel freezes into a read-only backdrop.**
While a Fan is open, only Fan children are hit-testable. Other root slots — and the center slot — do not highlight, do not preview, and do not respond to clicks. Clicking the parent slot again dismisses the Fan and restores the root menu. Nothing on the main wheel is transformed.

**D5 — Flick-out cancel becomes dynamic in both origin and radius.**
Replace the fixed `_menuCenter` + `R × 1.5` check with:

```
escaped = dist(cursor, activeWheelCenter) > activeWheelRadius × 1.5
```

| State | `activeWheelCenter` | `activeWheelRadius` | Threshold |
|---|---|---|---|
| Root menu | `_menuCenter` (summon point) | `R` = 90 | **135** (unchanged — zero regression) |
| Fan submenu | `_menuCenter` (concentric; center did not move) | `R + gap` = 160 | 240 |
| Ring submenu | parent slot position | sub-ring radius = 81 | **121.5** |

`BuildCascadeParentPose` already returns exactly `(center, radius)` for the active sub-wheel, so `UpdateFlickOutEscapeState` reuses it instead of growing a second source of truth. Origin must move with the radius: a radius-only increase would need ~196 DIP to cover Ring's far child (90 + 81 + 25), inflating the *root* threshold by 45%.

**D6 — Gap: 70 DIP, with graceful compression.**
`gap = 70` gives a 20 DIP visual clearance from the main ring's outer edge (115) while staying inside `maxSafeRadius`. When `R` grows (up to `SlotLayoutEngine.MaxRadius = 180` at high slot counts) and `R + gap` would exceed 225, **compress `gap` dynamically, never below 50** — 50 is the minimum value at which a Fan child's inner edge (`R + gap − 25`) still clears the main ring's outer edge (`R + 25`). Overlap must never be reachable.

**D7 — Ring: replace-mode, parent slot becomes the sub-wheel center.**
The main wheel **fades out only** (opacity → 0; no scale, no translation). The parent slot does **not** glide to the canvas center — it stays in place and *becomes* the sub-wheel's center slot, showing its own identity (icon + label, as today via `CascadeSubMenuStrategy.cs:64` `BackActionStrategy`), with click = return to root. The sub-wheel expands from that point as a complete ring. **v1.2.0 (user spec 2026-09-06):** the centre orb blooms back VISIBLE at the parent slot's position with the parent's icon AND label (it previously faded with the wheel and never reappeared, leaving the ring with an empty centre and an invisible dead-zone anchor). The hover-coordinator no longer resets a cascade centre to a generic "Back" label.

**D8 — Fan: parent slot stays in place and stays highlighted.**
Because the main wheel no longer moves, the parent slot must not glide to the center either. It remains on the ring, visually highlighted/outlined as the active anchor — this highlight is the *only* affordance telling the user how to dismiss the Fan, so it is not optional. The root menu's center slot keeps its root semantics but is frozen during the Fan (per D4).

**D9 — Entry/exit animation: bloom from the parent slot, nothing else moves.**
- Fan: children bloom from the parent slot's position to their arc positions (translate + fade, ~150 ms). The main wheel receives **no transform of any kind**.
- Ring: the main wheel fades out (no scale), children bloom from the parent slot position.
- Exit reverses it. `SubMenuCollapsedScale` / `SubMenuCollapsedOpacity` are deleted along with the collapse calls.

---

## Considered Options

- **Fan centered on the parent slot itself** (a separate arc whose center is the parent): rejected — it satisfies "centered on the parent" but breaks "concentric with the main wheel, larger radius", and it changes `SubMenuParentPose.CenterX/CenterY`, dragging the DPI/local-space translation at `HitTestCascadeSubMenu` (`:2011-2030`) into the change. D1 gets the same visual reading with a far smaller diff.
- **Keep the viewport glide for Ring** (Ring = replace-mode would then need no geometry change at all): rejected — the user explicitly wants the canvas to stop moving, and D3 + D5 together remove the motion without regression.
- **Hard editor validation blocking save at >3 sub-actions**: rejected — interrupting a configuration flow over a soft preference is worse than a warning; the engine fallback already guarantees correct rendering.
- **Raise the Fan cap to 5 with narrower wings**: rejected — 30° wing spacing already produced a hit-test bug (`change 2026-09-05-cascade-submenu-fan-qa`); narrowing it further invites a repeat.
- **Paginate Fan (>3 per page)**: rejected — introduces a paging interaction for a style meant for small sets.
- **Radius-only flick-out fix** (keep `_menuCenter` as origin): rejected — quantified above; costs ~45% more drag distance in the root menu.
- **Enlarge the 500×500 canvas / make it full-screen**: rejected — zero performance benefit (animation mutates transforms), destroys `ClampMenuCenter`'s extent math, and would force pointer warping on every summon. The space audit shows no need.
- **Fan children bloom with a scale pop** (0.45 → 1.0): rejected for the main wheel; retained as an option for children only. D9 uses translate + fade to keep the "grown from the parent" causality unambiguous.

---

## Consequences

Positive:
- Overlap becomes **structurally impossible** rather than tuned: children are outside the main ring by construction.
- The shrink animation and the canvas glide both disappear; entering a submenu no longer moves anything the user did not click.
- Flick-out cancel becomes semantically correct: "drag away from the wheel you are currently using", with the root-menu threshold bit-for-bit unchanged.
- Ring and Fan acquire clean, opposite semantics — **Ring = replace** (main wheel leaves, new full wheel at the parent), **Fan = overlay** (main wheel stays, small arc outside it).
- The Fan cap becomes visible at configuration time instead of being an invisible engine fallback.

Negative / trade-offs:
- Fan's flick-out threshold rises 135 → 240. Escaping a Fan needs a longer drag; this is the arithmetic consequence of `gap = 70` and is accepted.
- D7 changes Ring's center-slot host: the parent slot is reused in place rather than being moved to the canvas center. `CascadeSubMenuStrategy` must stop assuming the center slot sits at `(250,250)`.
- `RestoreRootMenuAsync` (`:2744`) currently animates the menu center back; with D3 that return glide must be removed as well (the canvas never left).

Risk:
- Any code that reads `_menuCenterX/Y` while a submenu is open must be re-audited for the new "center never moves" invariant. `UpdateFlickOutEscapeState` is the known instance (D5); `IsWithinQuickSwitchZone` and the magnetism controller are the next candidates.

## Implementation surface (indicative)

- `Services/SubMenuLayoutEngine.cs` — Fan branch uses the pose radius as-is (D1); `FanMaxSlots` retained (D2).
- `ViewModels/MenuSession.cs` — `BuildCascadeParentPose` (`:2038`): Fan radius `R + gap` with D6 compression; Ring center → `_subMenuOriginX/Y` (D3). `EnterSubMenuAsyncCore` (`:2513`): drop `AnimateMenuCenterAsync`, drop both collapse calls, drop the parent glide, parent stays in place (D7/D8/D9). `UpdateFlickOutEscapeState` (`:1972`): D5. `RestoreRootMenuAsync` (`:2744`): drop the return glide.
- `ViewModels/Strategies/CascadeSubMenuStrategy.cs` — center slot no longer assumed at canvas center (D7).
- `ViewModels/Dialogs/SlotEditorViewModel.cs` + `AddSlotContent.xaml` / `SlotConfigurationDialogContent.xaml` — live >3 warning (D2).
- Tests: `Services/SubMenuLayoutEngineTests.cs` (22), `ViewModels/CascadeSubMenuLayoutRuntimeTests.cs` (5), `ViewModels/CascadeSubMenuEntryTests.cs` (3) — Fan/Ring center + radius assertions, and new coverage for the D5 threshold table.

---

**Change History**:
- v1.2.1 (2026-09-06): D1a orb-clearance - fan wings clamp to sectorHalf minus the orb's angular half-width (13.6 deg at 8 slots / r=160), 3-child fan relaxes to tightest non-overlapping spread; E2E-verified (fan-sector-constraint-2).
- v1.2.0 (2026-09-06): User-spec amendments - D1a fan wings clamped to the parent slot's own sector (pi/slotsPerPage, 22.5 deg at 8 slots); D7 Ring centre orb blooms back visible as the parent slot (icon + label), hover no longer resets cascade centres. E2E-verified (fan-sector-constraint, ring-center-visible-4).
- v1.1.0 (2026-09-06): Route A implementation (dedicated SubMenuSlots collection + second ItemsControl), status Proposed - Accepted.
- v1.0.0 (2026-09-06): Initial version. Design settled through a grilling session (Q1–Q9); implementation pending.
