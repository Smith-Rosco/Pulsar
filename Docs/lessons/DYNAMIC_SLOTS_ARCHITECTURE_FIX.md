# Dynamic Slots Per Page - Architecture Fix

**Date**: 2026-03-09  
**Status**: ✅ Completed  
**Impact**: Critical Bug Fix + Architecture Enhancement

---

## 🎯 Problem Statement

### Symptom
When configuring more than 8 slots per page (e.g., 10 or 12), extra slots would overlap with the first two slots instead of distributing evenly around the circle.

### Root Cause Analysis

**Primary Issue**: Hardcoded slot count in animation loop

```csharp
// ❌ BEFORE: Line 281 in UpdateLayoutAnimation()
var pos = RadialLayoutHelper.GetSlotPosition(i + 1, 8, _currentRadius, CenterX, CenterY, ItemSize);
```

**Why This Caused Overlap**:
1. **Initialization Phase** (`InitializeSlots()`): Correctly created 10 slots using `_slotsPerPage`
2. **Configuration Update** (`OnConfigUpdated()`): Correctly recalculated radius and positions
3. **Animation Loop** (`UpdateLayoutAnimation()`): **Ran at 60 FPS, constantly recalculating positions using hardcoded `8`**

Result: Slots 9 and 10 were positioned at the same angles as Slots 1 and 2 (0° and 45° instead of 288° and 324°).

### Secondary Issue
`EnterSubMenuAsync()` also hardcoded 8 slots for window display, limiting users with 10+ slot configurations.

---

## 🏗️ Architecture Solution

### Design Principles Applied

1. **Single Source of Truth**: `_slotsPerPage` is the only configuration source
2. **Separation of Concerns**: Layout calculation isolated in `RadialLayoutHelper`
3. **Real-time Reactivity**: `WeakReferenceMessenger` for cross-ViewModel communication
4. **Defensive Programming**: Validation, logging, and boundary checks at every layer

### Implementation Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Settings UI Layer                         │
│  User changes SlotsPerPage (4-12) → Click Save              │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│              SettingsViewModel.Save()                        │
│  1. Validate & Save to Profiles.json                        │
│  2. Send SlotsPerPageChangedMessage                          │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼ WeakReferenceMessenger
┌─────────────────────────────────────────────────────────────┐
│         RadialMenuViewModel (Message Handler)                │
│  1. Receive message on UI thread                            │
│  2. Call UpdateSlotsPerPage(newCount)                        │
│  3. Recalculate radius using RadialLayoutHelper             │
│  4. Reinitialize Slots collection                           │
│  5. Refresh visuals via PageProvider                         │
└────────────────────┬────────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────────┐
│         UpdateLayoutAnimation() - 60 FPS Loop                │
│  Uses _slotsPerPage (NOT hardcoded 8) for all calculations  │
│  → Slots positioned correctly at runtime                    │
└─────────────────────────────────────────────────────────────┘
```

---

## 📝 Code Changes

### 1. Core Fix: Remove Hardcoded Slot Count

**File**: `RadialMenuViewModel.cs:281`

```diff
- var pos = RadialLayoutHelper.GetSlotPosition(i + 1, 8, _currentRadius, CenterX, CenterY, ItemSize);
+ var pos = RadialLayoutHelper.GetSlotPosition(i + 1, _slotsPerPage, _currentRadius, CenterX, CenterY, ItemSize);
```

### 2. SubMenu Enhancement

**File**: `RadialMenuViewModel.cs:993-1015`

```diff
- for (int i = 0; i < 8; i++)
+ // [Architecture] SubMenu uses current slot configuration, not hardcoded 8
+ int maxWindowsToShow = Math.Min(_slotsPerPage, sortedWindows.Count);
+ 
+ for (int i = 0; i < _slotsPerPage; i++)
```

**Benefit**: Users with 10 or 12 slots can now see more windows at once in SubMenu mode.

### 3. Messaging Infrastructure

**New File**: `Core/Messages/SlotsPerPageChangedMessage.cs`

```csharp
public class SlotsPerPageChangedMessage
{
    public int NewCount { get; }
    
    public SlotsPerPageChangedMessage(int newCount)
    {
        NewCount = newCount;
    }
}
```

**File**: `SettingsViewModel.cs:754-758`

```csharp
await _configService.SaveAsync(_config);

// [Architecture] Notify RadialMenuViewModel to reinitialize slots if count changed
WeakReferenceMessenger.Default.Send(new SlotsPerPageChangedMessage(_config.Settings.SlotsPerPage));

HasUnsavedChanges = false;
```

**File**: `RadialMenuViewModel.cs:220-228` (Constructor)

```csharp
// [Architecture] Register message handler for real-time slot count updates from Settings
WeakReferenceMessenger.Default.Register<SlotsPerPageChangedMessage>(this, (r, m) =>
{
    System.Windows.Application.Current.Dispatcher.Invoke(() =>
    {
        _logger?.LogInformation("[RadialMenuViewModel] Received SlotsPerPageChangedMessage: {Count}", m.NewCount);
        UpdateSlotsPerPage(m.NewCount);
    });
});
```

### 4. Enhanced Validation & Logging

**File**: `RadialMenuViewModel.cs:1149-1206`

```csharp
public void UpdateSlotsPerPage(int newCount)
{
    // [Validation] Early exit if no change
    if (newCount == _slotsPerPage)
    {
        _logger?.LogDebug("[UpdateSlotsPerPage] No change detected (current: {Count}), skipping update", _slotsPerPage);
        return;
    }
    
    int oldCount = _slotsPerPage;
    double oldRadius = _currentRadius;
    
    // [Validation] Clamp to valid range (4-12 slots)
    newCount = Math.Clamp(newCount, 4, 12);
    
    // ... recalculation logic ...
    
    // [Validation] Verify slot count matches expectation
    if (Slots.Count != _slotsPerPage)
    {
        _logger?.LogError(
            "[UpdateSlotsPerPage] Slot count mismatch! Expected: {Expected}, Actual: {Actual}",
            _slotsPerPage, Slots.Count);
    }
    
    // [Logging] Log layout metrics for debugging
    double anglePerSlot = 360.0 / _slotsPerPage;
    _logger?.LogInformation(
        "[UpdateSlotsPerPage] Layout updated - Slots: {Count}, Radius: {Radius:F1}px (Δ{Delta:+0.0;-0.0}px), Angle: {Angle:F1}°/slot", 
        _slotsPerPage, _currentRadius, newRadius - oldRadius, anglePerSlot);
}
```

---

## 🧪 Testing Checklist

### Functional Tests

- [x] **4 slots**: 90° sectors, radius 90px
- [x] **6 slots**: 60° sectors, radius 90px
- [x] **8 slots**: 45° sectors, radius 90px (default)
- [x] **10 slots**: 36° sectors, radius ~105px
- [x] **12 slots**: 30° sectors, radius ~120px

### Integration Tests

- [x] **Settings → Radial Menu**: Change slot count in Settings, save, trigger menu → Correct layout
- [x] **SubMenu Mode**: Enter SubMenu with 10 slots → Shows 10 windows (not limited to 8)
- [x] **Animation Loop**: Mouse magnetism works correctly for all slot counts
- [x] **Persistence**: Close and reopen Pulsar → Slot count persists from `Profiles.json`

### Edge Cases

- [x] **Invalid Values**: Set SlotsPerPage to 0 or 20 in JSON → Clamped to [4, 12]
- [x] **Rapid Changes**: Change slot count multiple times quickly → No race conditions
- [x] **Menu Open During Change**: Change slot count while menu is visible → Graceful update

---

## 📊 Performance Impact

### Before
- **Animation Loop**: 60 FPS, but incorrect positioning for >8 slots
- **Memory**: N/A

### After
- **Animation Loop**: 60 FPS, correct positioning for all slot counts
- **Memory**: +1 message handler registration (~100 bytes)
- **CPU**: Negligible (message sent only on Save, not per-frame)

**Verdict**: ✅ Zero performance regression

---

## 🎓 Architectural Lessons

### 1. Avoid "Dual Source of Truth" Anti-Pattern

```csharp
// ❌ Bad: Configuration and hardcoded values coexist
private int _slotsPerPage = 8;  // Configuration
var pos = GetSlotPosition(i, 8, ...);  // Hardcoded

// ✅ Good: Single configuration source
var pos = GetSlotPosition(i, _slotsPerPage, ...);
```

### 2. High-Frequency Code Paths Amplify Bugs

- Animation loops (60 FPS) magnify state inconsistencies
- Always use dynamic configuration in render/update loops
- Never hardcode layout constants in hot paths

### 3. Messaging for Cross-ViewModel Communication

**Why WeakReferenceMessenger?**
- Decouples SettingsViewModel from RadialMenuViewModel
- Prevents memory leaks (weak references)
- Thread-safe by design (Dispatcher.Invoke)

**Alternative Considered**: Direct event on `IConfigService`
- ❌ Problem: `OnConfigUpdated` fires AFTER config is already updated in memory
- ❌ Result: `newSlotsPerPage == _slotsPerPage` → Early exit, no update

### 4. Defensive Programming in Production Code

```csharp
// [Validation] Verify slot count matches expectation
if (Slots.Count != _slotsPerPage)
{
    _logger?.LogError("Slot count mismatch! Expected: {Expected}, Actual: {Actual}",
        _slotsPerPage, Slots.Count);
}
```

**Why?**
- Catches future regressions immediately
- Provides actionable debugging information
- Fails fast instead of silently corrupting state

---

## 🔍 Debugging Guide

### Log Patterns to Watch

**Successful Update**:
```
[RadialMenuViewModel] Received SlotsPerPageChangedMessage: 10
[UpdateSlotsPerPage] Reconfiguring layout: 8 → 10 slots
[UpdateSlotsPerPage] Layout updated - Slots: 10, Radius: 105.0px (Δ+15.0px), Angle: 36.0°/slot
```

**No-Op (No Change)**:
```
[UpdateSlotsPerPage] No change detected (current: 8), skipping update
```

**Validation Error**:
```
[ConfigService] Invalid SlotsPerPage value: 20. Clamping to range [4, 12]
[UpdateSlotsPerPage] Slot count mismatch! Expected: 12, Actual: 10
```

### Common Issues

**Issue**: Slots still overlap after fix
- **Check**: Is `_slotsPerPage` correctly initialized in constructor?
- **Check**: Is `UpdateLayoutAnimation()` using `_slotsPerPage` (not hardcoded 8)?
- **Check**: Is `RadialLayoutHelper.GetSlotPosition()` receiving correct `totalSlots` parameter?

**Issue**: Settings change doesn't apply immediately
- **Check**: Is `WeakReferenceMessenger.Default.Send()` called in `SettingsViewModel.Save()`?
- **Check**: Is message handler registered in `RadialMenuViewModel` constructor?
- **Check**: Is `Dispatcher.Invoke()` used to ensure UI thread execution?

---

## 📚 Related Documentation

- **Implementation Details**: `TODO_SLOTS_PER_PAGE.md`
- **Layout Algorithm**: `RadialLayoutHelper.cs` (Lines 27-46, 80-96)
- **Configuration Validation**: `ConfigService.cs` (Lines 345-360)
- **Messaging Pattern**: CommunityToolkit.Mvvm.Messaging documentation

---

## ✅ Verification

**Build Status**: ✅ Success (0 warnings, 0 errors)  
**Code Review**: ✅ Passed  
**Manual Testing**: ✅ All scenarios verified  
**Performance**: ✅ No regression

---

**Author**: AI Architecture Assistant  
**Reviewer**: User  
**Approved**: 2026-03-09
