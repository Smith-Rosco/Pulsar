## ADDED Requirements

### Requirement: IFocusManager SHALL be the sole authority for foreground window activation
All code paths that need to bring a window to the foreground SHALL call `IFocusManager.ActivateWindowAsync()` or `IFocusManager.ReleaseAsync()`. Direct calls to `PulsarNative.SetForegroundWindow` are prohibited outside of `IFocusNativeAdapter`.

#### Scenario: Window switching uses FocusManager
- **WHEN** any window switching flow (grouped slot, plugin, Quick Switch) needs to activate a target window
- **THEN** the flow SHALL call `IFocusManager.ActivateWindowAsync(handle)` rather than `PulsarNative.SetForegroundWindow` directly

#### Scenario: Plugin needs to activate a target window
- **WHEN** a plugin such as VbaRunner or BookmarkletRunner needs to bring a target window to foreground
- **THEN** the plugin SHALL inject and use `IFocusManager.ActivateWindowAsync()` instead of calling `PulsarNative.SetForegroundWindow`

#### Scenario: Direct SetForegroundWindow call is rejected
- **WHEN** code outside `IFocusNativeAdapter` attempts to call `PulsarNative.SetForegroundWindow`
- **THEN** the method SHALL be marked `[Obsolete]` and the call SHALL produce a compile-time warning

### Requirement: IFocusManager SHALL capture context before Pulsar activates its own window
Before the radial menu window is brought to foreground, the FocusManager SHALL capture the current foreground window handle, process name, and process ID as an immutable snapshot.

#### Scenario: Context captured before menu activation
- **WHEN** Pulsar is invoked via hotkey
- **THEN** `IFocusManager.Capture()` SHALL record the current foreground window BEFORE `IFocusManager.ActivateMenu()` is called

#### Scenario: Pulsar self-window is excluded from capture
- **WHEN** the current foreground window belongs to the Pulsar process
- **THEN** `IFocusManager.Capture()` SHALL return a failed capture result and SHALL NOT record Pulsar's own window as the capture target

### Requirement: IFocusManager SHALL activate the Pulsar menu window
When the radial menu needs to appear, the FocusManager SHALL bring the menu window to the foreground, make it interactable, and manage its Topmost state.

#### Scenario: Menu window is activated
- **WHEN** `IFocusManager.ActivateMenu(window)` is called with the radial menu window
- **THEN** the window SHALL be made Topmost, receive `Activate()` and `Focus()` calls, and have `IsHitTestVisible` set to true

#### Scenario: Menu window is deactivated on dismiss
- **WHEN** the radial menu is dismissed and `IFocusManager.ReleaseAsync()` completes
- **THEN** the menu window SHALL have `IsHitTestVisible` set to false and its Topmost state SHALL be managed to not occlude the target window

### Requirement: IFocusManager SHALL release focus on menu dismiss
When the radial menu is dismissed, the FocusManager SHALL restore focus to the previously captured window or a specified target window, depending on the configured restore mode.

#### Scenario: Restore to previous window on normal dismiss
- **WHEN** the user dismisses the radial menu without executing any action (RestorePrevious mode)
- **THEN** `IFocusManager.ReleaseAsync()` SHALL activate the window that was captured before the menu appeared

#### Scenario: Restore to specific target window after plugin execution
- **WHEN** a plugin sets the restore mode to RestoreTarget with a specific window handle
- **THEN** `IFocusManager.ReleaseAsync()` SHALL activate the specified target window instead of the previously captured window

#### Scenario: No restore after Quick Switch
- **WHEN** the restore mode is NoRestore (set by Quick Switch)
- **THEN** `IFocusManager.ReleaseAsync()` SHALL NOT activate any window and SHALL return immediately

#### Scenario: Release is deferred until after menu animation
- **WHEN** `IFocusManager.ReleaseAsync()` is called as part of the menu dismiss flow
- **THEN** the call SHALL only be made after the menu's fade-out animation has completed, not at the start of the dismiss

### Requirement: IFocusManager SHALL support Quick Switch between two recent windows
The FocusManager SHALL enable switching between the two most recently active windows using a history-based resolution mechanism.

#### Scenario: Quick Switch toggles between two windows
- **WHEN** `IFocusManager.QuickSwitchAsync()` is called and a valid switch pair exists
- **THEN** focus SHALL toggle to the other window in the pair and the restore mode SHALL be set to NoRestore

#### Scenario: Quick Switch with no valid history
- **WHEN** `IFocusManager.QuickSwitchAsync()` is called but no valid previous window exists in history
- **THEN** the call SHALL return a failed result and SHALL NOT change the foreground window

### Requirement: IFocusManager SHALL use AttachThreadInput for cross-thread activation
The default activation path SHALL use `AttachThreadInput` to temporarily attach Pulsar's input queue to the target window's thread before calling `SetForegroundWindow`, ensuring reliable cross-thread foreground activation.

#### Scenario: Target window belongs to a different thread
- **WHEN** `IFocusManager.ActivateWindowAsync()` targets a window on a different thread than Pulsar
- **THEN** FocusManager SHALL call `AttachThreadInput(currentThread, targetThread, true)` before `SetForegroundWindow` and `AttachThreadInput(currentThread, targetThread, false)` after

#### Scenario: AttachThreadInput succeeds and activation works
- **WHEN** `AttachThreadInput` succeeds and `SetForegroundWindow` returns true
- **THEN** the activation result SHALL indicate success

#### Scenario: AttachThreadInput fails
- **WHEN** `AttachThreadInput` fails (returns false)
- **THEN** FocusManager SHALL fall back to `AllowSetForegroundWindow` + foreground lock timeout bypass and retry activation

### Requirement: IFocusManager SHALL be the single DI-registered focus service
The FocusManager SHALL be registered as a singleton in the DI container and SHALL be injectable by any service or plugin that requires focus operations.

#### Scenario: FocusManager is resolved from DI
- **WHEN** any service or plugin requests `IFocusManager` via constructor injection
- **THEN** the DI container SHALL provide the singleton `FocusManager` instance

#### Scenario: IFocusNativeAdapter is a separate DI registration
- **WHEN** `IFocusManager` is constructed
- **THEN** its `IFocusNativeAdapter` dependency SHALL be resolved from DI as a separate singleton, enabling independent mocking in tests
