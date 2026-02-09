# Handover Document: Switcher Polish & Paging

**Date**: 2026-02-09
**Status**: Features Implemented & Verified (Build Success).
**Previous Agent**: Opencode (Gemini 3 Pro)

## 1. Context
We have refined the Pulsar Switcher based on user feedback. The switcher now behaves more like a "Muscle Memory" tool with pinned slots, smart paging, and optimized interaction.

## 2. Implemented Features

### 2.1 Fixed Slot Layout (Muscle Memory)
*   **Logic**: `RadialMenuViewModel.LoadRunningProcessesAsync` now implements a "Pinned + Flow" strategy.
*   **Pinned Slots**: Reads the "Global" profile from `ProfilesConfig`. If a running app matches a configured slot (e.g., Slot 1 = "Chrome"), it is *forced* into that position on Page 1.
*   **Mixed Filling**: Empty slots on Page 1 are filled by other running apps (sorted by Z-Order/MRU).
*   **Overflow**: Any apps that don't fit on Page 1 move to Page 2, 3, etc.

### 2.2 Scroll Wheel Paging
*   **Interaction**: Scrolling the mouse wheel while the menu is open flips through pages of apps.
*   **Implementation**: `RadialMenuWindow` captures `PreviewMouseWheel` and routes it to `ViewModel.HandleMouseWheelMixed`.
*   **Visuals**: Center text updates to show "Page X" (except for Page 1 which shows "Switch").

### 2.3 Interaction Refinements
*   **Direct Switch**: Left-clicking an app with only **one** window now immediately switches to it, skipping the Drill-down animation.
*   **Drill-Down**: Left-clicking an app with **multiple** windows enters the Sub-Menu (Window Walker).
*   **No Smart Layout**: Removed the "Edge Clamping" logic. The menu now spawns exactly where triggered (usually centered on cursor), preventing unexpected shifts.

### 2.4 Flicker Fix
*   **Optimization**: `RadialMenuWindow.Summon` no longer calls `RefreshThemeOnShow` redundantly.
*   **Animation**: Initial `Opacity` is explicitly set to 0 before the Pop-in animation starts, ensuring a smooth entry without flash-frames.

## 3. Key Files Modified
*   `Pulsar/ViewModels/RadialMenuViewModel.cs`: Core logic for Paging, Pinned Slots, and Click Handling.
*   `Pulsar/Views/RadialMenuWindow.xaml.cs`: Mouse Wheel binding, Animation tuning, Layout simplifications.

## 4. How to Verify
1.  **Build & Run**: `dotnet run --project Pulsar/Pulsar/Pulsar.csproj`.
2.  **Test Pinned Slots**:
    *   Go to Settings -> Global Profile.
    *   Set Slot 1 to "Notepad" (Action: `com.pulsar.winswitcher`, Args: `app=Notepad`).
    *   Open Notepad and 8 other apps.
    *   Trigger Pulsar. Notepad should be at 12 o'clock (Slot 1).
3.  **Test Paging**:
    *   Open > 8 apps.
    *   Trigger Pulsar. Scroll Mouse Wheel. You should see new apps appear.
4.  **Test Interaction**:
    *   Open 1 Calculator. Click it. It should switch instantly.
    *   Open 2 Notepads. Click it. It should show the Sub-Menu.
