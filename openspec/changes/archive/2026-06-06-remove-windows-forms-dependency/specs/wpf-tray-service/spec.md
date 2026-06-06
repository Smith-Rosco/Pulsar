## ADDED Requirements

### Requirement: TrayIconService uses Hardcodet TaskbarIcon

The `TrayIconService` SHALL use `Hardcodet.Wpf.TaskbarIcon.NotifyIcon` (WPF `TaskbarIcon`) instead of `System.Windows.Forms.NotifyIcon` for system tray icon management.

#### Scenario: Tray icon initialization
- **WHEN** `TrayIconService.Initialize()` is called
- **THEN** a `TaskbarIcon` is created with a custom `.ico` resource loaded from `pack://application:,,,/Pulsar.ico`
- **AND** the icon is displayed in the system tray

#### Scenario: Icon load failure fallback
- **WHEN** the custom `.ico` resource fails to load
- **THEN** the tray icon falls back to a default application icon
- **AND** no exception is thrown to the caller

### Requirement: Tray right-click context menu uses WPF ContextMenu

The `TrayIconService` SHALL attach a WPF `ContextMenu` (not `Forms.ContextMenuStrip`) to the `TaskbarIcon`, containing "Settings" and "Exit Pulsar" menu items.

#### Scenario: Settings menu item opens settings window
- **WHEN** user clicks "Settings" in the tray context menu
- **THEN** the `SettingsWindow` is shown (or created if not existing)
- **AND** minimized windows are restored

#### Scenario: Exit menu item shuts down application
- **WHEN** user clicks "Exit Pulsar" in the tray context menu
- **THEN** the tray icon is disposed
- **AND** `Application.Current.Shutdown()` is called

### Requirement: Left-click on tray icon opens settings

The `TaskbarIcon` SHALL respond to left-mouse-button clicks by opening the `SettingsWindow`, matching the existing behavior (`Forms.MouseButtons.Left` handler).

#### Scenario: Single left-click on tray icon
- **WHEN** user left-clicks the tray icon
- **THEN** the `SettingsWindow` is opened

### Requirement: Balloon notifications use TaskbarIcon.ShowBalloonTip

The `TrayIconService.ShowNotification()` method SHALL use `TaskbarIcon.ShowBalloonTip()` to display balloon notifications with a 3-second timeout, mapping `PulsarNotificationIcon` to `Hardcodet.Wpf.TaskbarIcon.BalloonIcon`.

#### Scenario: Info notification
- **WHEN** `ShowNotification("Title", "Message", PulsarNotificationIcon.Info)` is called
- **THEN** a balloon tip appears with the title "Title", message "Message", and info-level icon
- **AND** the balloon auto-dismisses after 3 seconds

### Requirement: Tray icon is disposed on shutdown

The `TrayIconService.Dispose()` method SHALL hide and dispose the `TaskbarIcon`, matching existing cleanup behavior.

#### Scenario: Application shutdown
- **WHEN** `Dispose()` is called
- **THEN** the `TaskbarIcon` is hidden and disposed
- **AND** no tray icon remnant remains in the system tray
