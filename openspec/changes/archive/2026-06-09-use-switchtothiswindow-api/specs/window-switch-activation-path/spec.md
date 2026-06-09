## MODIFIED Requirements

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
