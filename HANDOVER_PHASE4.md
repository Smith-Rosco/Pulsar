# Handover Document: Deep-Dive Switcher Implementation

**Date**: 2026-02-09
**Status**: Phase 1-3 Complete. Phase 4 (Polish) Pending.
**Previous Agent**: Opencode (Gemini 3 Pro)

## 1. Project Context
We are implementing the **Deep-Dive Switcher** (Window Walker) as described in `switcher需求文档.md`. This transforms Pulsar's switcher from a simple app launcher into a hierarchical window navigator (App -> Windows).

## 2. Completed Work

### Phase 1: Foundation (Data)
*   **`IWindowService`**: Extended to support `GetProcessWindowsAsync(int pid)`.
*   **`WindowService`**: Implemented `EnumWindows` logic to capture all visible windows, extracting Icons and StartTime for sorting.
*   **`PulsarContext`**: Upgraded to `CaptureAsync`. It now snapshots the entire window tree state when the hotkey is released.
*   **`Native/DwmHelper`**: Created P/Invoke definitions for `DwmRegisterThumbnail` API.

### Phase 2: Logic (State Machine)
*   **`RadialMenuViewModel`**:
    *   Implemented `MenuState` enum (`Root`, `SubMenu`).
    *   **Drill-Down**: Clicking a Process Slot clears the menu and populates it with that process's windows (sorted by StartTime).
    *   **Roll-Back**: Clicking Center Orb in `SubMenu` returns to `Root`.
    *   **Action**: Releasing Ctrl on a Window Slot triggers `WindowHelper.SetForegroundWindow`.
    *   **Launcher Mode**: Now dynamically loads running processes (Limit 8) instead of static config.

### Phase 3: Visuals (The Lens)
*   **`ThumbnailHost.cs`**: A custom WPF control that hosts the DWM Thumbnail (Live Preview).
*   **`RadialMenuWindow.xaml`**:
    *   Integrated `ThumbnailHost` behind the Center Orb.
    *   Added a "Glass" border effect behind the thumbnail.
    *   **Dynamic Title**: Bottom text now updates based on hover state (e.g., "Back", App Name, or Full Window Title).
*   **Safety**: Added `WindowHelper.IsWindow` check before switching to prevent crashes on closed windows.

## 3. Pending Tasks (To-Do for Next Agent)

### 3.1 Smart Layout (Pivot Adjustment)
*   **File**: `Views/RadialMenuWindow.xaml.cs` -> `UpdateDpiAndPosition` method.
*   **Status**: Stubbed but not implemented.
*   **Task**: Implement logic to shift the Window `Left`/`Top` if the mouse is too close to the screen edge, ensuring the expanded sub-menu ring doesn't get clipped.

### 3.2 Visual Polish
*   **Thumbnail Quality**: Verify `DwmUpdateThumbnailProperties` settings (Opacity, SourceClientAreaOnly).
*   **Animations**: The transition between Root and SubMenu is currently instant. Consider adding a subtle fade or scale animation in `RadialMenuWindow.xaml`.
*   **Empty State**: Handle cases where a process is running but has no visible windows (currently logic might show an empty sub-menu).

### 3.3 Edge Cases & Cleanup
*   **Process Grouping**: Currently takes the first 8 processes. Logic should be smarter (e.g., MRU order or excluding background apps).
*   **Resource Management**: Ensure `ThumbnailHost` correctly unregisters DWM thumbnails when visibility changes to prevent resource leaks (Initial implementation handles `Unloaded`, but verify `IsVisible` changes).

## 4. Key Files
*   `Pulsar/ViewModels/RadialMenuViewModel.cs`: Core logic, State Machine.
*   `Pulsar/Views/RadialMenuWindow.xaml`: UI layout, Lens effect.
*   `Pulsar/Views/Controls/ThumbnailHost.cs`: DWM wrapper control.
*   `Pulsar/Services/WindowService.cs`: Window enumeration logic.
*   `Pulsar/Native/WindowHelper.cs`: P/Invoke signatures.

## 5. How to Verify
1.  **Build**: `dotnet build Pulsar/Pulsar/Pulsar.csproj` (Currently builds successfully).
2.  **Run**: `dotnet run --project Pulsar/Pulsar/Pulsar.csproj`.
3.  **Test**:
    *   Open multiple windows (e.g., Notepad, Calc, Browser).
    *   Trigger Pulsar Switcher (Default key or configured).
    *   **Hover** an app to see the App Name.
    *   **Click** an app to drill down.
    *   **Hover** a window slot to see the **Live DWM Thumbnail** in the center.
    *   **Release Key** to switch.

---
**Guidance**: Focus on **3.1 Smart Layout** next. The visual "Lens" is working, but if the menu spawns at the screen edge, the sub-slots might be cut off.
