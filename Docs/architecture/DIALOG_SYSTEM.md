# Dialog System Architecture

**Status**: Published  
**Scope**: Architecture  
**Applies To**: All dialogs in Pulsar  
**Last Updated**: 2026-09-11

---

## Rule (TL;DR)

Pulsar uses a unified dialog architecture (v4.1.0+) where all dialogs are managed through `DialogService` and displayed in `DialogHostWindow`. Never create standalone `Window` classes for dialogs.

**Register every dialog in the dialog catalog** ([ADR-033](../decisions/033-dialog-catalog-single-registration-surface.md)): one row in `Services/DialogCatalog.cs` owns the title key, content type, size preset, buttons and theme. Call sites show the dialog with `ShowCustomAsync(DialogId.X, content)` and choose **none** of them. Choosing a title key or a `DialogSizeConstraints` preset at the call site is deprecated.

**Heavy configuration belongs in settings transient pages, not modals** (ADR-029, openspec `2026-09-08-dynamic-settings-tabs`): rich, revisited configuration (gesture summon, plugin settings, process blacklist) migrates to sidebar transient pages / dedicated settings pages. Dialogs remain the right tool for one-shot confirmations, pickers (Icon/Process/Secret/Color) and warnings — not for long-form configuration editing.

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
// RECOMMENDED — a registered dialog. Title, size, buttons and theme come from the
// catalog row; the call site supplies only the id and the content view model.
var result = await _dialogService.ShowCustomAsync(DialogIds.PickIcon, iconPickerVm);

// Registered dialog whose title is a composite format string (row TitleIsFormat = true):
// the trailing arguments feed the row's {0} placeholder.
var result = await _dialogService.ShowCustomAsync(DialogIds.PluginLogs, logsVm, pluginName);
```
- **Size / title / buttons**: owned by the catalog row — never chosen at the call site
- **Use**: every dialog that has a catalog row (i.e. all of them)

The `string`-keyed overloads still exist for dynamic titles and one-off sizes;
`ShowMessageAsync` / `ShowConfirmationAsync` / `ShowInputAsync` /
`ShowColorPickerAsync` are built on them and are not catalog dialogs (a message
body is not a registered view model). Using them for a dialog that *has* a row
re-opens the drift [ADR-033](../decisions/033-dialog-catalog-single-registration-surface.md) closed.

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

### Step 3: Register the DataTemplate (REQUIRED)

**CRITICAL**: You MUST register an implicit `DataTemplate` in `Themes/DialogTemplates.xaml` for your ViewModel, otherwise the dialog displays the ViewModel's type name instead of your UI.

Add beside the other dialog templates in `Themes/DialogTemplates.xaml`:

```xml
<DataTemplate DataType="{x:Type dialogs:MyDialogViewModel}">
    <contents:MyDialogContent/>
</DataTemplate>
```

**Why this is required**: WPF's `ContentPresenter` uses implicit DataTemplates to determine how to render objects. Without a registered DataTemplate it falls back to calling `ToString()` on your ViewModel, which displays the fully qualified type name (e.g. "Pulsar.ViewModels.Dialogs.MyDialogViewModel"). `DialogService` additionally fails fast through `HasTemplate`, and `DialogCatalogTests` fails the build.

**Location**: `Pulsar/Pulsar/Themes/DialogTemplates.xaml`. The dictionary is merged twice — `App.xaml` (feeds `DialogService.HasTemplate`'s fail-fast) and `DialogHostWindow.xaml` (feeds the visual tree) — but both read the same file, so there is nothing to keep in sync.

### Step 4: Register the catalog row (REQUIRED)

Add a `DialogId` constant and one `DialogRegistration` row in `Services/DialogCatalog.cs`. The row owns the title key, size preset, buttons and theme:

```csharp
// In DialogIds
public static readonly DialogId MyDialog = new("MyDialog");

// In DialogCatalog._registrations
new(DialogIds.MyDialog, "Dialog.MyDialog.Title", typeof(MyDialogViewModel), DialogSizeConstraints.Medium)
```

Add the title key to **both** `Resources/Strings.resx` and `Resources/Strings.zh-CN.resx`. If the title is a composite format string (e.g. "Configure {0}"), set `TitleIsFormat = true` on the row and pass the arguments at the call site.

### Step 5: Show the Dialog

```csharp
var vm = new MyDialogViewModel();
var result = await _dialogService.ShowCustomAsync(DialogIds.MyDialog, vm);
```

No title key, no size preset, no button set at the call site — the row owns them. `DialogCatalogTests` verifies all three legs (title key present in both resx, a template for the content type, a row for every template or a documented exemption) on every build.

---

## Common Patterns

### List Selection Dialog
```csharp
// The row owns the size: DialogIds.PickProcess is LargeResizable (resizable + maximizable).
var picker = new ProcessPickerViewModel(_windowService);
var result = await _dialogService.ShowCustomAsync(DialogIds.PickProcess, picker);
```

### Form Dialog
```csharp
// Dialogs.AddProfile's row is Medium.
var vm = new InputProfileViewModel(...);
var result = await _dialogService.ShowCustomAsync(DialogIds.AddProfile, vm);
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
- ❌ **Deprecated**: Choosing a title key and a `DialogSizeConstraints` preset at the call site for a dialog that has a catalog row
- ✅ **Recommended**: Use `DialogService` for all dialogs
- ✅ **Recommended**: Give each dialog a catalog row and show it with `ShowCustomAsync(DialogId.X, content)` (ADR-033)
- ✅ **Recommended**: Use `ShowMessageAsync` with `DialogType` for better UX

---

## Troubleshooting

### Dialog shows ViewModel type name instead of UI
**Symptom**: Dialog displays "Pulsar.ViewModels.Dialogs.MyDialogViewModel" instead of your custom UI.

**Root Cause**: Missing DataTemplate registration in `Themes/DialogTemplates.xaml`.

**Solution**: Add the DataTemplate in `Themes/DialogTemplates.xaml` (see Step 3):
```xml
<DataTemplate DataType="{x:Type dialogs:MyDialogViewModel}">
    <contents:MyDialogContent/>
</DataTemplate>
```

### `DialogCatalogTests` fails after adding a dialog
**Symptom**: Build fails with "every catalog title key must resolve", "must have an implicit DataTemplate", or "a format flag that disagrees with the resx value".

**Root Cause**: One of the three catalog legs is missing or inconsistent — the title key is absent from one of the two resx files, the content type has no DataTemplate, the template has no catalog row, or `TitleIsFormat` disagrees with whether the resx **value** contains `{0}`.

**Solution**: Fix the leg the message names. For the last case: a value with `{0}` needs `TitleIsFormat = true` on the row plus the argument at the call site; a value without `{0}` needs `TitleIsFormat = false` (or the placeholder added when the title genuinely should include the value).

### Dialog is too large for simple confirmation
**Solution**: Use `ShowConfirmationAsync()` or `ShowMessageAsync()` instead of custom dialog.

### List content is cramped
**Solution**: The row's `DialogSizeConstraints` decides — change it in `DialogCatalog`, not at the call site. `LargeResizable` allows maximizing.

### Icons not showing
**Check**: Icons only show for string content in `ShowMessageAsync()`, not custom ViewModels.

### Maximize button visible when it shouldn't be
**Check**: The catalog row's size preset — `LargeResizable` enables maximize, `Large` does not.

---

## Related Documents

- [UI Best Practices](../guides/UI_BEST_PRACTICES.md) - General UI guidelines
- [WPF Theme Injection Pitfalls](../lessons/WPF_THEME_INJECTION_PITFALLS.md) - Theme-related issues
- [Component Library](../guides/COMPONENT_LIBRARY.md) - Reusable UI components

---

**Change History**:
- v1.3.0 (2026-09-11): Dialog catalog (ADR-033) — `ShowCustomAsync(DialogId, content)` is the recommended path; a catalog row owns title key / size / buttons / theme; new-dialog recipe gains a row step; DataTemplate location corrected to `Themes/DialogTemplates.xaml`; troubleshooting for the catalog guards
- v1.2.0 (2026-03-07): Emphasized DataTemplate registration requirement, added troubleshooting for missing DataTemplate
- v1.1.0 (2026-03-07): Added XSmall/LargeResizable presets, icon display, button semantics, size selection guide
- v1.0.0 (2026-03-03): Initial extraction from AGENTS.md
