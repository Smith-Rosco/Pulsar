## ADDED Requirements

### Requirement: WindowActivationMonitor SHALL use PulsarNative for WinEvent hooks

The file `Services/WindowActivationMonitor.cs` SHALL use `PulsarNative.SetWinEventHook` and `PulsarNative.UnhookWinEvent` instead of inline P/Invoke declarations.

#### Scenario: Hook uses PulsarNative
- **WHEN** WindowActivationMonitor sets up foreground window event hook
- **THEN** it calls `PulsarNative.SetWinEventHook` not inline definition

### Requirement: WindowActivationMonitor SHALL remove inline delegate and constants

The inline `WinEventDelegate` declaration and constants `EVENT_SYSTEM_FOREGROUND`, `WINEVENT_OUTOFCONTEXT` SHALL be removed after migration.

#### Scenario: Inline declarations removed
- **WHEN** WindowActivationMonitor source is searched for "WinEventDelegate"
- **THEN** no inline delegate definition exists (uses PulsarNative.WinEventDelegate)

### Requirement: WindowActivationMonitor behavior SHALL remain unchanged

The monitor SHALL continue to raise `GlobalWindowActivated` events when foreground window changes, with identical timing and filtering logic.

#### Scenario: Event still fires correctly
- **WHEN** foreground window changes to a new application
- **THEN** GlobalWindowActivated event fires with the new window handle