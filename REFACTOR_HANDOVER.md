# Dialog System Refactoring Handover

## 1. Project Overview & Objective
We are migrating Pulsar's fragmented dialog system (direct `Window` instantiation) to a **Unified Dialog System (UDS)** managed by `IDialogService`.

**Goal**:
- All dialogs should be invoked via `IDialogService`.
- No UI logic (Window creation) in ViewModels or Plugins.
- Consistent UI (Mica, ScrollViewer, Theme) managed by `DialogHostWindow`.

## 2. Completed Work (Infrastructure)
The core infrastructure is built and stable:

- **Service**: `IDialogService` implemented in `DialogService.cs`.
- **Host Window**: `DialogHostWindow.xaml` (supports Mica, Auto-Scroll, Standardized Buttons).
- **ViewModels**: `DialogHostViewModel.cs` handles the bridge between Service and View.
- **Enums**: `DialogResult`, `DialogType`, `DialogButtons` in `Pulsar.Models.Enums`.
- **Integration**:
  - `SettingsViewModel` now uses `_dialogService.ShowConfirmationAsync` for deletions and resets.
  - `ConfirmationDialog.xaml` has been **deleted**.
  - `App.xaml.cs` registers `IDialogService` as Singleton.

## 3. Remaining Tasks (To-Do)

The following specific dialog windows still exist and are instantiated directly. They must be refactored into **ViewModels + UserControls** to be displayed via `_dialogService.ShowCustomAsync<T>`.

### Priority 1: Complex Inputs
Convert these Windows into `UserControls` and corresponding `ViewModels`.

1.  **QuickSecretsDialog** (`Views/Dialogs/QuickSecretsDialog.xaml`)
    -   *Current*: Instantiated in `SettingsViewModel.AddSecret`.
    -   *Target*: Create `QuickSecretsViewModel`. Move XAML content to `Views/Dialogs/Contents/QuickSecretsContent.xaml`.
    -   *Action*: Update `SettingsViewModel` to use `_dialogService.ShowCustomAsync<QuickSecretsViewModel>(...)`.

2.  **InputProfileDialog** (`Views/Dialogs/InputProfileDialog.xaml`)
    -   *Current*: Instantiated in `SettingsViewModel.AddProfileDialog`.
    -   *Target*: Create `InputProfileViewModel`.
    -   *Action*: Handle "Process Picking" logic within the VM (invoke `IDialogService` recursively if needed, or use a service).

3.  **EditProfileDialog** (`Views/Dialogs/EditProfileDialog.xaml`)
    -   *Target*: Create `EditProfileViewModel`.

### Priority 2: Pickers
These are modal pickers.

4.  **ProcessPickerDialog**
    -   *Target*: `ProcessPickerViewModel`.
    -   *Note*: Logic for fetching processes should be in the VM.

5.  **IconPickerDialog** & **ColorPickerDialog**
    -   *Target*: `IconPickerViewModel` / `ColorPickerViewModel`.

6.  **SimpleInputDialog**
    -   *Target*: Either replace with `_dialogService.ShowInputAsync` (if simple enough) or generic `InputViewModel`.

## 4. Technical Implementation Guide

### Step A: Create the ViewModel
Create a class implementing `ObservableObject` (and optionally `IDialogViewModel` if you create that interface for validation/lifecycle hooks).

```csharp
// ViewModels/Dialogs/QuickSecretsViewModel.cs
public partial class QuickSecretsViewModel : ObservableObject
{
    [ObservableProperty] private string _label;
    [ObservableProperty] private string _account;
    // ...
}
```

### Step B: Create the View (UserControl)
Extract the content from the existing `Window` (excluding `<Window>` tags) into a `UserControl`.

```xml
<!-- Views/Dialogs/Contents/QuickSecretsContent.xaml -->
<UserControl x:Class="...">
    <StackPanel>
        <ui:TextBox Text="{Binding Label}" />
        <!-- ... -->
    </StackPanel>
</UserControl>
```

### Step C: Register DataTemplate
In `App.xaml` or `Views/Dialogs/DialogHostWindow.xaml` resources, map the VM to the View.

```xml
<DataTemplate DataType="{x:Type viewmodels:QuickSecretsViewModel}">
    <views:QuickSecretsContent />
</DataTemplate>
```

### Step D: Call from Service
```csharp
var vm = new QuickSecretsViewModel();
var result = await _dialogService.ShowCustomAsync("Add Secret", vm, DialogButtons.OkCancel);

if (result == DialogResult.Confirmed)
{
    // Access data from vm
    var label = vm.Label;
}
```

## 5. Plugin Accessibility
Ensure `IDialogService` is accessible to plugins.
- Plugins acquire the service via `Initialize(IServiceProvider provider)`.
- Verify `Pulsar.Services.Interfaces` namespace is visible to Plugin projects.

---
**Status**: Ready for Priority 1 tasks.
