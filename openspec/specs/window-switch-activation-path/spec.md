## ADDED Requirements

### Requirement: Window activation SHALL use a shared activation path
Pulsar SHALL activate a selected window through a shared activation path so app-switching entry points do not implement independent foreground-switch logic, and all user-facing foreground switching flows that activate a concrete target window SHALL delegate to that shared path.

#### Scenario: Grouped slot activates a selected window
- **WHEN** a grouped radial slot has already selected a concrete window target
- **THEN** Pulsar SHALL activate that window through the shared activation path rather than a UI strategy-specific native call sequence

#### Scenario: Plugin-driven process switch activates a selected window
- **WHEN** a WinSwitcher plugin action resolves a concrete window target
- **THEN** Pulsar SHALL activate that window through the same shared activation path used by grouped slot switching

#### Scenario: Quick switch activates a resolved target
- **WHEN** the quick-switch engine resolves a concrete target window
- **THEN** Pulsar SHALL activate that target through the shared activation path instead of using a quick-switch-specific native foreground sequence

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
