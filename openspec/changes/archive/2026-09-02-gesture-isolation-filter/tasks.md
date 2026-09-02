## 1. Native facts adapter

- [x] 1.1 Add `ForegroundWindowFacts` record (ClassName, ProcessName, WindowRect, MonitorBounds) in `Models/`
- [x] 1.2 Add `IGestureIsolationNative` interface in `Services/Interfaces/` (GetForegroundWindowFacts, IsFullscreenShellClass)
- [x] 1.3 Implement `GestureIsolationNative` in `Services/` using `PulsarNative.GetForegroundWindow` + `GetClassName` + process-name + `GetWindowRect` + cursor-monitor bounds (pattern: `WindowsFocusNativeAdapter`); verify no P/Invoke leaks into decision logic

## 2. Config surface

- [x] 2.1 Add `GestureIsolationEnabled` (bool, default false), `GestureIsolationMode` (enum Allowlist/Blocklist), `GestureIsolationProcesses` (List<string>), `GestureIsolationBlockFullscreen` (bool, default true) to `ProfileSettings` in `Models/ProfilesConfig.cs`
- [x] 2.2 Add `GestureIsolationMode` enum (Allowlist/Blocklist, JSON string converter) in `Models/` and verify `ProfilesConfigDefaultsTests` covers the new defaults

## 3. Isolation decision logic

- [x] 3.1 Add `IGestureIsolationService` in `Services/Interfaces/` with `bool IsGestureAllowed(ForegroundWindowFacts facts)` (or facts-less overload reading foreground internally)
- [x] 3.2 Implement `GestureIsolationService` in `Services/`: disabled ⇒ true; fullscreen-block short-circuit (skip `Progman`/`WorkerW`/`Shell_TrayWnd`); then allow-list/block-list match by case-insensitive process name
- [x] 3.3 Add unit tests `GestureIsolationServiceTests`: disabled→allow, fullscreen→deny, shell-class bypass, allow-list hit/miss, block-list hit/miss, empty-list semantics, case-insensitivity

## 4. Gesture pipeline wiring

- [x] 4.1 Inject `IGestureIsolationService` into `RadialMenuViewModel` constructor (optional param to keep existing tests compiling) and cache settings in `RefreshGestureConfig()` with deferred apply in `ApplyPendingGestureConfig()`
- [x] 4.2 In `FeedRightDragGesture`, before `_gestureDetector.OnRightDown` and the no-modifier pending-swallow branch: when isolation filter enabled and denied, return `false` without touching detector/pending state (spec: denied never enters state machine)
- [x] 4.3 Verify `dotnet build Pulsar/Pulsar/Pulsar.csproj` succeeds (0 new errors)

## 5. Settings UI

- [x] 5.1 Add gesture-isolation section to the gesture settings UI (enable toggle, isolation mode picker, process list editor, fullscreen block toggle) using localized strings (add `Settings.Gesture.*` keys to `Strings.resx` EN + `Strings.zh-CN.resx`)
- [x] 5.2 Bind to `SettingsViewModel` via `ConfigEditSession`/`RebuildCache` pattern and verify manual save applies without reverting `Profiles.json`

## 6. Tests & verification

- [x] 6.1 Add `RightDragGestureDetectorTests`/`MenuSessionGestureTests` cases: denied by isolation passes click through; allowed proceeds to summon/threshold; no detector state change on denial
- [x] 6.2 Run `dotnet test` — full suite green (no regressions in `RightDragGestureLeakTests`)
- [ ] 6.3 Manual QA: fullscreen app + modifier+right-click passes through; allow-list/block-list behavior; both themes — requires human to run
