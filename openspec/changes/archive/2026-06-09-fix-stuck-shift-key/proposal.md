## Why

The Shift modifier key frequently gets stuck in a "pressed" state in the internal keyboard modifier tracker (`GlobalKeyboardHook._trackedShiftDown`), causing `Ctrl+Q` (Window Switcher) to be misidentified as `Ctrl+Shift+Q` (Action Grid). Users must physically tap Shift again to reset the state. This happens because the synthetic event suppression mechanism in `UpdateModifierTracker` blocks ALL events—including real physical KeyUp events—during focus restoration windows (50-200ms), silently dropping modifier key releases.

## What Changes

- **Fix `UpdateModifierTracker`** to distinguish injected (synthetic) keyboard events from physical events using the `LLKHF_INJECTED` flag from `KBDLLHOOKSTRUCT.flags`. During synthetic event suppression, only injected events are blocked; real physical events continue to update the tracker.
- **Remove `keybd_event` from `ForceActivate`**: The `FocusManager.ForceActivate()` method currently uses `keybd_event(VK_MENU, ...)` to simulate an Alt key press for foreground stealing. This violates the existing `keyboard-hook-focus-sync` spec requirement and is unnecessary given the `AttachThreadInput`-based activation path.
- **Add safety net**: Reset modifier state when the radial menu is shown or hidden to clear any accumulated stale state.
- **Reorder pre-filter**: Move `UpdateModifierTracker` before the pre-filter listener check in `HookCallback` so modifier tracking is never skipped due to missing event subscribers.

## Capabilities

### New Capabilities
- `injected-event-filtering`: The keyboard hook distinguishes injected (SendInput/keybd_event) keyboard events from physical keystrokes via `KBDLLHOOKSTRUCT.flags`, and applies synthetic event suppression only to injected events.

### Modified Capabilities
- `keyboard-hook-focus-sync`: The `OnSyntheticEventBegin/End` suppression requirement changes from "suppress ALL events" to "suppress only injected events, while continuing to process physical events." The spec already mandates removing `keybd_event` from the activation path, which this change enforces.

## Impact

- **`GlobalKeyboardHook.cs`**: Read `KBDLLHOOKSTRUCT.flags`, modify `UpdateModifierTracker` to check `LLKHF_INJECTED`, reorder pre-filter, add `ResetModifierState` calls from menu show/hide integration.
- **`FocusManager.cs`**: Remove `keybd_event` calls from `ForceActivate()`, eliminate `Thread.Sleep(50)`, simplify to use only `AllowSetForegroundWindow` + `SetForegroundWindowNative` + `BringWindowToTop`.
- **`RadialMenuViewModel.cs`**: Call `ResetAllModifiers()` via `IHotkeyService` or directly on the hook when the menu is shown or hidden.
- **`HotkeyService.cs`**: Optionally expose `ResetModifierState()` on `IHotkeyService` for callers to trigger state cleanup.
