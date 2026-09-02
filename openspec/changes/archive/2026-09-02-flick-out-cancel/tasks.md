## 1. Session escape state

- [x] 1.1 Add `bool IsFlickOutEscaped` (observable) to `MenuSession` and initialize false; verify existing `MenuSessionTests` still pass
- [x] 1.2 Add `GestureFlickOutCancelEnabled` (bool, default true) and `GestureFlickOutRadiusMultiplier` (double, default 1.5) to `ProfileSettings` in `Models/ProfilesConfig.cs`; verify `ProfilesConfigDefaultsTests` covers the new defaults

## 2. Escape tracking (move-driven)

- [x] 2.1 In `MenuSession.HandlePointerMoved`, when `InvocationSource == RightDragGesture` and flick-out enabled, compute `dist = hypot(_lastMouseX, _lastMouseY)` against `_slotLayoutEngine.CalculateOptimalRadius(_slotsPerPage) × multiplier` and set/clear `IsFlickOutEscaped` on cross/enter (enter = drop back within radius)
- [x] 2.2 Verify escape state never updates for hotkey-summoned menus (guard on `InvocationSource`) and never fires when flick-out is disabled
- [x] 2.3 Add unit tests in `MenuSessionGestureTests`: enter escape on flick-out, clear on re-entry, no escape for hotkey menu, no escape when disabled

## 3. Escape release resolution

- [x] 3.1 In `MenuSession.HandleGestureRightReleaseAsync`, capture `bool escaped = IsFlickOutEscaped;` in the same pre-hide block as `inCenterZone`
- [x] 3.2 When `escaped`, after the existing `GestureReleaseFadeDelayMs`, hide and return — no quick-switch, no `ExecuteSelectionAsync`; verify release is never delivered to the source app
- [x] 3.3 Add unit tests: flick-out release cancels without action; non-escape release still resolves by spatial position

## 4. Visual dim

- [x] 4.1 Expose `IsFlickOutEscaped` through `RadialMenuViewModel` and bind it in `RadialMenuWindow` (root opacity or overlay) using the existing animation controller (150-250ms dim/undim transitions)
- [x] 4.2 Verify `dotnet build Pulsar/Pulsar/Pulsar.csproj` succeeds (0 new errors) and the dim never applies to hotkey menus

## 5. Tests & verification

- [x] 5.1 Run `dotnet test` — full suite green (no regressions in `MenuSessionGestureTests`/`RightDragGestureLeakTests`)
- [x] 5.2 Manual QA: flick-out dims menu + release cancels; re-entry undims + release selects; hotkey menu unaffected; both themes — requires human to run
