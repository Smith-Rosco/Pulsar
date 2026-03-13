# Dynamic Slots Per Page - Implementation Lessons

**Date**: 2026-03-09  
**Feature**: Customizable Slots Per Page (4-12)  
**Status**: Partially Complete - UI Working, Runtime Update Pending

---

## 📋 Feature Overview

Implemented a feature allowing users to customize the number of slots displayed per page in the Radial Menu (4-12 slots), with automatic geometric adaptation.

### Completed Components

✅ **Phase 1**: Configuration Infrastructure
- Added `SlotsPerPage` property to `ProfileSettings` (default: 8)
- Implemented `IConfigService.GetValidatedSlotsPerPage()` with range validation (4-12)
- Configuration persists to `Profiles.json`

✅ **Phase 2**: Adaptive Geometry Calculation
- Implemented `RadialLayoutHelper.CalculateOptimalRadius()` - auto-calculates radius based on slot count
- Implemented `RadialLayoutHelper.CalculateDeadZoneRatio()` - dynamic dead zone adjustment
- Geometry adapts automatically: 4-6 slots (90px), 8 slots (90px), 10 slots (105px), 12 slots (120px)

✅ **Phase 3**: RadialMenuViewModel Refactoring
- Added `_slotsPerPage` field, loaded from config
- `InitializeSlots()` uses dynamic slot count
- `HandleMouseMove()` uses dynamic dead zone and slot count
- Added `UpdateSlotsPerPage()` method for runtime updates

✅ **Phase 4**: PageProvider Adaptation
- `BasePageProvider` uses dynamic `ItemsPerPage` from `IConfigService`
- `CommandPageProvider` and `ProcessPageProvider` adapted to use dynamic pagination

✅ **Phase 5**: Settings UI
- Added "Radial Menu Layout" section in Settings → General
- `ui:NumberBox` with range 4-12, real-time preview text
- Proper MVVM binding with `ObservableObject`

---

## 🐛 Critical Issues Encountered

### Issue 1: Slot Overlap After Config Change

**Symptom**: When changing from 8 to 10 slots, new slots overlay on top of existing slots.

**Root Cause**: 
- `Slots` collection initialized once in constructor with 8 elements
- When config changes to 10 slots, `OnConfigUpdated()` reloads config but doesn't reinitialize `Slots`
- `PageProvider` tries to fill 10 slots but only 8 `SlotViewModel` objects exist
- Result: First 8 positions filled correctly, slots 9-10 have nowhere to go

**Solution Implemented**:
```csharp
private async void OnConfigUpdated()
{
    _config = await _configService.LoadAsync();
    
    // Check if slots per page has changed
    int newSlotsPerPage = _configService.GetValidatedSlotsPerPage();
    if (newSlotsPerPage != _slotsPerPage)
    {
        _slotsPerPage = newSlotsPerPage;
        
        // Recalculate optimal radius
        double newRadius = RadialLayoutHelper.CalculateOptimalRadius(_slotsPerPage);
        _currentRadius = newRadius;
        _animTargetRadius = newRadius;
        
        // Reinitialize slots collection
        Slots.Clear();
        for (int i = 1; i <= _slotsPerPage; i++)
        {
            var pos = RadialLayoutHelper.GetSlotPosition(i, _slotsPerPage, _currentRadius, CenterX, CenterY, ItemSize);
            Slots.Add(new SlotViewModel(i, pos.X, pos.Y, ItemSize));
        }
        
        // Refresh if visible
        if (IsVisible && _pageProvider != null)
        {
            _pageProvider.RefreshVisuals(Slots, CenterSlot);
        }
    }
}
```

**Status**: ⚠️ **INCOMPLETE** - This only works when config file changes externally. When user saves in Settings, config is already updated in memory, so `ConfigUpdated` event doesn't fire again.

---

### Issue 2: Save Button Not Clickable

**Symptom**: After modifying `SlotsPerPage` in Settings, Save button remains disabled.

**Root Cause Chain**:

1. **Binding Path Error**:
   - XAML: `<StackPanel DataContext="{Binding GeneralSettings}">`
   - Initial binding: `Value="{Binding DataContext.SlotsPerPage, RelativeSource={RelativeSource AncestorType=Page}}"`
   - This tries to find `SlotsPerPage` on `SettingsViewModel`, but it should be on `GeneralSettings`
   - **Result**: Binding fails, setter never called

2. **Property Location Confusion**:
   - `SlotsPerPage` defined in `ProfileSettings` (model)
   - `SettingsViewModel` also defined a wrapper property
   - Caused binding ambiguity

3. **CanExecute Syntax Error**:
   - Original: `[RelayCommand(CanExecute = nameof(HasUnsavedChanges))]`
   - **Wrong**: `CanExecute` requires a **method**, not a property
   - CommunityToolkit.Mvvm couldn't evaluate, command always disabled

**Solution Implemented**:

**Step 1**: Make `ProfileSettings` observable
```csharp
public partial class ProfileSettings : ObservableObject
{
    [ObservableProperty]
    private int _slotsPerPage = 8;
}
```

**Step 2**: Fix XAML binding (bind directly to current DataContext)
```xml
<ui:NumberBox Value="{Binding SlotsPerPage, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
              Minimum="4"
              Maximum="12"/>
```

**Step 3**: Listen to property changes in ViewModel
```csharp
public ProfileSettings GeneralSettings
{
    get => _generalSettings;
    set
    {
        if (_generalSettings != null)
            _generalSettings.PropertyChanged -= OnGeneralSettingsPropertyChanged;
        
        if (SetProperty(ref _generalSettings, value))
        {
            if (_generalSettings != null)
                _generalSettings.PropertyChanged += OnGeneralSettingsPropertyChanged;
        }
    }
}

private void OnGeneralSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    if (e.PropertyName == nameof(ProfileSettings.SlotsPerPage))
    {
        OnPropertyChanged(nameof(SlotsPerPagePreview));
        MarkDirty();
    }
}
```

**Step 4**: Fix CanExecute syntax
```csharp
private bool CanSave() => HasUnsavedChanges;

[RelayCommand(CanExecute = nameof(CanSave))]
public async Task Save() { ... }
```

**Status**: ✅ **RESOLVED**

---

## 🚨 Outstanding Issue: Runtime Slot Update

### Problem
When user saves config in Settings:
1. Config is updated in memory (`_config.Settings.SlotsPerPage = 10`)
2. `ConfigService.SaveAsync()` writes to disk
3. `ConfigUpdated` event fires
4. `OnConfigUpdated()` reloads config from disk
5. **BUT**: Config in memory is already the new value, so `newSlotsPerPage != _slotsPerPage` is FALSE
6. Slots collection is NOT reinitialized
7. Next time Radial Menu opens, still shows 8 slots with overlapping content

### Root Cause
`OnConfigUpdated()` only detects changes by comparing new config value with cached `_slotsPerPage`. But when the change originates from Settings UI, both are already updated before the event fires.

### Proposed Solutions

#### Option A: Force Reload on Save (Recommended)
Modify `SettingsViewModel.Save()` to explicitly notify `RadialMenuViewModel`:

```csharp
// In SettingsViewModel.Save()
await _configService.SaveAsync(_config);

// Notify RadialMenuViewModel to reinitialize
WeakReferenceMessenger.Default.Send(new SlotsPerPageChangedMessage(_config.Settings.SlotsPerPage));
```

```csharp
// In RadialMenuViewModel constructor
WeakReferenceMessenger.Default.Register<SlotsPerPageChangedMessage>(this, (r, m) =>
{
    Application.Current.Dispatcher.Invoke(() =>
    {
        UpdateSlotsPerPage(m.NewCount);
    });
});
```

#### Option B: Always Reinitialize on ConfigUpdated
Remove the comparison check, always reinitialize:

```csharp
private async void OnConfigUpdated()
{
    _config = await _configService.LoadAsync();
    int newSlotsPerPage = _configService.GetValidatedSlotsPerPage();
    
    // Always update, even if value appears same
    _slotsPerPage = newSlotsPerPage;
    _currentRadius = RadialLayoutHelper.CalculateOptimalRadius(_slotsPerPage);
    _animTargetRadius = _currentRadius;
    
    // Always reinitialize slots
    Slots.Clear();
    for (int i = 1; i <= _slotsPerPage; i++)
    {
        var pos = RadialLayoutHelper.GetSlotPosition(i, _slotsPerPage, _currentRadius, CenterX, CenterY, ItemSize);
        Slots.Add(new SlotViewModel(i, pos.X, pos.Y, ItemSize));
    }
}
```

**Pros**: Simple, no messaging needed  
**Cons**: Unnecessary reinitialization on every config change

#### Option C: Track Dirty State in ConfigService
Add a flag to track whether `SlotsPerPage` specifically changed:

```csharp
// In ConfigService
private int _lastSlotsPerPage = 8;

public async Task SaveAsync(ProfilesConfig config)
{
    bool slotsChanged = config.Settings.SlotsPerPage != _lastSlotsPerPage;
    _lastSlotsPerPage = config.Settings.SlotsPerPage;
    
    // ... save logic ...
    
    if (slotsChanged)
    {
        SlotsPerPageChanged?.Invoke(config.Settings.SlotsPerPage);
    }
    
    ConfigUpdated?.Invoke();
}
```

---

## 📚 Key Architectural Lessons

### 1. WPF DataContext Binding Pitfalls

**Problem**: Nested DataContext can break bindings.

```xml
<!-- WRONG: Tries to find property on wrong object -->
<StackPanel DataContext="{Binding GeneralSettings}">
    <Control Value="{Binding DataContext.SomeProperty, RelativeSource={RelativeSource AncestorType=Page}}"/>
</StackPanel>

<!-- CORRECT: Bind directly to current DataContext -->
<StackPanel DataContext="{Binding GeneralSettings}">
    <Control Value="{Binding SomeProperty}"/>
</StackPanel>
```

**Lesson**: Always verify binding path matches actual DataContext hierarchy.

### 2. RelayCommand CanExecute Syntax

**Wrong**:
```csharp
[RelayCommand(CanExecute = nameof(HasUnsavedChanges))] // Property name
```

**Correct**:
```csharp
private bool CanSave() => HasUnsavedChanges;

[RelayCommand(CanExecute = nameof(CanSave))] // Method name
```

**Lesson**: `CanExecute` must reference a method returning `bool`, not a property.

### 3. ObservableCollection Initialization Timing

**Problem**: Collections initialized once in constructor don't adapt to config changes.

**Solution**: 
- Listen to config change events
- Detect relevant property changes
- Reinitialize collections when needed
- Consider using `ObservableObject` for config models

### 4. Event-Driven Architecture Gaps

**Problem**: `ConfigUpdated` event fires after save, but config is already updated in memory.

**Lesson**: 
- Event-driven updates work for external changes (file edits, other processes)
- For internal changes (Settings UI), need explicit notification or always-reinitialize strategy
- Consider using messaging pattern (WeakReferenceMessenger) for cross-ViewModel communication

---

## 🔧 Files Modified

### Core Logic
- `Pulsar/Helpers/RadialLayoutHelper.cs` - Added `CalculateOptimalRadius()`, `CalculateDeadZoneRatio()`
- `Pulsar/ViewModels/RadialMenuViewModel.cs` - Dynamic slot initialization, `OnConfigUpdated()` enhancement
- `Pulsar/ViewModels/Strategies/IPageProvider.cs` - `BasePageProvider` with dynamic `ItemsPerPage`
- `Pulsar/ViewModels/Strategies/CommandPageProvider.cs` - Uses dynamic pagination
- `Pulsar/ViewModels/Strategies/ProcessPageProvider.cs` - Uses dynamic pagination

### Configuration
- `Pulsar/Models/ProfilesConfig.cs` - `ProfileSettings` inherits `ObservableObject`, `SlotsPerPage` property
- `Pulsar/Services/Interfaces/IConfigService.cs` - Added `GetValidatedSlotsPerPage()`, `SetSlotsPerPage()`
- `Pulsar/Services/ConfigService.cs` - Validation and persistence logic

### UI
- `Pulsar/ViewModels/SettingsViewModel.cs` - Property change listening, `CanSave()` method, `SlotsPerPagePreview`
- `Pulsar/Views/Pages/SettingsGeneralPage.xaml` - "Radial Menu Layout" section with `NumberBox`

---

## 🎯 Next Steps for Completion

1. **Implement Runtime Slot Update** (Choose Option A, B, or C above)
2. **Test All Scenarios**:
   - Change from 8 → 10, save, trigger menu (should show 10 slots)
   - Change from 10 → 6, save, trigger menu (should show 6 slots)
   - Change from 8 → 12, save, trigger menu (should show 12 slots with expanded radius)
   - Verify no overlapping at any slot count
3. **Add Unit Tests** for `CalculateOptimalRadius()` and `CalculateDeadZoneRatio()`
4. **Performance Testing**: Ensure no lag when switching between different slot counts
5. **Documentation**: Update user-facing docs with new feature

---

## 📊 Verification Checklist

- [x] Config persists to `Profiles.json`
- [x] Settings UI shows current value
- [x] Settings UI updates preview text in real-time
- [x] Save button enables when value changes
- [x] Save button saves successfully
- [ ] **Radial Menu shows correct slot count after save** ⚠️ **PENDING**
- [ ] **No slot overlap at any count (4-12)** ⚠️ **PENDING**
- [x] Radius adapts automatically
- [x] Dead zone ratio adjusts for high slot counts
- [x] Trigger regions accurate

---

## 🔗 Related Documentation

- [PLUGIN_SYSTEM.md](../architecture/PLUGIN_SYSTEM.md) - Plugin architecture context
- [UI_BEST_PRACTICES.md](../guides/UI_BEST_PRACTICES.md) - WPF binding patterns
- [WPFUI_BUTTON_PRIMARY_BUG.md](./WPFUI_BUTTON_PRIMARY_BUG.md) - Similar UI binding issue
- [WPF_THEME_INJECTION_PITFALLS.md](./WPF_THEME_INJECTION_PITFALLS.md) - DataContext timing issues

---

**Last Updated**: 2026-03-09  
**Author**: AI Assistant  
**Status**: Requires completion of runtime slot update mechanism
