## Why

Pulsar is a WPF application that carries a `<UseWindowsForms>true</UseWindowsForms>` build dependency solely for 17 scattered references across 4 dependency chains. This blocks `<PublishTrimmed>true` (NETSDK1175), adds ~4MB of WinForms runtime assemblies to every build, creates cross-framework coupling (Forms.Timer vs DispatcherTimer, Forms enum types in model layer), and prevents the project from being a "pure WPF" application. The codebase already contains the replacement infrastructure (InputHelper.SendText, PulsarNative P/Invokes) — the remaining work is wiring.

## What Changes

- **Replace SendKeys.SendWait** with native `SendInput` (via existing `InputHelper`) across PKI plugin, SimpleCommandPlugin (2 variants), and WindowsSendKeysWriter. **BREAKING**: `EscapeForSendKeys()` on ISendKeysWriter becomes a no-op — callers that relied on SendKeys special-character escaping must be audited.
- **Replace `System.Windows.Forms.ToolTipIcon`** with a new `PulsarNotificationIcon` enum across all layers: data model (ActionFeedback), service interfaces (ITrayService, IActionFeedbackService), execution pipeline (PluginRuntimeKernel, PluginRegistryV2), and strategy layer (SlotStrategies).
- **Replace `Forms.Cursor.Position`** with `GetCursorPos` P/Invoke in MouseTrackingService, RadialMenuWindow.xaml.cs, SlotOrb.xaml.cs. Replace `Control.MousePosition` + `Screen.FromPoint` with `GetCursorPos` + `MonitorFromPoint` in DialogService.
- **Replace `Forms.NotifyIcon` + `Forms.ContextMenuStrip`** in TrayIconService with `Hardcodet.NotifyIcon.Wpf` (TaskbarIcon, native WPF ContextMenu, ShowBalloonTip).
- **Remove `global using FormsButton = System.Windows.Forms.Button`** from GlobalUsings.cs (dead alias, zero references).
- **Remove `<UseWindowsForms>true</UseWindowsForms>`** from Pulsar.csproj once all 17 references are eliminated. Enable `<PublishTrimmed>true</PublishTrimmed>`.

## Capabilities

### New Capabilities

- **`native-input-injection`**: Replace `SendKeys.SendWait` with `InputHelper.SendText`/`SendKeyCombination` across all consumers. Remove special-character escaping from ISendKeysWriter contract.
- **`notification-model`**: Introduce `PulsarNotificationIcon` enum replacing `System.Windows.Forms.ToolTipIcon` across model, interface, service, and execution pipeline layers.
- **`wpf-tray-service`**: Replace `Forms.NotifyIcon` + `Forms.ContextMenuStrip` with `Hardcodet.NotifyIcon.Wpf` (TaskbarIcon) for pure-WPF tray icon and context menu.
- **`native-cursor-api`**: Replace all `Forms.Cursor.Position` and `Control.MousePosition`/`Screen.FromPoint` calls with `GetCursorPos` and `MonitorFromPoint` P/Invoke.

### Modified Capabilities

_None — no existing specs to modify._

## Impact

- **Code**: 17 files modified across Services/, Plugins/Core/Pki/, Plugins/Extensions/BasicCommand/, Native/, ViewModels/Strategies/, Views/, Views/Controls/, Core/Plugin/Runtime/
- **Dependencies**: Add `Hardcodet.NotifyIcon.Wpf` NuGet package; remove implicit WinForms runtime assemblies
- **Interfaces**: `ITrayService.ShowNotification` signature changes from `Forms.ToolTipIcon` to `PulsarNotificationIcon`; `ActionFeedback.Icon` type changes; `ISendKeysWriter.EscapeForSendKeys` semantics change
- **Build**: Remove `<UseWindowsForms>true` from Pulsar.csproj and Pulsar.Simulator.csproj; enable `<PublishTrimmed>true`
- **Tests**: ActionFeedbackServiceTests may need `ToolTipIcon` → `PulsarNotificationIcon` reference update; no behavioral test changes expected
