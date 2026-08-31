# fix-right-drag-release-passthrough — Design

## Context

The right-drag gesture today is decided entirely at button-down by modifiers (`RightDragGestureDetector.OnRightDown`), then the whole gesture is swallowed (`FeedRightDragGesture` sets `Handled = true`). There is no displacement threshold, no way to hand a "not really a gesture" click back to the source app, and `RefreshGestureConfig` can clear in-flight state via `Reset()`, leaking the release. The gesture path also dispatches at default priority and hides asynchronously, making it feel heavier than the hotkey path.

This design imports StarPie's proven model — *swallow the down, decide by displacement, replay a synthetic click on sub-threshold release* — while keeping Pulsar's modifier-gated summon (two menus need two modifiers) and its existing `Handled`/swallow semantics. Defaults preserve current behavior.

Reference: `Docs/roadmap/RIGHT_DRAG_GESTURE_ANALYSIS.md`; StarPie `GestureController.ProcessMove` / `MouseHook.ReplayTriggerClick`.

## Goals / Non-Goals

**Goals:**
- A right-button release after a gesture must never reach the source application.
- A "plain click" (pressed with modifier but no real drag) must still show the native context menu via a replayed click.
- Summon/close latency must be at parity with the hotkey path.
- Config changes must not break an in-flight gesture.

**Non-Goals:**
- Changing the hotkey summon path.
- Adding outer-escape-cancel / full-screen / process-isolation (tracked separately in roadmap direction 1).
- Changing modifier-gated semantics (Shift=Action, Ctrl=Switcher) or menu internals.

## Decisions

### D1: Threshold state machine in `RightDragGestureDetector`

**Decision**: Extend `RightDragGestureDetector` from a 2-state (pressed/summoned) into a threshold-aware state machine:

- `Down` → decide by modifiers (unchanged). On a summon decision, enter `WaitingForThreshold` (pressed, not yet summoned).
- `Move(distanceSquared)` → when distance ≥ `threshold²`, transition to `Summoned`.
- `Up` →
  - if `Summoned` → `GestureRelease` (execute selection; swallow).
  - if `WaitingForThreshold` → `SubThresholdRelease` (replay click; swallow).
  - if not pressed → `None` (pass through).

Add a `FeedDisplacement(double dx, double dy)` method so the caller reports movement; the detector returns whether the threshold was crossed (so the caller can summon the menu on the UI thread exactly once).

**Summon timing**: `GestureSummonMode` controls *when the menu is summoned* and therefore *what a release means*:

- `Immediate` (default): menu summons at button-down (current behavior). The threshold is irrelevant for summoning; on `Up` the menu always gets `GestureRelease`. This mode preserves today's muscle-memory UX exactly, and its leak-fix comes entirely from D3 (release-race guard + defer-reset).
- `OnThreshold`: menu summons on the first move that crosses the threshold. On `Up`: if the menu was summoned → `GestureRelease`; if still sub-threshold → `SubThresholdRelease` (replay the click). This matches StarPie's "menu appears only once you drag; otherwise the click passes through as a normal right-click".

**New enum value**: `RightDragGestureDecision.SubThresholdRelease`.

**Move-event source (OnThreshold)**: the low-level hook (`GlobalMouseHook`) currently raises only down/up/wheel — no `WM_MOUSEMOVE`. `OnThreshold` needs displacement while pressed-but-not-summoned, so `GlobalMouseHook` gains a move event (`WM_MOUSEMOVE`, `MSLLHOOKSTRUCT.pt`), surfaced via `GlobalMouseService` and consumed by `FeedRightDragGesture` to feed `FeedDisplacement`. `Immediate` mode does not require it (menu already up; the rendering-loop `MouseTrackingService` tracks hover as today).

**Rationale**: A single owner of press/state eliminates the leak: the release decision now always has a concrete outcome (execute / replay / pass) instead of falling through to `None` when `IsSummoned` was lost.

### D2: Synthetic click replay with recursion suppression

**Decision**: Add replay support to the native hook layer:

- `GlobalMouseHook.ReplayClick(GlobalMouseButton button)` synthesizes `down`+`up` via `mouse_event` (right → `MOUSEEVENTF_RIGHTDOWN|RIGHTUP`, with middle/XButton support for future multi-trigger), and sets `_ignoreNextButtonDown`/`_ignoreNextButtonUp` so the *replayed* events pass straight through `CallNextHookEx` without re-entering gesture logic.
- `GlobalMouseEventArgs` gains `IsReplayed` (or the hook exposes an internal `IgnoreNext{Button}Down/Up` consumed in `HookCallback` before raising events) so Pulsar's event subscribers never see the synthetic events as user input.
- `GlobalMouseService` exposes `ReplayRightClick()` delegating to the hook.

**Rationale**: Mirrors StarPie `ReplayTriggerClick`. The `_ignoreNext*` flags prevent the infinite loop that would otherwise occur because the replayed down/up arrive back in the same hook.

### D3: Release-race hardening

**Decision**:
- `RefreshGestureConfig()` guards the `Reset()`: only call `_gestureDetector.Reset()` when `!_gestureDetector.IsPressed`; otherwise defer applying the new config until the gesture completes (store `_pendingGestureConfig` and apply on the next `GestureRelease`/`SubThresholdRelease`/`None` up).
- In `FeedRightDragGesture`, when a right-button *up* arrives while `_session.IsVisible` and the detector reports `None` (e.g. state was lost), still swallow it and route to `HandleGestureRightReleaseAsync` — a visible menu must never leak its release.

**Rationale**: Root cause B. The menu-visible guard is a belt-and-suspenders guarantee independent of detector state.

### D4: Dispatch & close timing parity

**Decision**:
- `FeedRightDragGesture` dispatches summon/show and release handling at `DispatcherPriority.Input` via `IUiDispatcher` (add an overload or use `Application.Current.Dispatcher.InvokeAsync(action, DispatcherPriority.Input)`), matching StarPie's `BeginInvoke(DispatcherPriority.Input)`.
- `HandleGestureRightReleaseAsync` sets `IsVisible = false` *synchronously* at the top of the release handling (after capturing hit-test state), then awaits execution — the menu disappears immediately on release, and the await only drives the action, not the hide.
- The loading-release quick-switch path already runs without waiting for the page load; keep it, but also hide first.

**Rationale**: Root cause C. Hiding on release (not after `await`) removes the perceived lag; input priority closes the dispatch gap to the hotkey path.

### D5: Settings & defaults

**Decision**: Add to `ProfileSettings`:
- `GestureSummonMode SummonMode` (enum `Immediate`/`OnThreshold`, default `Immediate`).
- `double GestureDragThreshold = 25.0` (DIPs).

Persist as camelCase in `Profiles.json` like existing settings. Add `Settings.General.GestureSummonMode` / `Settings.General.GestureDragThreshold` keys to both resx files; surface in `SettingsGeneralPage.xaml` beside the existing `EnableRightDragSummon` toggle. `RightDragModifiersConflict` still disables the gesture entirely.

**Rationale**: Additive settings, no schema break. Default `Immediate` keeps all existing tests/behavior green.

## Risks / Trade-offs

- **[Risk] Replay may still race the target app's own right-click handling in exotic cases** → the ignore-next flags suppress self-recursion; a replayed click is a normal right-click the app sees once. Mitigated by using `mouse_event` at the current cursor position (same as StarPie).
- **[Trade-off] `OnThreshold` summon delays the menu until the user drags** → this is opt-in; `Immediate` remains default so muscle-memory behavior is unchanged.
- **[Trade-off] Synchronous hide on release** → the menu disappears before the action completes; the existing action-feedback (`IActionFeedbackService`) already provides completion feedback, so this is the desired interaction.
- **[Risk] New `SubThresholdRelease` decision enum value ripples to call sites** → the enum is internal to the gesture path; only `FeedRightDragGesture` and tests handle it.
