# Pulsar Switcher Development Plan

This document outlines the phased implementation plan for the "Deep-Dive Switcher" functionality in Pulsar, based on the [Requirements Document](switcher需求文档.md).

## Phase 1: Foundation & Data Snapshot
**Goal**: Establish the data structures and service capabilities required to capture and store window states efficiently.

### 1.1 Extend `IWindowService`
- [x] Add `GetProcessWindows(int processId)` or similar method to retrieve all visible window handles for a specific process.
- [x] Ensure `WindowInfo` model includes:
    - `IntPtr Handle`
    - `string Title`
    - `DateTime StartTime` (for sorting)
    - `string ProcessName`
    - `BitmapSource` or `IntPtr` for Icon/Thumbnail.

### 1.2 Enhanced `PulsarContext`
- [x] Modify `PulsarContext` struct to include `IEnumerable<WindowInfo> AllWindows`.
- [x] Update `PulsarContext.Capture()` to populate this list efficiently.
    - **Optimization**: To avoid performance hits, consider lazy loading or only capturing the *list* of handles initially, and fetching details on demand, OR ensure `EnumWindows` is fast enough. *Requirement says "Snapshot... Immutable", so full capture is preferred if performant.*

### 1.3 Native Helpers
- [x] Create `Native/DwmHelper.cs` (or extend `NativeMethods`) to handle DWM Thumbnail registration (`DwmRegisterThumbnail`, `DwmUpdateThumbnailProperties`).
- [x] Ensure thumbnail generation is non-blocking (async/await or background thread).

**Acceptance Criteria:**
- Unit tests or debug output verify that `PulsarContext.Capture()` correctly lists all visible windows for running processes.
- Window list includes valid Handles and Launch Times.

---

## Phase 2: ViewModel State Machine & Navigation
**Goal**: Implement the "Root -> Sub-menu" navigation logic and spatial organization.

### 2.1 RadialMenuViewModel Refactor
- [x] Introduce `MenuState` enum: `Root`, `SubMenu`.
- [x] Add `Stack<MenuState>` or `ParentSlot` reference to handle "Roll-back".
- [x] Implement `EnterSubMenu(SlotViewModel parent)`:
    - Saves current root slots.
    - Generates new slots from `parent.AssociatedWindows`.
    - Sorts windows by `StartTime` (Oldest at 12:00, clockwise).

### 2.2 Interaction Logic
- [x] **Drill-down**: Modify `ExecuteSelection` (or add `HandleLeftClick`) to detect if the selected slot is a "Group/App" slot. If so, trigger `EnterSubMenu`.
- [x] **Roll-back**: Clicking the Center Orb (`Slot 0`) while in `SubMenu` state should trigger `RestoreRootMenu`.
- [x] **Cancel**: Releasing `Ctrl` while on Center Orb (in `Root` state) or outside valid slots closes Pulsar without action.
- [x] **Trigger**: Releasing `Ctrl` on a leaf node (Window Slot) triggers `WinSwitcherPlugin.SwitchToWindow(hwnd)`.

### 2.3 Smart Layout (Pivot Adjustment)
- [x] In `Show()`, calculate if the mouse position is too close to screen edges.
- [x] Apply an offset to the `Window.Left/Top` to ensure the sub-menu (expanded ring) remains fully visible.
- [x] Ensure the "Mouse Cursor" relative to the "Center" remains consistent (visual shift, not logical cursor shift).

**Acceptance Criteria:**
- User can click an App slot to see its windows.
- Windows are sorted by start time (12:00 start).
- Clicking Center Orb returns to App list.
- Releasing Ctrl on a Window slot triggers a (mock) switch action.

---

## Phase 3: Visual Experience (UI/UX)
**Goal**: Implement the "Lens" effect, DWM thumbnails, and dynamic titles.

### 3.1 16:9 Glass Backdrop (The Lens)
- [x] Create a new UI control `ThumbnailBackdrop` (or similar) in `RadialMenuWindow.xaml`.
- [x] Position it behind the Center Orb but above the background.
- [x] Implement visibility logic: Only visible when hovering a Sub-menu slot.

### 3.2 DWM Live Thumbnails
- [x] Integrate `DwmHelper` into the `ThumbnailBackdrop` control.
- [x] On `SlotHover`:
    - Get `WindowHandle` from the slot.
    - Call `DwmRegisterThumbnail` targeting the backdrop area.
- [x] On `SlotExit`: Unregister thumbnail.

### 3.3 Dynamic Title
- [x] Bind the Bottom Title text to a new property `CurrentHoverTitle` in ViewModel.
- [x] Logic:
    - Default: Profile Name (e.g., "Global").
    - Hover Root Slot: App Name / Plugin Name.
    - Hover Sub Slot: Window Title (e.g., "Project.docx - Word").

**Acceptance Criteria:**
- Hovering a window slot shows a live DWM thumbnail behind the center orb.
- The bottom text updates accurately based on hover state.
- UI follows strict `PulsarPrimaryButtonStyle` and theme guidelines.

---

## Phase 4: Integration & Polish
**Goal**: Finalize plugin integration and ensure "High-Tension" feel.

### 4.1 WinSwitcherPlugin Update
- [x] Ensure the plugin exposes the necessary data for the Root slots to identify themselves as "Drill-down capable".
- [x] Implement the specific `SwitchToWindow(IntPtr handle)` action. (Handled directly in ViewModel via WindowHelper).

### 4.2 Performance Tuning
- [x] Verify `Capture()` takes < 50ms.
- [x] Ensure DWM Thumbnails do not lag the UI thread.

### 4.3 Edge Cases
- [x] Handle "Process has 1 window" (Direct switch? Or Drill-down with 1 slot? *Decision: Drill-down for consistency, or auto-preview*).
- [x] Handle "Window closed while menu open" (Show failure message on click).

**Acceptance Criteria:**
- Full "Flow": Ctrl(Hold) -> Select App -> Click -> Select Window -> Ctrl(Release) -> Switch.
- No crashes if target window disappears.
- Smooth animations (if any) and instant response.

---

## Technical Notes & Constraints
- **Absolute Paths Only**: All file operations in code must use absolute paths.
- **Theme Isolation**: Remember `RadialMenuWindow` uses `Theme.Dark.xaml` and transparent background. Do not use Wpf.Ui `Appearance="Primary"`.
- **Git Safety**: Do not commit large binary assets or secrets.
