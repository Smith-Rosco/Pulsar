## ADDED Requirements

### Requirement: SmartSwitch SHALL show a launching notification when falling through to process start
When a WinSwitcher SmartSwitch action cannot find a running window and falls through to `Process.Start()`, Pulsar SHALL display a tray notification indicating that the application is being launched.

#### Scenario: SmartSwitch falls through to launch
- **WHEN** the WinSwitcher plugin executes a SmartSwitch action
- **AND** no running window is found for the target process
- **AND** a launch path is configured
- **THEN** Pulsar SHALL show a tray notification with title "Launching" and message containing the application name
- **AND** the notification SHALL fire before `Process.Start()` is called

#### Scenario: SmartSwitch finds a running window
- **WHEN** the WinSwitcher plugin executes a SmartSwitch action and a running window is found
- **THEN** Pulsar SHALL NOT show a launching notification
- **AND** the switch SHALL proceed normally

#### Scenario: SmartSwitch has no launch path configured
- **WHEN** the WinSwitcher plugin executes a SmartSwitch action
- **AND** no running window is found
- **AND** no launch path is configured
- **THEN** Pulsar SHALL NOT show a launching notification
- **AND** the action SHALL return an error result indicating the missing path

### Requirement: Launching notification SHALL auto-dismiss
The launching notification SHALL use the default balloon tip timeout and SHALL NOT require user dismissal.

#### Scenario: Notification is displayed
- **WHEN** a launching notification is shown
- **THEN** the notification SHALL auto-dismiss after the system default balloon tip timeout
- **AND** no user interaction SHALL be required to dismiss it
