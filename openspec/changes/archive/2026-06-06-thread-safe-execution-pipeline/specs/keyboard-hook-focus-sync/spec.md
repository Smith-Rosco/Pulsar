## ADDED Requirements

### Requirement: Hotkey actions SHALL dispatch to the UI thread before execution
All hotkey action delegates registered with `IHotkeyService` SHALL be invoked on the WPF UI dispatcher thread, regardless of which thread the keyboard hook callback runs on. The keyboard hook thread SHALL NOT synchronously execute any UI or plugin code.

#### Scenario: Hotkey action executes on UI thread
- **WHEN** a registered hotkey action is triggered by a keyboard hook event on the OS hook thread
- **THEN** the action delegate SHALL be dispatched to `Application.Current.Dispatcher` via `InvokeAsync()` before execution

#### Scenario: Keyboard hook thread remains non-blocking
- **WHEN** a hotkey action performs long-running work (async loading, context capture)
- **THEN** the keyboard hook callback SHALL return immediately without waiting for the action to complete

#### Scenario: Multiple hotkey actions are dispatched independently
- **WHEN** two different hotkey combinations are pressed in sequence
- **THEN** each action SHALL dispatch to the UI thread independently and SHALL NOT interfere with the other's dispatch
