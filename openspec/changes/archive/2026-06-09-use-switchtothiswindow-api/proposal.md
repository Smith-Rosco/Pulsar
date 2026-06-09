## Why

Pulsar currently uses `AttachThreadInput` and `SetForegroundWindow` to perform window switching. When these fail, the fallback mechanism relies on `BringWindowToTop` and `ForceActivate` which often fails in non-admin environments (due to the removal of the Alt-key hack that caused the Shift-key stuck issue). Since `uiAccess="true"` is not a viable option for users without administrator privileges in restricted corporate environments, we need a more reliable fallback. The `SwitchToThisWindow` API provides a "backdoor" to the Windows OS that acts like the native Alt+Tab, and can significantly improve window switching reliability without side effects.

## What Changes

- Add a P/Invoke definition for `SwitchToThisWindow` in `PulsarNative`.
- Expose `SwitchToThisWindow` in `IFocusNativeAdapter` and its concrete implementation.
- Update `FocusManager.ActivateWindowAsync` to use `SwitchToThisWindow` as the first retry step when `SetForegroundWindowNative` fails.

## Capabilities

### New Capabilities

### Modified Capabilities
- `window-switch-activation-path`: The activation path will try `SwitchToThisWindow` before falling back to `BringWindowToTop` when `SetForegroundWindowNative` fails.

## Impact

- `PulsarNative.cs` will gain a new native method definition.
- `IFocusNativeAdapter.cs` and `WindowsFocusNativeAdapter.cs` will expose the new method.
- `FocusManager.cs` will be updated to modify the fallback logic in `ActivateWindowAsync`.
- Window switching reliability will be significantly improved for environments without `uiAccess="true"`.
