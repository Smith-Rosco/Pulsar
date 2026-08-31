# fix-right-drag-release-passthrough — Proposal

## Why

The right-click summon gesture (`Ctrl/Shift` + right-button drag) has two UX defects users report versus the hotkey path:

1. **Release passthrough to the source application.** Releasing the right button after a gesture can send an unexpected right-click to the foreground app (native context menu / paste menu appears). This is both a correctness bug and a muscle-memory killer.
2. **Less fluid than hotkey invocation.** The menu summons/closes with visible latency compared to the hotkey path.

Root causes (see `Docs/roadmap/RIGHT_DRAG_GESTURE_ANALYSIS.md`):

- **A. No sub-threshold click replay.** Pulsar's `RightDragGestureDetector` decides ownership at right-button *down* purely from modifiers, then swallows the whole gesture. A "plain right-click while holding the modifier" (or a release before any real drag) never reaches the source app — and conversely a drag without the modifier fully passes through. StarPie solves this with a displacement threshold: it swallows the down, waits for `DragThreshold` (25px), and on a sub-threshold release **replays** a synthetic right-click to the source app via `mouse_event` (with `_ignoreNextButtonDown/Up` to avoid recursion).
- **B. Release race from `Reset()`.** `RefreshGestureConfig()` calls `_gestureDetector.Reset()` on config change, clearing `IsSummoned` while a gesture is in flight — so the right-button *up* resolves to `None` and leaks through to the source app.
- **C. Dispatch timing.** The gesture path routes through `InvokeOnUi` at default `DispatcherPriority.Normal` plus an `Opacity=0` → fade-in first frame; the close path awaits `HandleGestureRightReleaseAsync` before hiding. StarPie computes displacement on the hook thread and dispatches at `DispatcherPriority.Input`, and hides synchronously on release.

This change hardens the right-drag gesture so releases never leak, and makes summon/close feel as immediate as the hotkey path.

## What Changes

- **Displacement threshold + click replay** (`right-drag-threshold-replay` spec):
  - `RightDragGestureDetector` gains a displacement-threshold state machine (Down → `WaitingForThreshold` → activated menu or sub-threshold release).
  - On sub-threshold release, the system replays a synthetic right-button down/up to the source application, preserving native context menus, with recursion suppression.
  - New settings: `GestureSummonMode` (`Immediate` / `OnThreshold`, default `Immediate` to preserve current behavior) and `GestureDragThreshold` (default 25px, aligning with StarPie).
- **Release-race hardening** (`right-drag-release-race` spec):
  - `RefreshGestureConfig` must not clear in-flight gesture state; config application defers until the gesture ends, and a visible-menu guard prevents release leakage regardless.
- **Dispatch & close timing** (`right-drag-dispatch-timing` spec):
  - Gesture summons dispatch at `DispatcherPriority.Input`; release hides the menu synchronously before/as it executes the selection.

## Capabilities

### New Capabilities

- `right-drag-threshold-replay`: Displacement-threshold gesture activation plus synthetic right-click replay for sub-threshold releases (StarPie `DragThreshold` + `ReplayTriggerClick` model).
- `right-drag-release-race`: In-flight gesture state protection from config refresh `Reset()`, and a visible-menu release guard so a right-button up can never leak during an open menu.
- `right-drag-dispatch-timing`: Input-priority gesture dispatch and synchronous hide-on-release for hotkey-parity feel.

### Modified Capabilities

- `global-mouse-interception`: The low-level hook gains an ignore-next-event mechanism so replayed clicks are not re-intercepted.

## Impact

- **Affected code**:
  - `ViewModels/RightDragGestureDetector.cs` — threshold state machine
  - `ViewModels/RadialMenuViewModel.cs` — `FeedRightDragGesture`, dispatch priority, replay call
  - `ViewModels/MenuSession.cs` — `HandleGestureRightReleaseAsync` hide timing, loading-release path
  - `Native/GlobalMouseHook.cs` / `GlobalMouseEventArgs` — replay + ignore-next support
  - `Models/ProfilesConfig.cs` — `GestureSummonMode`, `GestureDragThreshold` settings
  - `Views/Pages/SettingsGeneralPage.xaml` — new settings UI (threshold + summon mode)
  - `Services/Interfaces/IHotkeyService.cs` — reused for modifier state (no change)
  - `Resources/Strings.resx` / `Strings.zh-CN.resx` — new settings keys
- **Dependencies**: Reuses `IUiDispatcher`, existing modifier tracking, `GlobalMouseHook`; no plugin-API or `Profiles.json` schema breaking changes (new settings are additive).
- **No breaking changes**: Default `GestureSummonMode=Immediate` preserves today's summon-on-down behavior; existing `RightDragGestureDetectorTests` / `MenuSessionGestureTests` must keep passing with the new default.
