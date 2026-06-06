## ADDED Requirements

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
