## Context

See proposal.md — Why. The gesture release path lives in `MenuSession.HandleGestureRightReleaseAsync` (hides synchronously, then `GestureReleaseFadeDelayMs`=180ms fade, then spatial resolution: center quick-switch / slot selection / empty dismiss). Move events already flow into `MenuSession.HandlePointerMoved(Vector relativePosition)` from `RadialMenuViewModel.OnMousePositionChanged` (via `_mouseTrackingService.ToRelative`), which stores `_lastMouseX/_lastMouseY` and does hit testing against the menu center. `SetMenuCenter` fixes the center. Menu visual state (preview/center label) is owned by `RadialMenuVisualStateCoordinator`; the window-level dim needs an observable the `RadialMenuWindow` can bind.

## Goals / Non-Goals

**Goals:**
- Real-time escape state (move-driven, StarPie `_lastEscapedState` model): cursor displacement from the menu center beyond 1.5 × wheel radius dims the menu as a cancel preview; re-entry restores it.
- Escape-state release cancels: hide menu, no selection, no quick-switch, no event leak.
- Configurable (enable toggle + radius multiplier, default 1.5) and gesture-path-only (hotkey menus unaffected).
- Pure-logic escape decision in `MenuSession` so existing `MenuSessionGestureTests` style tests cover it without the window.

**Non-Goals:**
- Changing hotkey release resolution (`HandleModifierRelease`) — untouched.
- Applying flick-out to submenus beyond the root ring radius check — radius is the current wheel radius regardless of submenu state.
- Separate visual treatment per renderer — the dim is a window-level opacity, orthogonal to the renderer contract.

## Decisions

### D1: Escape state owned by `MenuSession`, toggled from `HandlePointerMoved`

`MenuSession` is already the gesture state machine and owns the release handler, so the escaped flag lives there (`_isFlickOutEscaped`, observable through a `bool IsFlickOutEscaped` property). `HandlePointerMoved` computes `dist = hypot(_lastMouseX, _lastMouseY)` (relative coords are already center-relative) and flips the flag on cross/enter. The release handler reads the flag. No new service needed; the radius comes from the layout the session already uses.

- **Alternative rejected**: tracking in `RadialMenuViewModel` — it is the thin binding projection; the decision belongs in the session state machine next to the release handler.

### D2: Flick-out radius = `CalculateOptimalRadius(slotCount) × multiplier`

Reuse the existing `_slotLayoutEngine.CalculateOptimalRadius(_slotsPerPage)` (the same call the layout coordinator uses) so the escape radius tracks the actual wheel. Multiply by the configured multiplier (default 1.5). No hardcoded pixel value; matches the spec's "1.5 × current wheel radius" without a second source of truth.

- **Alternative rejected**: a fixed DIP constant — decouples from slot-count-dependent radius (4-slot vs 12-slot rings differ by ~2×) and breaks at high DPI.

### D3: Release cancels by flag capture before the hide, exactly like `inCenterZone`

`HandleGestureRightReleaseAsync` already captures spatial state (`inCenterZone`) before the synchronous hide. Add `bool escaped = _isFlickOutEscaped;` to the same capture block. When `escaped`, hide + return after the existing `GestureReleaseFadeDelayMs` (keeps the fade consistent with other releases) — no quick-switch, no `ExecuteSelectionAsync`. The caller already swallowed the release, so nothing leaks to the source app.

- The dim preview is a **hint**, not a hard gate: if the flag flips between capture and hide, the spatial path runs — acceptable because the flick-out radius (1.5×) is far outside any slot, so a just-escaped release almost never coincides with an intended selection.

### D4: Dim as a window-level observable + existing animation controller

Expose `IsFlickOutEscaped` (from `MenuSession`) through `RadialMenuViewModel` and bind it in `RadialMenuWindow` (root opacity or an overlay). Transitions use the existing animation path (`IAnimationController` / the same style as the dismiss fade, ~150-250ms) so the dim is a smooth preview, not a hard snap.

- **Alternative rejected**: routing through `RadialMenuVisualStateCoordinator.UpdateVisuals` — that coordinator manages preview/center content and cancels on every hover change; a continuous dim that only tracks escape state would fight its per-hover cancellation model.

### D5: Config in `ProfileSettings` with deferred refresh like other gesture settings

Add `GestureFlickOutCancelEnabled` (bool, default `true` — the gesture itself is opt-in via `EnableRightDragSummon`, so flick-out cancel is a safe default on) and `GestureFlickOutRadiusMultiplier` (double, default 1.5). Refresh via `RefreshGestureConfig()`/`ApplyPendingGestureConfig()` (same deferred-apply pattern as `_gestureDragThreshold`). When disabled, `_isFlickOutEscaped` stays false and release behaves exactly as today.

## Risks / Trade-offs

- **Escape flip racing the release** → capture the flag in the same pre-hide block as `inCenterZone`; residual window is sub-10ms and far outside slot geometry.
- **Dim fights hover feedback visually** → keep the dim subtle (e.g. opacity ~0.6) and only while escaped; re-entry undims via the same transition.
- **Per-move `hypot` cost** → trivial; `HandlePointerMoved` already does per-move hit testing.
- **Hotkey menu must not dim** → guard: `IsFlickOutEscaped` only updates when `InvocationSource == RightDragGesture` (the session already tracks `MenuInvocationSource`).

## Migration Plan

- New settings default on (`true`, multiplier 1.5); existing users get flick-out cancel automatically once gesture summon is enabled. Plain JSON fields deserialize to defaults when absent. No breaking change; hotkey path untouched.

## Open Questions

- Whether the dim should also blur/shrink (StarPie does a full visual "escape" fade) or only opacity — opacity only for now; visual richness can follow in the renderer direction without a spec change.
