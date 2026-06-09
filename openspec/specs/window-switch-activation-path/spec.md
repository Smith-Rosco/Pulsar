# window-switch-activation-path

## Purpose
Define the shared activation path for foreground window switching so all app-switching entry points use a consistent, reliable mechanism.
## Requirements
### Requirement: Window activation SHALL use a shared activation path
Pulsar SHALL activate a selected window through a shared activation path so app-switching entry points do not implement independent foreground-switch logic, and all user-facing foreground switching flows that activate a concrete target window SHALL delegate to that shared path. The shared activation path SHALL be implemented by `IFocusManager.ActivateWindowAsync()`, which uses `AttachThreadInput` + `SetForegroundWindowNative` as the primary path, followed by `SwitchToThisWindow` as the first fallback, and `BringWindowToTop` + `SetForegroundWindowNative` as the final fallback. It SHALL NOT use `keybd_event`-based workarounds.

#### Scenario: Grouped slot activates a selected window
- **WHEN** a grouped radial slot has already selected a concrete window target
- **THEN** Pulsar SHALL activate that window through `IFocusManager.ActivateWindowAsync()` rather than a UI strategy-specific native call sequence

#### Scenario: Plugin-driven process switch activates a selected window
- **WHEN** a WinSwitcher plugin action resolves a concrete window target
- **THEN** Pulsar SHALL activate that window through `IFocusManager.ActivateWindowAsync()`, the same activation path used by grouped slot switching

#### Scenario: Quick switch activates a resolved target
- **WHEN** the quick-switch engine resolves a concrete target window
- **THEN** Pulsar SHALL activate that target through `IFocusManager.ActivateWindowAsync()` instead of using a quick-switch-specific native foreground sequence

#### Scenario: Primary activation fails
- **WHEN** the primary `SetForegroundWindowNative` fails
- **THEN** Pulsar SHALL fallback to calling the native `SwitchToThisWindow` API, followed by a verification check of the foreground window, before falling back to `BringWindowToTop`.

### Requirement: Shared activation SHALL restore minimized windows before foreground switching
If the selected target window is minimized, Pulsar SHALL restore the window before completing foreground activation.

#### Scenario: Selected target is minimized
- **WHEN** the shared activation path receives a minimized target window
- **THEN** Pulsar SHALL restore that window before bringing it to the foreground

### Requirement: Shared activation SHALL handle invalid targets predictably
If the selected target window is no longer valid at activation time, Pulsar SHALL fail predictably without invoking an undefined native activation sequence.

#### Scenario: Selected target handle is no longer valid
- **WHEN** the shared activation path receives a target window handle that is no longer a valid window
- **THEN** Pulsar SHALL return a failed activation result and SHALL NOT report a successful switch

### Requirement: Shared activation SHALL expose activation outcome to callers
Pulsar SHALL return an activation outcome that allows callers and logs to distinguish successful activation from validation or OS-level activation failure.

#### Scenario: Caller receives failed activation outcome
- **WHEN** a switching flow invokes the shared activation path and activation does not complete successfully
- **THEN** the caller SHALL receive a failed activation outcome that can be logged and surfaced consistently

### Requirement: Shared activation SHALL provide visual confirmation after successful foreground switch
After the shared activation path successfully activates a target window, Pulsar SHALL request the system to flash the target window's title bar and taskbar button so the user receives unambiguous visual confirmation of the switch.

#### Scenario: Target window is activated successfully
- **WHEN** the shared activation path successfully brings a target window to the foreground
- **THEN** Pulsar SHALL flash the target window's title bar and taskbar button using `FlashWindowEx` with 3 flashes at the system default blink rate

#### Scenario: Activation fails
- **WHEN** the shared activation path cannot activate the target window
- **THEN** Pulsar SHALL NOT flash any window and SHALL return a failed activation result

#### Scenario: Target is already foreground
- **WHEN** the target window is already the current foreground window
- **THEN** Pulsar SHALL skip the flash since no switch occurred

