# Project Handover Document

**Date:** Feb 11, 2026
**Project:** Pulsar (Radial Menu Launcher)
**Status:** In Development (Features Partially Implemented)

## 1. Overview
Pulsar is a high-performance productivity launcher for Windows featuring a radial menu interface, built with .NET 8.0 and WPF.

## 2. Recent Implementation Efforts

Two major features were recently attempted:

### A. Quick Switch (Alt-Tab Behavior)
- **Goal:** Pressing and releasing the switcher hotkey (Ctrl+Q) quickly (< 250ms) without selection should switch to the *previous* window.
- **Current State:** Implemented in `RadialMenuViewModel` (timer logic) and `WindowService` (Z-Order traversal).
- **Reported Issue:** User reports focus is incorrect or switches to the wrong window (stuck on current).
- **Code Locations:** 
  - `Services/WindowService.cs`: `SwitchToPreviousWindow()` logic using `GetWindow(GW_HWNDNEXT)`.
  - `ViewModels/RadialMenuViewModel.cs`: `HandleKeyUp` with `_showStartTime` check.

### B. Smart Profile Creator
- **Goal:** In an unconfigured app, show a "Create Config" slot (Slot 1) to auto-create a profile.
- **Current State:** Implemented in `RadialMenuViewModel.Show()` and `CreateProfileStrategy`.
- **Reported Issue:** The slot does not appear as expected when invoking the command menu.
- **Code Locations:**
  - `ViewModels/RadialMenuViewModel.cs`: Logic in `Show()` method to inject `internal:create_profile` slot.
  - `ViewModels/Strategies/CreateProfileStrategy.cs`: Logic to create profile and open settings.

## 3. Known Issues & Debugging Guide

### Issue 1: Quick Switch Fails
- **Symptoms:** Switches to current window or unpredictable window.
- **Hypothesis:** 
  - `GetForegroundWindow` might be returning the Pulsar window itself (since it's active when key is released), causing the "Next" window logic to fail or pick the wrong one.
  - Z-Order traversal might be picking invisible or tool windows despite `IsAltTabWindow` checks.
- **Next Steps:** 
  - Debug `WindowService.SwitchToPreviousWindow` to see what `currentForeground` is.
  - Ensure Pulsar window is excluded or handled correctly in Z-Order search.

### Issue 2: Smart Slot Missing
- **Symptoms:** Slot 1 remains empty or shows Global items instead of "Add Profile".
- **Hypothesis:** 
  - The condition `!foundProfile` might be evaluating incorrectly.
  - The fallback to "Global" profile might be filling Slot 1 before the check.
  - `_currentSlots.Any(s => s.Slot == 1)` check might be true if Global profile has a slot 1.
- **Next Steps:**
  - Verify priority logic in `RadialMenuViewModel.Show()`.
  - Ensure "Smart Slot" injection happens *before* or *over* Global fallback if intended.

## 4. Build Status
- **Compilation:** Successful (`dotnet build` passes).
- **Warnings:** Minor nullability warnings in `RadialMenuViewModel.cs`.

## 5. Key Files Modified
- `Pulsar/Pulsar/Services/WindowService.cs`
- `Pulsar/Pulsar/Services/Interfaces/IWindowService.cs`
- `Pulsar/Pulsar/ViewModels/RadialMenuViewModel.cs`
- `Pulsar/Pulsar/ViewModels/Strategies/CreateProfileStrategy.cs`

---
*Prepared by Antigravity Agent*
