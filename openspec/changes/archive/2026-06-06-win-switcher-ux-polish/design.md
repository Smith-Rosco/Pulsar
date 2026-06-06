## Context

The WinSwitcher subsystem already has a robust activation pipeline (`WindowActivator`), feedback service (`IActionFeedbackService`), preview pipeline (`PreviewService` + `LiveWindowPreviewHost`), and selection engine (`WindowSelectionEngine`). These four changes are small additions to these existing services — no new subsystems or architectural patterns are introduced.

The radial menu session always originates at the cursor position, so the cursor monitor is a reliable proxy for "where the user is looking" when the radial menu appeared.

## Goals / Non-Goals

**Goals:**
1. Give users visual confirmation that a window switch completed, especially when UIPI prevents `SetForegroundWindow` from visibly raising the target (taskbar flash is the fallback signal).
2. Show a brief "Launching..." notification when SmartSwitch falls through to `Process.Start()` so the user isn't left wondering if anything happened.
3. Display actual window titles on sub-radial slots instead of opaque numbered suffixes, making it possible to distinguish between multiple windows of the same app.
4. Prefer same-monitor windows as a tiebreaker in multi-window selection to reduce cross-screen eye travel.

**Non-Goals:**
- Cross-process launch progress tracking (the toast is fire-and-forget).
- Full inline thumbnail previews on sub-radial slots (DWM limits + performance cost).
- Moving windows across monitors after activation.
- A HUD overlay for QuickSwitch (redundant with existing center orb preview and counter to QuickSwitch's "trusted fast switch" design intent).

## Decisions

### Decision 1: FlashWindowEx call site — in `WindowActivator`, not in callers

**Why**: `WindowActivator` is the single shared activation path (per `window-switch-activation-path` spec). Placing the flash here ensures every activation flow — grouped slot, plugin switch, QuickSwitch — gets the confirmation without modifying each caller.

**Alternatives considered**: Placing in `WindowService.ForceForegroundWindow()` — rejected because `ForceForegroundWindow` is used for focus restore (not user-initiated switch), where flashing would be distracting.

**Flash parameters**: `FLASHW_CAPTION | FLASHW_TRAY` with 3 flashes at system cursor blink rate. Flash only on success, not on already-foreground or failed activation.

### Decision 2: Launch toast — inject `ITrayService` into plugin, not `IActionFeedbackService`

**Why**: `IActionFeedbackService` returns a data object consumed after execution completes. The launch toast must fire *during* execution (before the potentially-blocking `Process.Start()` call). `ITrayService.ShowNotification()` is a fire-and-forget side effect that doesn't interfere with the PluginResult pipeline.

**Alternatives considered**: 
- Adding a "progress" concept to `PluginResult` — rejected; overengineered for a simple toast.
- Using `IActionFeedbackService` in `SlotStrategies` after the fact — rejected; the toast must appear *before* `Process.Start()` blocks, not after.

### Decision 3: Sub-radial window titles — set `Label` on `SlotViewModel` at sub-menu construction time

**Why**: `SlotViewModel.Label` is already the data-bound property driving the visual slot text. Sub-menu slots are built in `RadialMenuViewModel` when transitioning to a sub-menu. We set the window title as the label at that point, trivially.

**Truncation**: Window titles can be long. Truncate to 40 characters with ellipsis, matching Windows Alt+Tab conventions. Full title is available in the tooltip (already bound to slot metadata).

### Decision 4: Same-monitor preference — add as a secondary ordering criterion, not a filter or primary sort

**Why**: Activation recency remains the primary ranking signal (per `window-switch-selection-core` spec). Monitor preference is a tiebreaker applied only when candidates have equivalent `RealActivationTime`. Filtering out off-monitor windows would be too aggressive (the user may intentionally want a window on another screen).

**Implementation**: Add a `PreferredMonitorRect` (nullable `RECT`) to `WindowSelectionRequest`. In `WindowSelectionEngine.SelectTargetWindow()`, after the existing `OrderByDescending/ThenBy` chain, add a `ThenByDescending` that checks whether each window's rect intersects `PreferredMonitorRect`. Caller passes `null` to opt out (QuickSwitch, for example, shouldn't apply monitor preference since it's a deterministic pair-swap).

**Precision**: Use `GetWindowRect` to get candidate window bounds. Intersection check, not center-point check — a window spanning two monitors should still match either.

## Risks / Trade-offs

- **[Risk] FlashWindowEx may flash the wrong window if the target was already brought foreground by another mechanism between activation and flash** → Mitigation: Flash happens immediately after `SetForegroundWindow` in the same activation method; the race window is sub-millisecond.
- **[Risk] Launch toast may show stale "Launching..." if Process.Start fails silently** → Mitigation: WinSwitcherPlugin already has comprehensive exception handling around `Process.Start()`. If it fails, the PluginResult.Error message supersedes the toast. The toast auto-dismisses after the default balloon timeout (~5s).
- **[Risk] Long window titles may overflow the radial slot's circular layout** → Mitigation: Truncation at 40 chars + ellipsis. SlotOrb already has text fitting logic for varying label lengths.
- **[Risk] Same-monitor preference may cause unexpected selection when a long-inactive window on the same monitor outranks a recently-used window on another monitor** → Mitigation: Monitor preference is only a tiebreaker, applied only when activation times are equivalent. The primary ranking by RealActivationTime is untouched.
