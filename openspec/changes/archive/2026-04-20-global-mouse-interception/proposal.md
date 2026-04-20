## Why

The current implementation of the radial menu (`RadialMenuWindow`) relies on a transparent Canvas to handle interactions within a 500x500 window. This design poses a significant limitation: if the user clicks outside the 500x500 bounds while the menu is open, the click "falls through" to the underlying application, potentially causing unintended actions (like closing a document or clicking a random link), and forces the WPF window to lose focus.

To achieve a true "frictionless" experience, we need the radial menu to act as an invisible, full-screen input absorber without actually resizing the window (which causes performance and rendering issues on multi-monitor setups). By leveraging a Global Mouse Hook, we can intercept and swallow all mouse clicks globally while the menu is visible, ensuring that the user can drag their mouse anywhere on the screen to trigger a slot, without fear of interacting with background applications. Furthermore, we can re-purpose the Right-Click to act as a universal "Go Back" / "Cancel" action within the menu.

## What Changes

- Implement a global mouse hook to intercept left and right mouse clicks globally.
- When the radial menu is visible:
  - **Left Clicks:** Swallowed by the hook to prevent interacting with background applications. It will simultaneously trigger the `HandleLeftClick` logic of the active slot.
  - **Right Clicks:** Swallowed by the hook. If inside a sub-menu, it navigates back to the root menu. If at the root menu, it does nothing but show a visual/auditory indication that it cannot go back further.
- Ensure the radial menu remains visible and functional even if the mouse cursor ventures far outside its visual bounds.
- Remove reliance on WPF's native `CaptureMouse` for the `RadialMenuWindow`, relying purely on the global hook for interception.

## Capabilities

### New Capabilities
- `global-mouse-interception`: The ability to intercept, handle, and suppress mouse events (Left Click, Right Click) system-wide, conditionally based on application state (e.g., radial menu visibility), protecting background applications from unintended clicks.

### Modified Capabilities
- `radial-menu`: The radial menu's interaction model changes from WPF-bounded mouse capture to global hook-based input handling. Right-click navigation is introduced as a new interaction paradigm.

## Impact

- **Native Hooks (`Pulsar.Native`):** New classes will be created or existing ones (`GlobalMouseWheelHook.cs`) will be extended to handle `WM_LBUTTONDOWN`, `WM_LBUTTONUP`, `WM_RBUTTONDOWN`, `WM_RBUTTONUP`.
- **ViewModels (`RadialMenuViewModel`, `RadialMenuInputCoordinator`):** Need to be updated to receive click events from the hook rather than WPF `MouseLeftButtonUp` events. Must implement Right-Click navigation logic.
- **Views (`RadialMenuWindow.xaml.cs`):** Removal of `MenuCanvas.CaptureMouse()` and `ReleaseMouseCapture()`.
- **Performance:** Adding global hooks requires careful resource management to avoid system-wide input lag. The hook must process quickly and ideally be unhooked or bypassed efficiently when the menu is not visible.
