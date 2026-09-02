# fix-right-drag-release-passthrough — Tasks

## 1. Threshold state machine in `RightDragGestureDetector` (pure, test-first)

- [x] 1.1 Add `RightDragGestureDecision.SubThresholdRelease` to the enum
- [x] 1.2 Add `bool FeedDisplacement(double dx, double dy)` to `RightDragGestureDetector` — returns `true` when the threshold is crossed for the first time this press; track `double thresholdSquared` (from `GestureDragThreshold`)
- [x] 1.3 Change `OnRightDown` to enter `WaitingForThreshold` (pressed, not summoned) instead of immediately `IsSummoned = true`
- [x] 1.4 Change `OnRightUp` to return `SubThresholdRelease` when pressed-but-not-summoned, `GestureRelease` when summoned, `None` otherwise
- [x] 1.5 Extend `RightDragGestureDetectorTests`: Immediate-mode default (summon on down), OnThreshold crossing/re-crossing, sub-threshold → SubThresholdRelease, reset mid-gesture preserves state (see 3.2), replayed-release determinism

## 2. Hook layer: move events + replay with recursion suppression

- [x] 2.1 Add `WM_MOUSEMOVE` handling to `GlobalMouseHook`: raise `OnMouseMove` with `MSLLHOOKSTRUCT.pt` (only when subscribed and not paused) — needed by `OnThreshold`
- [x] 2.2 Add `GlobalMouseEventArgs.Replayed` flag (or an internal ignore-next mechanism) and expose `GlobalMouseHook.ReplayRightClick()`: sets `_ignoreNextButtonDown`/`_ignoreNextButtonUp`, then `mouse_event(MOUSEEVENTF_RIGHTDOWN)` + `MOUSEEVENTF_RIGHTUP`
- [x] 2.3 In `HookCallback`, honor `_ignoreNextButtonDown/Up` — consume one, return `CallNextHookEx` without raising events
- [x] 2.4 Expose `ReplayRightClick()` on `IGlobalMouseService` (delegating to hook); surface `OnMouseMove` on `IGlobalMouseService`
- [x] 2.5 Add hook tests: replay produces down+up, ignore-next consumed once, no loop, move event raised with correct coords

## 3. ViewModel wiring: `FeedRightDragGesture`

- [x] 3.1 Add `GestureSummonMode` and `GestureDragThreshold` reads in `RefreshGestureConfig` (D5); only `Reset()` when `!_gestureDetector.IsPressed` (D3)
- [x] 3.2 Defer applying pending gesture config until gesture ends: store `_pendingGestureConfig`, apply on next release/pass-through `Up`
- [x] 3.3 On right-button down: keep summoning immediately for `Immediate`; for `OnThreshold` don't summon yet (detector stays `WaitingForThreshold`)
- [x] 3.4 Feed `OnMouseMove` → `FeedDisplacement`; on first crossing, summon the menu (once) at the down position
- [x] 3.5 On right-button up: handle `SubThresholdRelease` by calling `ReplayRightClick()` and swallowing; handle `GestureRelease` as today
- [x] 3.6 Add visible-menu guard: when `_session.IsVisible` and detector returns `None` for an `Up`, still swallow and route to `HandleGestureRightReleaseAsync` (D3)
- [x] 3.7 Dispatch summon/release at `DispatcherPriority.Input` via `Application.Current.Dispatcher.InvokeAsync(action, DispatcherPriority.Input)` (D4)

## 4. MenuSession release timing

- [x] 4.1 In `HandleGestureRightReleaseAsync`, set `IsVisible = false` synchronously at the top (after capturing hit-test state), before any `await`
- [x] 4.2 In the loading-release quick-switch branch, also hide synchronously before quick-switching
- [x] 4.3 Update `MenuSessionGestureTests` / `MenuSessionTests` for the new hide-timing (menu hidden on release, not after action)

## 5. Settings & localization

- [x] 5.1 Add `GestureSummonMode SummonMode` (enum `Immediate`/`OnThreshold`, default `Immediate`) and `double GestureDragThreshold = 25.0` to `ProfileSettings` (camelCase persistence)
- [x] 5.2 Add `Settings.General.GestureSummonMode` and `Settings.General.GestureDragThreshold` to `Strings.resx` (EN) and `Strings.zh-CN.resx` (ZH)
- [x] 5.3 Add settings UI in `SettingsGeneralPage.xaml` beside `EnableRightDragSummon`: summon-mode selector (only when gesture enabled) + drag-threshold slider (only visible for `OnThreshold`)
- [x] 5.4 Wire the new settings through `SettingsViewModel` / `SettingsViewModel.General` observable properties; ensure `RightDragModifiersConflict` still disables the gesture

## 6. Tests & verification

- [x] 6.1 Unit tests green: `RightDragGestureDetectorTests`, hook replay tests, `MenuSessionGestureTests`, `SettingsViewModel` settings round-trip
- [x] 6.2 Run `dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj` — all pass (baseline 380+)
- [x] 6.3 Build `Pulsar/Pulsar/Pulsar.csproj` — 0 errors
- [x] 6.4 Manual QA (requires human): plain modifier+click shows native context menu (OnThreshold); drag summons once; release over slot executes without native menu; config save mid-gesture does not leak release; both themes


