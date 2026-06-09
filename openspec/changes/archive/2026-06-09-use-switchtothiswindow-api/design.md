## Context

Pulsar currently struggles with switching to certain windows in environments where it cannot run as an Administrator and cannot use `uiAccess="true"` (due to lacking a digital signature and running from arbitrary directories like Desktop or Downloads in enterprise environments). The existing fallbacks (like `BringWindowToTop`) are not reliable enough. The legacy hack of sending `keybd_event(VK_MENU)` was removed because it caused the Shift key to become stuck in the global keyboard hook state. 

Microsoft provides an undocumented/deprecated API called `SwitchToThisWindow` which tells the OS to bring the window to the front using the native Alt+Tab logic, effectively bypassing standard foreground lock restrictions without requiring Administrator privileges or `uiAccess`.

## Goals / Non-Goals

**Goals:**
- Increase the success rate of window switching for non-admin users.
- Integrate `SwitchToThisWindow` cleanly into the existing retry logic of `FocusManager`.
- Ensure no side-effects occur with the global keyboard hook or modifier keys.

**Non-Goals:**
- We are not implementing `uiAccess="true"` in the application manifest.
- We are not restoring the Alt-key `keybd_event` hack.
- We are not rewriting the entire focus management logic; we are just adding an intermediate fallback step.

## Decisions

### Decision 1: Use `SwitchToThisWindow` API

**Choice**: Add `SwitchToThisWindow` to the `PulsarNative` P/Invoke definitions and expose it via `IFocusNativeAdapter`.

**Alternatives considered**:
- *UI Automation*: Too heavy, requires instantiating accessibility APIs, and might have performance overhead.
- *`uiAccess="true"`*: Cannot be used without a digital certificate and installing in "Program Files", which is not feasible for the target restricted intranet environments.

**Rationale**: `SwitchToThisWindow` is lightweight, does not trigger fake keystrokes that might corrupt `GlobalKeyboardHook`, and has a high success rate as it leverages the native OS Alt+Tab mechanisms to bypass the Foreground Lock timeout.

### Decision 2: Modify `FocusManager.ActivateWindowAsync`

**Choice**: Modify `FocusManager.ActivateWindowAsync` to execute the following logic:
1. Try `AllowSetForegroundWindow` + `SetForegroundWindowNative`.
2. If it fails, try `SwitchToThisWindow(hWnd, true)`. Check success by calling `GetForegroundWindow()`.
3. If it still fails, fall back to `BringWindowToTop` + `SetForegroundWindowNative`.

**Rationale**: By keeping `SetForegroundWindowNative` as the primary path, we maintain the behavior that has proven safe. `SwitchToThisWindow` is used purely as an enhancement fallback. Since `SwitchToThisWindow` returns `void`, we must rely on checking `GetForegroundWindow()` after the call to verify if it succeeded.

## Risks / Trade-offs

- **Risk**: `SwitchToThisWindow` is officially documented as "not intended for general use" by Microsoft.
  - **Mitigation**: It has been present in Windows since XP and is used by many major tools (e.g. Wox, Listary). It is highly unlikely to be removed. We are using it as a fallback, not as the primary switching path.
- **Risk**: We might get a flash or visual artifact if the window does not restore properly.
  - **Mitigation**: We already restore minimized windows using `ShowWindow(hWnd, SW_RESTORE)` before attempting to set the foreground window.
