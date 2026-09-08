# ADR-028: Window History Single Authority — MenuPrevious + MRU Stack in One Module

**Status**: Accepted
**Date**: 2026-09-08
**Deciders**: milo (owner), WorkBuddy agent (grill auto + implementation)

---

## Context

The 2026-09-08 architecture review surfaced candidate C3: "MRU 'previous window'
semantics split across three state machines, no single authority". Focused fact
collection reframed the claim:

- **One active dual-track**: `WindowTrackingService.PreviousWindowHandle`
  (single slot, written only when the Radial Menu is invoked, deliberately
  *unfiltered* — plugin contexts need the real foreground window even when it is
  not Alt-Tab-valid) and `QuickSwitchEngine._windowHistory` (10-deep MRU stack +
  5s switch pair, written on every eligible activation, strictly filtered).
- **One fully dead subsystem**: `FocusManager.IFocusHistory`
  (`RecordWindow` had zero production callers, the two-slot state was never
  written, `QuickSwitchAsync` had zero callers, its DI registration had zero
  consumers). `WindowService.RecordPreviousWindow()` and
  `WindowTrackingService.RegisterOrUpdateWindow()` were equally dead (ADR-010
  already flagged the latter).

The two tracks were not redundant — they serve different consumers (PulsarContext
vs quick-switch resolution) — but their state lived in different modules with no
locality: consumers had to know which module to ask.

## Decision

1. **QuickSwitchEngine is the single Window History authority.** The unfiltered
   menu snapshot moves into it as `SetMenuSnapshot` / `GetMenuSnapshot`
   (thread-safe via `Interlocked`/`Volatile`, still zero P/Invoke, still pure
   logic). The filtered MRU stack and switch pair stay where they are. The
   engine now exposes both views explicitly: MenuPrevious and HistoryTop.
2. **`WindowTrackingService` keeps only the window metadata registry**
   (FirstSeen / LastActivation) used by inventory sorting. Its
   `PreviousWindowHandle` slot and dead `RegisterOrUpdateWindow` are removed.
3. **Dead code is deleted, not kept for legacy**: `IFocusHistory`
   (interface + `FocusManager` explicit implementation + DI registration),
   `IFocusManager.QuickSwitchAsync`, `QuickSwitchResult`,
   `IWindowFocusContextService.RecordPreviousWindow`, and
   `WindowService.RecordPreviousWindow`.
4. **The facade stays put.** `IWindowService` / `IWindowFocusContextService`
   surface unchanged to the 24 existing consumers (ADR-010 kept promise);
   only the internals re-point: all five `PreviousWindowHandle` read/write
   sites in `WindowService` now read/write the engine's MenuPrevious slot.
   The four MenuSession quick-switch paths are untouched (they belong to
   candidate C1).
5. **Qualification stays at the eligibility seam.** The engine's snapshot is
   written unfiltered, but `ResolveTarget` re-validates every candidate, so an
   invalid MenuPrevious is never actually selected.

## Consequences

### Positive
- **Locality**: previous-window recall has one home and two named views
  (MenuPrevious / HistoryTop); consumers ask one module.
- **Deletions**: ~90 lines of dead infrastructure removed (interface, explicit
  impl, dead facade methods, dead registry method, dead result type, DI line).
- **Testability**: the previously untested slot semantics now have 6 new engine
  tests + a facade dual-track pin test (ineligible window → snapshot set,
  history skipped); the capacity-trim and multi-level fallback gaps are now
  characterized (previously 0 coverage).
- **Closes ADR-010's open follow-up** ("reconcile the twin previous window
  state machines") — the reconciliation deleted one twin outright and merged
  the active pair into a single module.

### Negative
- `WindowTrackingServiceTests` lost its only test that exercised
  `RegisterOrUpdateWindow`; it was rewritten to pin the surviving
  first-seen/activation semantics instead.
- The MenuPrevious slot is an `IntPtr` cache without ownership semantics —
  same as before; stale handles are guarded at resolution time, not at write
  time.

### Neutral
- The unfiltered/filtered distinction is now a documented, tested contract
  rather than an accident of two modules.

## Related Decisions

- [ADR-010: Window service deepening](./010-window-service-deepening.md)
  (the twin-state-machine follow-up this ADR closes)
- [ADR-004: Window history stack](./004-window-history-stack.md)
  (10-deep stack + fallback semantics preserved unchanged)
- [ADR-023: Page provider factory seam](./023-page-provider-factory-seam.md)
  (prior deepening; MenuSession quick-switch paths remain its concern)
