## ADDED Requirements

### Requirement: Shared activation SHALL flash the target window after successful foreground switch
After the shared activation path successfully brings a target window to the foreground, Pulsar SHALL flash the target window's title bar and taskbar button to provide unambiguous visual confirmation of the switch.

#### Scenario: Activation succeeds and window is foregrounded
- **WHEN** the shared activation path successfully calls `SetForegroundWindow` and the target window becomes foreground
- **THEN** Pulsar SHALL call `FlashWindowEx` with `FLASHW_CAPTION | FLASHW_TRAY` flags for the target window
- **AND** Pulsar SHALL flash exactly 3 times at the system cursor blink rate

#### Scenario: Activation fails or target is invalid
- **WHEN** the shared activation path determines the target window is invalid or activation does not complete successfully
- **THEN** Pulsar SHALL NOT call `FlashWindowEx` for that target

#### Scenario: Target window is already foreground
- **WHEN** the shared activation path receives a target window that is already the foreground window
- **THEN** Pulsar SHALL NOT call `FlashWindowEx` since no switch occurred

### Requirement: FlashWindowEx SHALL use the system default flash parameters
Pulsar SHALL use system-defined flash timing rather than custom durations to respect user accessibility settings.

#### Scenario: Flash is triggered
- **WHEN** Pulsar calls `FlashWindowEx`
- **THEN** `dwTimeout` SHALL be set to 0 (default cursor blink rate)
- **AND** `uCount` SHALL be set to 3
- **AND** `dwFlags` SHALL include both `FLASHW_CAPTION` and `FLASHW_TRAY`
