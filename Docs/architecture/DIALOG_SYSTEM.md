# Dialog System Architecture

**Status**: Published  
**Scope**: Architecture  
**Applies To**: All dialogs in Pulsar  
**Last Updated**: 2026-03-03

---

## Rule (TL;DR)

Pulsar uses a unified dialog architecture (v4.1.0+) where all dialogs are managed through `DialogService` and displayed in `DialogHostWindow`. Never create standalone `Window` classes for dialogs.

---

## Architecture Overview

```
DialogService (IDialogService)
    ↓
DialogHostWindow (FluentWindow container)
    ↓
ContentPresenter (dynamic ViewModel loading)
    ↓
Your Content (UserControl)
```

---

## Creating a New Dialog

### Step 1: Create ViewModel

Create a ViewModel implementing `IDialogViewModel` in `ViewModels/Dialogs/`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.ViewModels.Base;

public partial class MyDialogViewModel : ObservableObject, IDialogViewModel
{
    [ObservableProperty]
    private string _myProperty = string.Empty;

    public Action<Pulsar.Models.Enums.DialogResult>? RequestClose { get; set; }
    public bool IsScrollable => false;

    public Task<bool> CanCloseAsync(Pulsar.Models.Enums.DialogResult result)
    {
        return Task.FromResult(true);
    }
}
```

### Step 2: Create Content XAML

Create a UserControl in `Views/Dialogs/Contents/`:

```xml
<UserControl x:Class="Pulsar.Views.Dialogs.Contents.MyDialogContent"
             xmlns:vm="clr-namespace:Pulsar.ViewModels.Dialogs"
             d:DataContext="{d:DesignInstance Type=vm:MyDialogViewModel}">
    <StackPanel Margin="20">
        <TextBlock Text="{Binding MyProperty}"/>
    </StackPanel>
</UserControl>
```

### Step 3: Add DialogService Method (Optional)

For frequently used dialogs, add a convenience method to `IDialogService`:

```csharp
Task<string?> ShowMyDialogAsync(string title, string initialValue = "");
```

---

## Built-in Dialogs

- `ShowInputAsync()` - Text input dialog (Small size)
- `ShowColorPickerAsync()` - Color picker with RGB sliders and presets (Medium size)
- `ShowMessageAsync()` - Simple message box (Small size)
- `ShowConfirmationAsync()` - Yes/No confirmation (Small size)
- `ShowCustomAsync<T>()` - Generic dialog for any `IDialogViewModel` (configurable size)

---

## Dialog Features (Automatic)

- ✅ Theme inheritance (from SettingsWindow or global theme)
- ✅ Smart Owner detection (Active Window > SettingsWindow > MainWindow)
- ✅ Intelligent placement (CenterOwner/CenterScreen/NearMouse/CenterActiveWindow)
- ✅ Size presets (Small/Medium/Large/Auto/Default)
- ✅ Mica backdrop effect
- ✅ Keyboard navigation (Enter/Esc)

---

## Size Presets

```csharp
DialogSizeConstraints.Small   // 400x300 (input dialogs)
DialogSizeConstraints.Medium  // 600x450 (color picker, forms)
DialogSizeConstraints.Large   // 800x600 (complex content)
DialogSizeConstraints.Auto    // Size to content with min/max constraints
```

---

## Placement Strategies

```csharp
DialogPlacement.CenterOwner        // Relative to parent window (default)
DialogPlacement.CenterScreen       // Screen center
DialogPlacement.NearMouse          // Near cursor with boundary checks
DialogPlacement.CenterActiveWindow // Relative to active window
```

---

## Example Usage

```csharp
// Simple input
var name = await _dialogService.ShowInputAsync("Enter Name", "Please enter your name", "Default");

// Color picker
var color = await _dialogService.ShowColorPickerAsync("Pick Color", "#FF5733");

// Custom dialog
var myVm = new MyDialogViewModel();
var result = await _dialogService.ShowCustomAsync("My Dialog", myVm, DialogButtons.OkCancel);
```

---

## Migration Notes

- ❌ **Deprecated**: Creating standalone `Window` classes for dialogs
- ❌ **Deprecated**: Manual theme application with `IThemeService.ApplyTheme()`
- ❌ **Deprecated**: Manual Owner setting
- ✅ **Recommended**: Use `DialogService` for all dialogs

---

## Related Documents

- [UI Best Practices](../guides/UI_BEST_PRACTICES.md) - General UI guidelines
- [WPF Theme Injection Pitfalls](../lessons/WPF_THEME_INJECTION_PITFALLS.md) - Theme-related issues

---

**Change History**:
- v1.0.0 (2026-03-03): Initial extraction from AGENTS.md
