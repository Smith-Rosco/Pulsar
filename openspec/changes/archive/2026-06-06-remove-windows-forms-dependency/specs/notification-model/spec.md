## ADDED Requirements

### Requirement: PulsarNotificationIcon enum replaces ToolTipIcon

The system SHALL define a `PulsarNotificationIcon` enum in `Pulsar.Models` with values `Info`, `Warning`, `Error`, and `None`. All code SHALL use `PulsarNotificationIcon` instead of `System.Windows.Forms.ToolTipIcon`.

#### Scenario: Enum values match notification severity
- **WHEN** code references `PulsarNotificationIcon.Info`
- **THEN** the value represents an informational notification
- **WHEN** code references `PulsarNotificationIcon.Error`
- **THEN** the value represents an error notification

### Requirement: ActionFeedback uses PulsarNotificationIcon

The `ActionFeedback` model class SHALL store its notification icon using `PulsarNotificationIcon` instead of `ToolTipIcon`. The `ActionFeedback.Icon` property SHALL be of type `PulsarNotificationIcon`.

#### Scenario: Creating a success feedback
- **WHEN** `ActionFeedbackService.Create("com.pulsar.pki", "fill", successResult)` is called
- **THEN** the returned `ActionFeedback.Icon` is `PulsarNotificationIcon.Info`

#### Scenario: Creating an error feedback
- **WHEN** `ActionFeedbackService.Create("com.pulsar.pki", "fill", errorResult)` is called
- **THEN** the returned `ActionFeedback.Icon` is `PulsarNotificationIcon.Error`

### Requirement: ITrayService.ShowNotification accepts PulsarNotificationIcon

The `ITrayService.ShowNotification(string title, string message, PulsarNotificationIcon icon)` method SHALL accept `PulsarNotificationIcon` as its icon parameter. Implementations SHALL map `PulsarNotificationIcon` values to the underlying tray library's icon type internally.

#### Scenario: Tray notification with warning icon
- **WHEN** `ITrayService.ShowNotification("Title", "Message", PulsarNotificationIcon.Warning)` is called
- **THEN** the tray implementation renders a warning-level balloon notification

### Requirement: Execution pipeline callers use PulsarNotificationIcon

The `PluginRuntimeKernel` (Circuit Breaker notifications) and `PluginRegistryV2` (hot-reload notifications) SHALL call `ITrayService.ShowNotification()` with `PulsarNotificationIcon` values instead of `ToolTipIcon`.

#### Scenario: Circuit breaker trip notification
- **WHEN** Circuit Breaker trips for an extension plugin
- **THEN** `ShowNotification` is called with `PulsarNotificationIcon.Error`

#### Scenario: Plugin hot-reload success notification
- **WHEN** a plugin is successfully hot-reloaded
- **THEN** `ShowNotification` is called with `PulsarNotificationIcon.Info`
