## Why

When Pulsar is summoned with `Ctrl+Q` during a Windows file drag operation, the radial menu appears but mouse-wheel paging stops working. This breaks a key switcher workflow exactly when users need it most: keeping the left mouse button held on a dragged file while paging through windows to find a destination.

## What Changes

- Add a new runtime capability that keeps radial-menu page navigation available while Pulsar is visible during an active drag session.
- Move wheel-based paging off the `RadialMenuWindow`'s local WPF mouse-wheel event path and onto a global mouse-wheel input path that remains available during OLE drag-and-drop loops.
- Define when Pulsar may consume wheel input and when it must pass wheel input through unchanged to avoid interfering with normal system scrolling.
- Preserve existing paging semantics such as root-only paging, single-page hints, and boundary feedback while extending them to drag-session invocation.

## Capabilities

### New Capabilities
- `drag-session-wheel-paging`: Radial menu paging remains available when Pulsar is invoked during an active Windows drag-and-drop session.

### Modified Capabilities
- (none)

## Impact

**Affected Code:**
- `Pulsar/Pulsar/Views/RadialMenuWindow.xaml.cs` - current wheel paging entry point is window-local and will no longer be the only path
- `Pulsar/Pulsar/ViewModels/RadialMenuViewModel.cs` - paging input gating remains here and must support drag-session wheel input
- `Pulsar/Pulsar/Services/HotkeyService.cs` and `Pulsar/Pulsar/Native/GlobalKeyboardHook.cs` - existing global input pattern informs the new approach
- New global mouse-wheel input service or native hook implementation under `Services/` and/or `Native/`

**Systems Affected:**
- Global input handling
- Radial menu paging behavior
- Drag-and-drop window switching workflow

**Dependencies / APIs:**
- Windows low-level mouse input (`WM_MOUSEWHEEL`, likely via `WH_MOUSE_LL` or equivalent)

**No breaking API changes** are expected, but this change does alter runtime input-routing behavior while the radial menu is visible.
