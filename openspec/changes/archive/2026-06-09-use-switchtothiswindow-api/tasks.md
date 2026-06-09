## 1. Native API Definition

- [x] 1.1 Add `SwitchToThisWindow` P/Invoke definition to `PulsarNative.cs`.
- [x] 1.2 Add `SwitchToThisWindow` method to `IFocusNativeAdapter.cs`.
- [x] 1.3 Implement `SwitchToThisWindow` in `WindowsFocusNativeAdapter.cs` that calls the P/Invoke method.

## 2. Fallback Logic Update

- [x] 2.1 Update `FocusManager.ActivateWindowAsync` to insert `SwitchToThisWindow(hWnd, true)` as the first fallback if `SetForegroundWindowNative` fails.
- [x] 2.2 Add verification logic using `GetForegroundWindow()` after `SwitchToThisWindow` to check if it succeeded.
- [x] 2.3 Ensure `BringWindowToTop` is only called if `SwitchToThisWindow` fallback also fails.
- [x] 2.4 Add appropriate `LogInformation` entries to trace the fallback execution path.

## 3. Verification

- [x] 3.1 Build the application and ensure no compilation errors.
- [ ] 3.2 Test window switching via the simulator or a debug build to ensure `SwitchToThisWindow` is invoked when needed and functions correctly without triggering keyboard state bugs.