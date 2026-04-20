## Context

Currently, `RadialMenuWindow` attempts to manage mouse clicks using WPF's `UIElement.CaptureMouse()`. However, Windows `SetCapture` API limitations mean that mouse capture only persists outside the window bounds if a mouse button is actively held down (drag state). If the user just moves the mouse outside the 500x500 window without holding a button, clicks will fall through to underlying applications, causing accidental interactions and causing the menu to lose focus.

We need a way to intercept and swallow all mouse clicks (Left and Right) globally while the radial menu is open, regardless of where the cursor is on the screen, without resorting to a full-screen transparent window (which has multi-monitor and DPI scaling issues).

We already have a low-level mouse hook implemented in `Pulsar.Native.GlobalMouseWheelHook` for scrolling. This design extends that pattern to handle clicks.

## Goals / Non-Goals

**Goals:**
- Prevent mouse clicks (Left and Right) from reaching background applications while the Radial Menu is visible.
- Ensure that clicking anywhere on the screen (even outside the 500x500 window) successfully triggers the currently highlighted slot.
- Introduce Right-Click as a "Go Back" mechanism for nested sub-menus.
- Provide visual feedback when attempting to "Go Back" from the root menu.
- Maintain high performance; the hook must not cause system-wide mouse lag.

**Non-Goals:**
- Creating a full-screen transparent overlay window.
- Implementing complex multi-select or pin/dock features with Right-Click at this time (reserved for future exploration).
- Replacing keyboard input mechanisms (hotkeys remain the primary invocation method).

## Decisions

### 1. Unified Global Mouse Hook
**Decision:** Instead of modifying `GlobalMouseWheelHook`, we will create a new, unified `GlobalMouseHook` (or rename the existing one) that handles `WM_MOUSEWHEEL`, `WM_LBUTTONDOWN`, `WM_LBUTTONUP`, `WM_RBUTTONDOWN`, and `WM_RBUTTONUP`.
**Rationale:** Having multiple WH_MOUSE_LL hooks installed simultaneously can degrade system performance. A single hook processing all necessary mouse messages is more efficient.
**Alternative:** Create a separate `GlobalMouseClickHook`. Rejected due to performance concerns with multiple hooks.

### 2. Event Routing to ViewModel
**Decision:** The `GlobalMouseHook` will expose standard .NET events (e.g., `OnMouseLeftButtonDown`, `OnMouseRightButtonUp`). The `RadialMenuWindow.xaml.cs` (or a dedicated Service) will subscribe to these events and forward them to the `RadialMenuViewModel`.
**Rationale:** Keeps the native hook logic decoupled from the UI/ViewModel logic. The Hook just reports "a click happened globally", and the ViewModel decides what to do based on its `IsVisible` state.
**Alternative:** Inject the ViewModel directly into the Hook. Rejected as it violates separation of concerns.

### 3. Swallowing Clicks
**Decision:** The Hook event args will include a `Handled` boolean. If the ViewModel determines the menu is open, it sets `Handled = true`. The Hook will then return `1` to `CallNextHookEx`, effectively swallowing the event and preventing Windows from passing it to the background app.
**Rationale:** Standard mechanism for low-level hooks to intercept input.

### 4. Right-Click Semantics
**Decision:** 
- **Sub-Menu:** Right-click triggers `RestoreRootMenu()`.
- **Root Menu:** Right-click triggers a visual "bounce" or "shake" animation on the center slot, indicating "cannot go back further".
**Rationale:** Provides an intuitive, mouse-centric navigation flow without requiring the user to aim for the center slot to cancel/go back.

### 5. Hook Lifecycle Management
**Decision:** The hook will be instantiated when the application starts (since it's also needed for mouse wheel scrolling over slots). However, the *click interception* logic inside the hook callback will quickly return if `RadialMenuViewModel.IsVisible` is false.
**Rationale:** Installing and uninstalling global hooks frequently can be unstable. A persistent hook with a fast-path exit when not needed is standard practice.

## Risks / Trade-offs

- **[Risk] System-wide Input Lag:** If the hook callback takes too long to execute, the entire Windows mouse cursor can lag or stutter.
  - **Mitigation:** The hook callback must be extremely fast. It should not perform complex logic or UI updates directly. It should merely raise an event, set `Handled = true`, and let the ViewModel handle the actual action asynchronously (e.g., via `Dispatcher.InvokeAsync`).
- **[Risk] Unhandled Exceptions in Hook:** An exception in a low-level hook can cause the application to crash silently or destabilize the system mouse.
  - **Mitigation:** Wrap the hook callback logic in a robust `try-catch` block. Log errors to Sentinel, but ensure `CallNextHookEx` is always called if the event wasn't explicitly handled.
- **[Risk] Antivirus False Positives:** Global hooks are sometimes flagged by heuristic AV engines.
  - **Mitigation:** Since we are already using a mouse wheel hook, adding click detection shouldn't drastically change our security profile, but it remains a risk. Code signing is the primary defense.
