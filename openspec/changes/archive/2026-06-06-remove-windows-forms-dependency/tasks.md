## 1. Setup (Prerequisites)

- [x] 1.1 Add `Hardcodet.Wpf.TaskbarIcon` NuGet package to `Pulsar.csproj`
- [x] 1.2 Define `PulsarNotificationIcon` enum in `Pulsar.Models` with values `Info`, `Warning`, `Error`, `None`
- [x] 1.3 Add `GetCursorPos`, `MonitorFromPoint`, `GetMonitorInfo` P/Invoke declarations and supporting structs (`POINT`, `MONITORINFO`, `MONITOR_DEFAULTTONEAREST`) to `PulsarNative.cs`

## 2. SendKeys → InputHelper (High ROI)

- [x] 2.1 Rewrite `WindowsSendKeysWriter.SendWait(string keys)` to call `InputHelper.SendText(keys)` instead of `SendKeys.SendWait(keys)`
- [x] 2.2 Change `WindowsSendKeysWriter.EscapeForSendKeys(string? input)` to return `input` unchanged (no-op, no SendKeys escaping needed)
- [x] 2.3 Audit and update `SendKeysInjectionExecutor` to verify it does not rely on `EscapeForSendKeys` for functional correctness
- [x] 2.4 Implement SendKeys-format parser in `SimpleCommandPlugin.SendKeysAsync` that maps `{ENTER}` → `VK_RETURN`, `{TAB}` → `VK_TAB`, `{ESC}` → `VK_ESCAPE`, modifier prefixes (`^`, `+`, `%`) to `InputHelper.SendKeyCombination()`, and plain text to `InputHelper.SendText()`
- [x] 2.5 Apply same parser to `SimpleCommandPlugin.Refactored.cs.SendKeysAsync` (or delete `.Refactored.cs` if it is no longer maintained)
- [x] 2.6 Remove `using System.Windows.Forms;` from `WindowsSendKeysWriter.cs`, `SimpleCommandPlugin.cs`, `SimpleCommandPlugin.Refactored.cs`

## 3. ToolTipIcon → PulsarNotificationIcon (High ROI)

- [x] 3.1 Change `ActionFeedback.Icon` property type from `ToolTipIcon` to `PulsarNotificationIcon`
- [x] 3.2 Update `ActionFeedbackService` — replace all 36 occurrences of `ToolTipIcon.Info`/`Warning`/`Error` with `PulsarNotificationIcon.Info`/`Warning`/`Error`
- [x] 3.3 Change `ITrayService.ShowNotification` signature: `Forms.ToolTipIcon` → `PulsarNotificationIcon`
- [x] 3.4 Update `TrayIconService.ShowNotification` to accept `PulsarNotificationIcon` and map to underlying tray icon type (for now, delay actual tray implementation to Phase 3; add a `// TODO: Map to Hardcodet BalloonIcon once TrayIconService is migrated`)
- [x] 3.5 Update `PluginRuntimeKernel.cs:316` — replace `ToolTipIcon.Error` with `PulsarNotificationIcon.Error`
- [x] 3.6 Update `PluginRegistryV2.cs:520,530` — replace `ToolTipIcon.Info`/`Warning` with `PulsarNotificationIcon.Info`/`Warning`
- [x] 3.7 Update `SlotStrategies.cs:97` — replace `feedback.Icon` usage (already compatible after ActionFeedback change; remove `using System.Windows.Forms` import)
- [x] 3.8 Run `dotnet build` to verify no remaining `ToolTipIcon` references

## 4. Cursor.Position → GetCursorPos (Medium ROI)

- [x] 4.1 Replace `Forms.Cursor.Position` with `PulsarNative.GetCursorPos` in `MouseTrackingService.GetGlobalCursorPosition()`
- [x] 4.2 Replace `Forms.Cursor.Position` with `PulsarNative.GetCursorPos` in `RadialMenuWindow.xaml.cs` (position-at-cursor logic)
- [x] 4.3 Replace `Forms.Cursor.Position` with `PulsarNative.GetCursorPos` in `SlotOrb.xaml.cs` (parallax animation)
- [x] 4.4 Replace `Control.MousePosition` + `Screen.FromPoint` + `Screen.WorkingArea` with `GetCursorPos` + `MonitorFromPoint` + `GetMonitorInfo` in `DialogService.PositionNearMouse()`
- [x] 4.5 Remove `using Forms = System.Windows.Forms;` from `RadialMenuWindow.xaml.cs`, `SlotOrb.xaml.cs`, `MouseTrackingService.cs`, `DialogService.cs`

## 5. Cleanup & Verification

- [x] 5.1 Remove `global using FormsButton = System.Windows.Forms.Button;` from `GlobalUsings.cs:16`
- [x] 5.2 Remove `using System.Windows.Forms; // [New] For ToolTipIcon` from `SlotStrategies.cs:13`
- [x] 5.3 Run `dotnet build` — verify zero `System.Windows.Forms` references remain in Pulsar project (only TrayIconService pending Phase 3)
- [x] 5.4 Run `dotnet test` — verify all 242 tests still pass, `ActionFeedbackServiceTests` still pass with new enum type

## 6. TrayIconService → Hardcodet (Capstone)

- [x] 6.1 Rewrite `TrayIconService` to use `Hardcodet.Wpf.TaskbarIcon.TaskbarIcon` instead of `Forms.NotifyIcon`
- [x] 6.2 Replace `TryLoadCustomIcon()` — load `.ico` from `pack://application:,,,/Pulsar.ico` using `TaskbarIcon.IconSource` (WPF-native `BitmapImage` or `BitmapDecoder`)
- [x] 6.3 Replace `BuildContextMenu()` — use WPF `ContextMenu` with `MenuItem` elements instead of `Forms.ContextMenuStrip` with `ToolStripMenuItem`
- [x] 6.4 Replace left-click handler — use `TaskbarIcon.TrayMouseDoubleClick` or routed event instead of `Forms.MouseEventArgs`/`MouseButtons.Left`
- [x] 6.5 Replace `ShowBalloonTip` — use `TaskbarIcon.ShowBalloonTip(title, message, icon)` with `BalloonIcon` mapping from `PulsarNotificationIcon`
- [x] 6.6 Update `Dispose()` — dispose `TaskbarIcon` instead of `NotifyIcon`
- [x] 6.7 Remove `using Forms = System.Windows.Forms;` from `TrayIconService.cs`
- [x] 6.8 Remove `using System.Windows.Forms;` from `ITrayService.cs` (already changed signature in task 3.3)

## 7. Build Configuration

- [x] 7.1 Remove `<UseWindowsForms>true</UseWindowsForms>` from `Pulsar.csproj:9`
- [x] 7.2 Remove `<UseWindowsForms>true</UseWindowsForms>` from `Pulsar.Simulator.csproj:9`
- [x] 7.3 Change `<PublishTrimmed>false</PublishTrimmed>` to `<PublishTrimmed>true</PublishTrimmed>` in `Pulsar.csproj:38`
- [x] 7.4 Update trimming comment (`Pulsar.csproj:36-37`) to document the new state: "Trimming enabled after removing System.Windows.Forms dependencies" or remove the comment entirely
- [x] 7.5 Run `dotnet build` — verify clean build with zero warnings (NETSDK1175 should be gone)
- [x] 7.6 Run `dotnet test` — verify all tests pass in Release configuration
- [ ] 7.7 Manual QA: launch Pulsar, verify tray icon appears, right-click menu works, balloon notification appears, PKI injection works, SimpleCommand sendkeys works, cursor-triggered radial menu positions correctly
