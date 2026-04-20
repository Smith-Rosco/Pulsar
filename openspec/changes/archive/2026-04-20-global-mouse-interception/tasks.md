## 1. Native Hook Implementation

- [x] 1.1 Create `GlobalMouseHook.cs` in `Pulsar.Native` (or rename/expand `GlobalMouseWheelHook`).
- [x] 1.2 Implement P/Invoke signatures for `WM_LBUTTONDOWN`, `WM_LBUTTONUP`, `WM_RBUTTONDOWN`, `WM_RBUTTONUP`.
- [x] 1.3 Create a unified event args class (e.g., `GlobalMouseEventArgs`) that includes button type, position, and a `Handled` property.
- [x] 1.4 Implement the hook callback logic to raise the managed event and respect the `Handled` property by returning `1` when true.
- [x] 1.5 Update DI registration in `App.xaml.cs` to use the new unified hook.

## 2. Input Coordinator Updates

- [x] 2.1 Update `RadialMenuInputCoordinator` to handle a new `HandleGlobalMouseClickAsync` method.
- [x] 2.2 Move existing `HandleLeftClickAsync` logic into the new global handler.
- [x] 2.3 Implement Right-Click logic in `HandleGlobalMouseClickAsync`: call `restoreRootMenu()` if in a sub-menu.
- [x] 2.4 Implement Right-Click root menu bounce animation trigger (may require adding an event or command to `RadialMenuViewModel` that the View can subscribe to).

## 3. ViewModel Integration

- [x] 3.1 Inject the `GlobalMouseHook` (or a wrapper service) into `RadialMenuViewModel`.
- [x] 3.2 Subscribe to the hook's mouse events in `RadialMenuViewModel`.
- [x] 3.3 In the event handler, check `IsVisible`. If false, do nothing. If true, set `e.Handled = true`.
- [x] 3.4 In the event handler, if `IsVisible` is true, marshal the call to the UI thread (if necessary) and delegate to `RadialMenuInputCoordinator`.

## 4. View Cleanup & Animation

- [x] 4.1 In `RadialMenuWindow.xaml.cs`, remove `MenuCanvas.CaptureMouse()` from the `Summon()` method.
- [x] 4.2 In `RadialMenuWindow.xaml.cs`, remove `MenuCanvas.ReleaseMouseCapture()` from the `Dismiss()` method.
- [x] 4.3 In `RadialMenuWindow.xaml.cs`, remove the old `this.MouseLeftButtonUp += ...` handler.
- [x] 4.4 Implement the "bounce/shake" animation in `RadialMenuWindow.xaml` or `.cs` for the center slot, triggered by the ViewModel when a root-level right-click occurs.

## 5. Testing & Validation

- [x] 5.1 Test: Verify left-clicking outside the window triggers the active slot.
- [x] 5.2 Test: Verify left-clicking outside the window does *not* click background apps.
- [x] 5.3 Test: Verify right-clicking in a sub-menu returns to the root menu.
- [x] 5.4 Test: Verify right-clicking in the root menu plays the bounce animation and does *not* close the menu.
- [x] 5.5 Test: Verify normal mouse functionality is completely restored when the menu closes.
