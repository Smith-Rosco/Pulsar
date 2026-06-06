# WPF ComboBox SelectedValue OneWay Blank Selection Box

**Status**: Published  
**Scope**: Lesson  
**Applies To**: WPF ComboBox with data-bound ItemsSource, SelectedValue/SelectedValuePath pattern, ObservableCollection replacement  
**Last Updated**: 2026-06-06

---

## Rule (TL;DR)

Never use `SelectedValue="{Binding ..., Mode=OneWay}"` on a ComboBox whose `ItemsSource` collection is replaced at runtime (e.g., via `new ObservableCollection<T>(...)`). Always use default TwoWay binding on `SelectedValue` and override `Equals`/`GetHashCode` on the item type so the ComboBox can match instances across collection replacements.

---

## Symptom

- ComboBox dropdown items display text correctly (labels visible when expanded)
- Selection box (closed ComboBox) shows blank/no text
- Occurs specifically when `SetParameterMetadata()` rebuilds `AvailableActions` with a new `ObservableCollection`, replacing the previous one

---

## Root Cause

This is a **two-factor bug** caused by the interaction of:

### 1. `Mode=OneWay` on `SelectedValue` (Commit `817b7a0`)

Commit `817b7a0` changed ComboBox `SelectedValue` from default TwoWay to `Mode=OneWay` as part of a RadioButton feedback loop fix (see [WPF_RADIOBUTTON_PROPERTYCHANGED_FEEDBACK_LOOP.md](./WPF_RADIOBUTTON_PROPERTYCHANGED_FEEDBACK_LOOP.md)). However, the RadioButton feedback loop was specific to `IsChecked` TwoWay + `Checked` event + `GroupName` cross-container interactions — **it did not apply to ComboBox**. The ComboBox `Mode=OneWay` fix was unnecessary and introduced a display bug.

### 2. Collection Replacement Breaks `SelectedItem` Resolution

`SetParameterMetadata()` in `PluginSlot` creates a **new** `ObservableCollection<SlotActionOption>` every time the action changes:

```csharp
AvailableActions = new ObservableCollection<SlotActionOption>(availableActions);
```

WPF's ComboBox resolves `SelectedItem` from `SelectedValue` by searching `ItemsSource`. The timing is:

1. `Slot.Action = "launch"` → fires `PropertyChanged("Action")`
2. OneWay binding sets `ComboBox.SelectedValue = "launch"` → resolves `SelectedItem` from current `ItemsSource` ✓
3. `InitializeSlotMetadata` → `SetParameterMetadata` → **replaces** `ItemsSource` with new collection
4. ComboBox detects old `SelectedItem` is not in new collection (reference equality fails) → **clears selection** → blank display

### 3. Missing `Equals` Override

`SlotActionOption` did not override `Equals`/`GetHashCode`. When the ComboBox's `ItemsSource` is replaced, it checks if the existing `SelectedItem` is in the new collection using `IndexOf()`, which uses `Equals`. With default reference equality, different `SlotActionOption` instances (even with the same `Value`) are treated as different → selection lost.

---

## The Fix

### Fix 1: Override `Equals`/`GetHashCode` on `SlotActionOption`

```csharp
public partial class SlotActionOption : ObservableObject
{
    public required string Value { get; init; }
    // ...

    public override bool Equals(object? obj)
    {
        if (obj is SlotActionOption other)
            return string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
        return false;
    }

    public override int GetHashCode()
    {
        return Value != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Value) : 0;
    }
}
```

This ensures the ComboBox can match `SelectedItem` across collection replacements by comparing `Value`.

### Fix 2: Remove `Mode=OneWay` from ComboBox `SelectedValue` Binding

```xml
<!-- WRONG (introduced by 817b7a0) -->
<ComboBox SelectedValue="{Binding Slot.Action, Mode=OneWay}" ... />

<!-- CORRECT (original default TwoWay) -->
<ComboBox SelectedValue="{Binding Slot.Action}" ... />
```

The RadioButton feedback loop (IsChecked TwoWay + GroupName + Checked event) does not affect ComboBox because ComboBox has no `GroupName` and `SelectionChanged` does not create a re-entrancy loop with `SelectedValue`.

---

## Debugging Approach

When `SelectedValue` binding shows blank selection box:

1. Check if `ItemsSource` is being replaced (not mutated in-place) — look for `new ObservableCollection<T>(...)` assignments
2. Verify the item type overrides `Equals` for value-based comparison
3. Confirm `SelectedValue` binding is NOT `Mode=OneWay` — this prevents proper `SelectedItem` resolution
4. Compare with known-good patterns in the codebase (e.g., `SettingsSlotsPage.xaml` uses `SelectedItem`+TwoWay)

---

## Files Changed

| File | Change |
|---|---|
| `Models/SlotParameterEditorModels.cs` | Override `Equals`/`GetHashCode` on `SlotActionOption` to compare by `Value` |
| `Views/Dialogs/Contents/AddSlotContent.xaml` | Remove `Mode=OneWay` from ComboBox `SelectedValue` binding |
| `Views/Dialogs/Contents/SlotConfigurationDialogContent.xaml` | Remove `Mode=OneWay` from ComboBox `SelectedValue` binding |

---

## Related Documents

- [WPF_RADIOBUTTON_PROPERTYCHANGED_FEEDBACK_LOOP.md](./WPF_RADIOBUTTON_PROPERTYCHANGED_FEEDBACK_LOOP.md) - The original RadioButton fix that inadvertently added `Mode=OneWay` to ComboBox
- [WPF_USERCONTROL_BINDING_BREAKS.md](./WPF_USERCONTROL_BINDING_BREAKS.md) - Visual tree binding issues
- [UI_BEST_PRACTICES.md](../guides/UI_BEST_PRACTICES.md) - General WPF UI guidelines

---

**Change History**:  
- v1.0.0 (2026-06-06): Initial version — captured from task editor ComboBox blank selection box bug
