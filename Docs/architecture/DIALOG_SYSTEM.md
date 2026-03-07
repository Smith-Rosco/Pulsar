# Dialog System Architecture

**Status**: Published  
**Scope**: Architecture  
**Applies To**: All dialogs in Pulsar  
**Last Updated**: 2026-03-07

---

## Rule (TL;DR)

Pulsar uses a unified dialog architecture (v4.1.0+) where all dialogs are managed through `DialogService` and displayed in `DialogHostWindow`. Never create standalone `Window` classes for dialogs.

**Always choose the correct size preset** based on dialog content complexity.

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

## Size Presets (CRITICAL - Choose Wisely!)

| Preset | Size | Resizable | Maximize | Use Case | Example |
|--------|------|-----------|----------|----------|---------|
| **XSmall** | 350×200 | ❌ | ❌ | Simple confirmations | Yes/No, OK/Cancel |
| **Small** | 380×240 | ❌ | ❌ | Detailed confirmations, single input | SaveDontSaveCancel, text input |
| **Medium** | 600×450 | ❌ | ❌ | Forms, pickers | New Profile, Edit Secret, ColorPicker |
| **Large** | 800×600 | ✅ | ❌ | Complex content, lists | ProcessBlacklist, PluginLogs |
| **LargeResizable** | 800×600 | ✅ | ✅ | Large lists needing full screen | IconPicker, ProcessPicker |
| **Auto** | Dynamic | ❌ | ❌ | Content-sized with constraints | Rare use |

### Size Selection Decision Tree

```
Is it a simple Yes/No or OK/Cancel?
  → XSmall

Is it a confirmation with details (SaveDontSaveCancel) or single input?
  → Small

Is it a form or picker with moderate content?
  → Medium

Is it a list or complex content that users might want to resize?
  → Large (if no maximize needed)
  → LargeResizable (if maximize needed for viewing many items)
```

---

## Visual Features (Automatic)

### Icon Display (v4.2.0+)

Dialogs automatically display icons based on `DialogType` for string content:

- **Info**: ℹ️ Blue (#0078D4)
- **Warning**: ⚠️ Orange (#FFA500)
- **Error**: ❌ Red (#D13438)
- **Success**: ✅ Green (#107C10)

Icons appear in the title bar area, next to the message content.

### Button Semantics (v4.2.0+)

- **Primary Button**: Blue (Save, OK, Yes)
- **Secondary Button**: Gray (Cancel)
- **Tertiary Button**: 
  - Red/Danger style for destructive actions ("Don't Save")
  - Gray for neutral actions ("No")

---

## Built-in Dialogs

### ShowConfirmationAsync()
```csharp
var result = await _dialogService.ShowConfirmationAsync(
    "Delete Profile", 
    "Are you sure you want to delete this profile?",
    "Delete", 
    "Cancel");
```
- **Size**: XSmall (350×200)
- **Use**: Simple Yes/No confirmations

### ShowMessageAsync()
```csharp
var result = await _dialogService.ShowMessageAsync(
    "Unsaved Changes",
    "You have unsaved changes. Do you want to save before closing?",
    DialogType.Warning,
    DialogButtons.SaveDontSaveCancel);
```
- **Size**: XSmall for simple messages, Small for SaveDontSaveCancel
- **Features**: Automatic icon display, Danger button for "Don't Save"

### ShowInputAsync()
```csharp
var name = await _dialogService.ShowInputAsync(
    "Enter Name", 
    "Please enter your name", 
    "Default");
```
- **Size**: Small (380×240)
- **Use**: Single text input

### ShowColorPickerAsync()
```csharp
var color = await _dialogService.ShowColorPickerAsync("Pick Color", "#FF5733");
```
- **Size**: Medium (600×450)
- **Use**: Color selection with RGB sliders

### ShowCustomAsync()
```csharp
// Without size constraint (uses Medium by default)
var result = await _dialogService.ShowCustomAsync(
    "My Dialog", 
    myViewModel, 
    DialogButtons.OkCancel);

// With explicit size constraint (RECOMMENDED)
var result = await _dialogService.ShowCustomAsync(
    "Select Icon", 
    iconPickerVm, 
    DialogButtons.OkCancel,
    DialogSizeConstraints.LargeResizable);
```
- **Size**: Medium by default, **always specify for custom dialogs**
- **Use**: Any custom ViewModel

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
    public bool IsScrollable => false; // Set to true if content might overflow

    public Task<bool> CanCloseAsync(Pulsar.Models.Enums.DialogResult result)
    {
        // Validate before closing
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

### Step 3: Register DataTemplate (if needed)

Add to `DialogHostWindow.xaml` Resources:

```xml
<DataTemplate DataType="{x:Type dialogs:MyDialogViewModel}">
    <contents:MyDialogContent/>
</DataTemplate>
```

### Step 4: Call Dialog with Correct Size

```csharp
var vm = new MyDialogViewModel();
var result = await _dialogService.ShowCustomAsync(
    "My Dialog", 
    vm, 
    DialogButtons.OkCancel,
    DialogSizeConstraints.Medium); // Choose appropriate size!
```

---

## Common Patterns

### List Selection Dialog
```csharp
// Use LargeResizable for lists with many items
var picker = new ProcessPickerViewModel(_windowService);
var result = await _dialogService.ShowCustomAsync(
    "Select Application", 
    picker, 
    DialogButtons.OkCancel,
    DialogSizeConstraints.LargeResizable);
```

### Form Dialog
```csharp
// Use Medium for forms
var vm = new InputProfileViewModel(...);
var result = await _dialogService.ShowCustomAsync(
    "New Profile", 
    vm, 
    DialogButtons.OkCancel,
    DialogSizeConstraints.Medium);
```

### Confirmation with Destructive Action
```csharp
// Use ShowMessageAsync with Warning type for destructive confirmations
var result = await _dialogService.ShowMessageAsync(
    "Delete Item",
    "This action cannot be undone. Continue?",
    DialogType.Warning,
    DialogButtons.YesNo);
```

---

## Dialog Features (Automatic)

- ✅ Theme inheritance (from SettingsWindow or global theme)
- ✅ Smart Owner detection (Active Window > SettingsWindow > MainWindow)
- ✅ Intelligent placement (CenterOwner/CenterScreen/NearMouse/CenterActiveWindow)
- ✅ Icon display based on DialogType (Info/Warning/Error/Success)
- ✅ Danger button styling for destructive actions
- ✅ Mica backdrop effect
- ✅ Keyboard navigation (Enter/Esc)
- ✅ Automatic maximize button hiding (except LargeResizable)

---

## Placement Strategies

```csharp
DialogPlacement.CenterOwner        // Relative to parent window (default)
DialogPlacement.CenterScreen       // Screen center
DialogPlacement.NearMouse          // Near cursor with boundary checks
DialogPlacement.CenterActiveWindow // Relative to active window
```

---

## Migration Notes

- ❌ **Deprecated**: Creating standalone `Window` classes for dialogs
- ❌ **Deprecated**: Manual theme application with `IThemeService.ApplyTheme()`
- ❌ **Deprecated**: Manual Owner setting
- ❌ **Deprecated**: Using default Medium size for all custom dialogs
- ✅ **Recommended**: Use `DialogService` for all dialogs
- ✅ **Recommended**: Always specify `DialogSizeConstraints` for custom dialogs
- ✅ **Recommended**: Use `ShowMessageAsync` with `DialogType` for better UX

---

## Troubleshooting

### Dialog is too large for simple confirmation
**Solution**: Use `ShowConfirmationAsync()` or `ShowMessageAsync()` instead of custom dialog.

### List content is cramped
**Solution**: Use `DialogSizeConstraints.LargeResizable` to allow maximizing.

### Icons not showing
**Check**: Icons only show for string content in `ShowMessageAsync()`, not custom ViewModels.

### Maximize button visible when it shouldn't be
**Check**: Ensure you're using the correct size preset (not LargeResizable).

---

## Related Documents

- [UI Best Practices](../guides/UI_BEST_PRACTICES.md) - General UI guidelines
- [WPF Theme Injection Pitfalls](../lessons/WPF_THEME_INJECTION_PITFALLS.md) - Theme-related issues
- [Component Library](../guides/COMPONENT_LIBRARY.md) - Reusable UI components

---

**Change History**:
- v1.1.0 (2026-03-07): Added XSmall/LargeResizable presets, icon display, button semantics, size selection guide
- v1.0.0 (2026-03-03): Initial extraction from AGENTS.md
