## Context

Pulsar currently enables `<UseWindowsForms>true</UseWindowsForms>` in `Pulsar.csproj:9` to support 17 scattered `System.Windows.Forms` references across 4 dependency chains:

| Chain | Files | API Used | Replaceable With |
|-------|-------|----------|------------------|
| SendKeys | 3 | `SendKeys.SendWait()` | Existing `InputHelper.SendText()` / `SendKeyCombination()` |
| ToolTipIcon | 6 | `Forms.ToolTipIcon` in model/service/pipeline | New `PulsarNotificationIcon` enum |
| Cursor.Position | 4 | `Cursor.Position`, `Control.MousePosition`, `Screen.FromPoint` | `GetCursorPos` + `MonitorFromPoint` P/Invoke |
| TrayIcon | 1 (+ interface) | `NotifyIcon`, `ContextMenuStrip`, `MouseButtons` | `Hardcodet.NotifyIcon.Wpf` (TaskbarIcon) |

A dead `global using FormsButton = System.Windows.Forms.Button` in `GlobalUsings.cs:16` exists with zero references.

The .csproj (line 36-38) explicitly links `<PublishTrimmed>false</PublishTrimmed>` to these dependencies: *"Trimming currently incompatible with WinForms usage (NETSDK1175). Keep disabled until we remove System.Windows.Forms dependencies (NotifyIcon/SendKeys, etc)."*

## Goals / Non-Goals

**Goals:**
- Eliminate ALL `System.Windows.Forms` references from the codebase (17 files → 0)
- Remove `<UseWindowsForms>true</UseWindowsForms>` from both `Pulsar.csproj` and `Pulsar.Simulator.csproj`
- Maintain 100% behavioral parity — tray icon, PKI injection, SimpleCommand sendkeys, cursor tracking, dialog positioning must work identically
- Enable `<PublishTrimmed>true</PublishTrimmed>` after removal
- Remove dead `FormsButton` global using

**Non-Goals:**
- Changing plugin execution semantics or action feedback logic
- Adding new notification features or tray menu items
- Replacing `System.Drawing.Icon` (used for `.ico` loading — not WinForms-specific, compatible with Hardcodet)
- Refactoring ITrayService beyond the signature change required for ToolTipIcon → PulsarNotificationIcon
- Replacing `System.Media.SystemSounds` (this is NOT WinForms — it's `System.dll`)

## Decisions

### D1: SendKeys → InputHelper.SendText (not InputSimulator NuGet)

**Choice**: Replace `SendKeys.SendWait()` with existing `InputHelper.SendText()` for pure-text injection and `InputHelper.SendKeyCombination()` for key combinations. For SimpleCommandPlugin which accepts SendKeys-format strings (`{ENTER}`, `^+{F4}`), implement a minimal SendKeys→SendInput parser in the plugin itself.

**Rationale**: `InputHelper` already has complete `SendInput` P/Invoke with `INPUT`/`KEYBDINPUT` structures (`Native/InputHelper.cs`). Adding an `InputSimulator` NuGet would be a new dependency for functionality already 90% implemented. The remaining 10% is a tokenizer that maps `{ENTER}` → `VK_RETURN`, `^c` → `VK_CONTROL + 'C'`.

**Alternatives considered**:
- InputSimulator NuGet: More complete SendKeys emulation but adds a dependency for a marginal feature. Rejected — project already has native infrastructure.
- Keep WindowsSendKeysWriter using SendInput under the hood: Would still need a SendKeys parser in the implementation. Same effort, no benefit.

### D2: ToolTipIcon → PulsarNotificationIcon enum

**Choice**: Define a new `PulsarNotificationIcon` enum in `Core/Plugin/` (or `Models/`) with values `Info`, `Warning`, `Error`, `None`. Replace all `System.Windows.Forms.ToolTipIcon` references. Update `ITrayService.ShowNotification` signature. Map to Hardcodet's `BalloonIcon` in the TrayIconService implementation.

**Rationale**: `ToolTipIcon` is the most pervasive WinForms type — it leaks from UI layer into model (`ActionFeedback`), execution pipeline (`PluginRuntimeKernel`, `PluginRegistryV2`), and strategies (`SlotStrategies`). Introducing a project-owned enum restores architectural layering. It also decouples the notification system from the tray implementation — if TrayIconService changes later, only the implementation needs updating, not callers.

**Alternatives considered**:
- Use Hardcodet's `BalloonIcon` enum directly: Would couple ActionFeedback model to Hardcodet's types. Rejected — model should not depend on UI library.
- Keep ToolTipIcon as an internal detail: Doesn't achieve cleanliness goal. Rejected.

### D3: Cursor.Position → GetCursorPos P/Invoke

**Choice**: Add `GetCursorPos` to `PulsarNative.cs`. Replace `Forms.Cursor.Position` with `GetCursorPos(out POINT)` calls. Replace `Control.MousePosition` + `Screen.FromPoint` + `Screen.WorkingArea` in DialogService with `GetCursorPos` + `MonitorFromPoint` + `GetMonitorInfo`.

**Rationale**: `Cursor.Position` is a thin wrapper around `GetCursorPos` — replacing it actually removes overhead (no interop to WinForms). The `Screen.FromPoint` replacement is the only non-trivial piece, requiring 3 P/Invoke declarations and a struct. But these are standard Win32 calls with well-known signatures.

**Alternatives considered**:
- Introduce `ICursorService` abstraction: Over-engineering for 4 call sites, each using cursor coordinates differently. Rejected — a shared native helper method is sufficient.
- Use .NET's `Mouse.GetPosition()`: Requires a `UIElement` reference, not available in service-layer code. Rejected.

### D4: NotifyIcon → Hardcodet.NotifyIcon.Wpf

**Choice**: Replace `TrayIconService` implementation to use `Hardcodet.NotifyIcon.Wpf` (specifically `TaskbarIcon`, WPF `ContextMenu`, `ShowBalloonTip`). Add `Hardcodet.Wpf.TaskbarIcon` NuGet package (actively maintained fork of `Hardcodet.NotifyIcon.Wpf`, 1.0+ million downloads, .NET 8 compatible).

**Rationale**: Hardcodet is the de facto standard for WPF tray icons. It handles DPI awareness, Windows 11 adaptations, icon reconstruction, and native WPF context menus. Hand-rolling Shell_NotifyIconW would require ~300 lines of message pump boilerplate and balloon tip state management. The NuGet dependency is well-maintained and adds negligible size.

**Alternatives considered**:
- HwndSource + Shell_NotifyIconW: Zero new dependencies but ~300 lines of complex state management. Rejected — not worth the maintenance burden for UI infrastructure.
- Keep WinForms NotifyIcon, just isolate it: Doesn't achieve the goal of removing `<UseWindowsForms>true`. Rejected.

### D5: Implementation Order

**Choice**: Implement in 3 phases, ordered by ROI (cleanliness per effort unit):
1. **Phase 1** (independent, parallelizable): SendKeys + ToolTipIcon + Cursor → covers 13 of 17 files
2. **Phase 2** (cleanup): Remove dead FormsButton alias, verify build + tests
3. **Phase 3** (capstone): TrayIcon migration → removes remaining 2 files, enables removing `<UseWindowsForms>true`

**Rationale**: Phase 1 items are independent (no shared files) and can be done in any order. Completing them first derisks the project — even if TrayIcon migration hits unexpected issues, 76% of WinForms references are already eliminated.

## Risks / Trade-offs

- **Hardcodet balloon tip behavior**: `ShowBalloonTip` API differs from `NotifyIcon.ShowBalloonTip`. Must verify balloon appears, auto-closes after timeout, and click event fires correctly. → Mitigation: Manual QA on Windows 10 and 11.
- **SendInput cross-session injection**: `SendKeys.SendWait` starts a terminal services thread for cross-session reliability. `SendInput` does not. This only matters if Pulsar needs to inject into elevated (UAC) windows. → Mitigation: Pulsar operates at user-session level; elevated window injection is not a current use case. Document limitation.
- **MonitorFromPoint + GetMonitorInfo complexity**: ~40 lines of new P/Invoke in PulsarNative.cs for DialogService. → Mitigation: Well-documented Win32 API, standard struct layout. Minimal risk.
- **Hardcodet package selection**: The original `Hardcodet.NotifyIcon.Wpf` is not maintained for .NET 8+. Must use `Hardcodet.Wpf.TaskbarIcon` fork. → Mitigation: Verify package compatibility before committing.
- **EscapeForSendKeys semantics change**: Callers relying on `ISendKeysWriter.EscapeForSendKeys` for SendKeys-specific escaping break silently if the new implementation becomes a no-op. → Mitigation: Audit all callers. Currently only `SendKeysInjectionExecutor` calls it; verify no other consumers.

## Open Questions

- Should `PulsarNotificationIcon` live in `Core/Plugin/` (as it's consumed by the execution pipeline) or `Models/` (as it's a data model)? Current leaning: `Models/` for value types, `Core/Plugin/` only for plugin contract types.
- Confirm `Hardcodet.Wpf.TaskbarIcon` vs `Hardcodet.NotifyIcon.Wpf` fork status — which builds for .NET 8 without warnings?
