## 1. Native Layer — FlashWindowEx P/Invoke

- [x] 1.1 Add `FLASHWINFO` struct to `Pulsar/Pulsar/Native/PulsarNative.cs` with fields: `cbSize`, `hwnd`, `dwFlags`, `uCount`, `dwTimeout`
- [x] 1.2 Add `FLASHW_CAPTION` (0x1), `FLASHW_TRAY` (0x2), `FLASHW_ALL` constants to `PulsarNative.cs`
- [x] 1.3 Add `FlashWindowEx` P/Invoke declaration to `PulsarNative.cs`
- [x] 1.4 Build and verify no compilation errors in `PulsarNative.cs`

## 2. FlashWindowEx — Shared Activation Path Integration

- [x] 2.1 Add optional `flashAfterActivation` parameter (default `true`) to `WindowActivator.ActivateWindow()` method signature
- [x] 2.2 In `WindowActivator.ActivateWindow()`, after successful `SetForegroundWindow`, call `FlashWindowEx` with `FLASHW_CAPTION | FLASHW_TRAY`, `uCount=3`, `dwTimeout=0`
- [x] 2.3 Skip flash when activation fails (window invalid, already foreground, or SetForegroundWindow returns false)
- [x] 2.4 Build and verify `WindowActivator.cs` compiles

## 3. Launch Toast — WinSwitcherPlugin Integration

- [x] 3.1 Add `ITrayService` dependency injection to `WinSwitcherPlugin.Initialize()` via `IServiceProvider`
- [x] 3.2 In `SmartSwitchAsync()`, before calling `LaunchApplicationAsync()`, call `_trayService.ShowNotification("Launching", $"Starting {processName}...", PulsarNotificationIcon.Info)`
- [x] 3.3 Guard toast with null check on `_trayService` to avoid crash if service is unavailable
- [x] 3.4 Build and verify `WinSwitcherPlugin.cs` compiles

## 4. Sub-Radial Window Titles — ViewModel Layer

- [x] 4.1 Locate sub-menu slot construction in `RadialMenuViewModel` (method that builds `GroupedSlots` or sub-menu slot list)
- [x] 4.2 For each sub-menu slot representing a window: set `SlotViewModel.Label` to `window.Title` (truncated to 40 chars with ellipsis if longer)
- [x] 4.3 Fall back to process name when window title is null/empty/whitespace
- [x] 4.4 Verify no binding break: `Label` property in `SlotViewModel` is already data-bound to slot visual text in XAML
- [x] 4.5 Build and verify radial menu Window shows correct sub-menu labels

## 5. Same-Monitor Preference — Selection Engine

- [x] 5.1 Add nullable `PreferredMonitorRect` property (`RECT?`) to `WindowSelectionRequest` class
- [x] 5.2 In `WindowSelectionEngine.SelectTargetWindow()`, after existing `OrderByDescending` chain, add `ThenByDescending` that checks if `GetWindowRect(w.Handle)` intersects `PreferredMonitorRect` (only when `PreferredMonitorRect` is not null)
- [x] 5.3 Ensure intersection check handles partial overlap (window spanning two monitors matches both)
- [x] 5.4 Set `PreferredMonitorRect` in grouped root-slot selection calls: use `MonitorFromPoint(GetCursorPos, MONITOR_DEFAULTTONEAREST)` + `GetMonitorInfo` to get cursor monitor's `rcWork`
- [x] 5.5 Leave `PreferredMonitorRect` as null for QuickSwitch and plugin-driven switch paths (per design)
- [x] 5.6 Build and verify `WindowSelectionEngine.cs` and `WindowSelectionRequest.cs` compile

## 6. Validation & Verification

- [x] 6.1 Run `dotnet build Pulsar/Pulsar/Pulsar.csproj` — confirm zero compilation errors
- [x] 6.2 Run existing tests: `dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj` — confirm all tests pass
- [ ] 6.3 Manual smoke test: trigger QuickSwitch (Ctrl+Q) — verify no regression in switching behavior
- [ ] 6.4 Manual smoke test: open sub-menu for multi-window app (e.g., Chrome with 3+ windows) — verify window titles appear as slot labels
- [ ] 6.5 Manual smoke test: create a SmartSwitch slot with fallback path, trigger when app not running — verify "Launching..." toast appears
- [ ] 6.6 Manual smoke test: switch to a window on a different monitor — verify target window taskbar button flashes
