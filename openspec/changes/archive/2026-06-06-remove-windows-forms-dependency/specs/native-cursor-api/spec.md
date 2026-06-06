## ADDED Requirements

### Requirement: Cursor position uses GetCursorPos P/Invoke

The system SHALL use `GetCursorPos` P/Invoke (from `PulsarNative.cs`) instead of `System.Windows.Forms.Cursor.Position` for obtaining the global cursor position in screen coordinates.

#### Scenario: MouseTrackingService gets cursor position
- **WHEN** `MouseTrackingService.GetGlobalCursorPosition()` is called
- **THEN** the method calls `GetCursorPos(out POINT pt)` and returns a `System.Windows.Point(pt.X, pt.Y)`

#### Scenario: RadialMenuWindow positions near cursor
- **WHEN** `RadialMenuWindow` positions itself at invocation time
- **THEN** the window center is placed at the cursor position obtained via `GetCursorPos`
- **AND** DPI scaling is correctly applied for WPF logical units

#### Scenario: SlotOrb parallax animation uses cursor
- **WHEN** `SlotOrb` computes parallax offset for animation
- **THEN** the cursor position is obtained via `GetCursorPos`
- **AND** the computed offset is based on the orb's screen position relative to the cursor

### Requirement: Dialog positioning uses native screen API

The `DialogService.PositionNearMouse()` method SHALL use `GetCursorPos` + `MonitorFromPoint` + `GetMonitorInfo` instead of `Control.MousePosition` + `Screen.FromPoint` for positioning dialogs near the mouse cursor with screen boundary awareness.

#### Scenario: Dialog placed near cursor within screen bounds
- **WHEN** a dialog is shown near the mouse cursor
- **THEN** the dialog is offset from the cursor by (20, 20) pixels
- **AND** the dialog remains within the working area of the monitor containing the cursor

#### Scenario: Dialog respects taskbar boundaries
- **WHEN** the cursor is near the bottom-right edge of the screen
- **THEN** the dialog is repositioned to stay within the monitor's working area (excluding the taskbar)
