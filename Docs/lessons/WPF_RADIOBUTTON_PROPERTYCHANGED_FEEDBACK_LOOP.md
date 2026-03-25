# WPF RadioButton PropertyChanged Feedback Loop

**Status**: Published  
**Scope**: Lesson  
**Applies To**: WPF RadioButton inside ItemsControl, ObservableObject slot/action patterns  
**Last Updated**: 2026-03-25

---

## Rule (TL;DR)

Never use `Two-Way` binding on `RadioButton.IsChecked` when also handling `Checked`/`Click` events. Always use `Mode=OneWay`. Never let `PropertyChanged` on an observable property re-trigger the same setter that just fired it — filter the property name in `OnPropertyChanged` handlers to break the feedback loop.

---

## Symptom

- Selecting a RadioButton action briefly sets `Slot.Action`, but the value is immediately cleared back to empty string.
- `HasBlockingIssue` stays `true` even after an action is visually selected.
- Wizard "Continue" button remains disabled and cannot be clicked.
- Logs show the pattern: `slot.Action='run'` followed immediately by `slot.Action=''`.

---

## Root Cause

This is a **feedback loop** caused by three interacting mechanisms:

### 1. Two-Way `IsChecked` Binding Writes Back

When `SetAction(action)` is called:
1. `slot.Action = action` is set.
2. `InitializeSlotMetadata()` calls `slot.SetParameterMetadata(...)` which fires `OnPropertyChanged(nameof(AvailableActions))`.
3. `NotifyStateChanged()` calls `SyncSelectedActionStates()`, which sets `option.IsSelected = true` on the matching RadioButton option.
4. `IsSelected` changing fires its own `PropertyChanged`.
5. With `IsChecked=\"{Binding IsSelected}\"` (Two-Way), WPF writes `IsChecked = true` back to the RadioButton, triggering the `Checked` event **again**.
6. The second `Checked` event calls `SetAction(action)` a second time — or worse, with a stale/null value.

### 2. `GroupName` Cross-Container Mutual Exclusion

A fixed `GroupName="CreateSlotActionGroup"` string is **global** across all RadioButtons in the same window. When `ItemsControl` re-virtualizes or re-renders items, WPF enforces mutual exclusion by unchecking other RadioButtons with the same `GroupName`, firing additional spurious `Checked`/`Unchecked` events on unrelated items.

### 3. `OnSlotPropertyChanged` Re-Entry on `Action`

The ViewModel subscribes to `slot.PropertyChanged`. When `slot.Action` is set inside `SetSlotDraftAction`, the `PropertyChanged` event fires, triggering `OnSlotPropertyChanged`, which calls `ApplySuggestions()` + `NotifyStateChanged()`. This causes a second full UI refresh cycle that may re-invoke the action-setting path.

---

## The Fix

### Fix 1: `IsChecked` must be `Mode=OneWay`

```xml
<!-- WRONG -->
<RadioButton IsChecked="{Binding IsSelected}"
             Click="ActionRadio_Click" />

<!-- CORRECT -->
<RadioButton IsChecked="{Binding IsSelected, Mode=OneWay}"
             Checked="ActionRadio_Checked" />
```

Using `Checked` instead of `Click` ensures the event only fires when the button transitions to checked state, not on every click (including re-clicking an already-checked button).

### Fix 2: Remove `GroupName` from DataTemplate RadioButtons

```xml
<!-- WRONG -->
<RadioButton GroupName="CreateSlotActionGroup" ... />

<!-- CORRECT: no GroupName; mutual exclusion is handled by IsSelected binding -->
<RadioButton IsChecked="{Binding IsSelected, Mode=OneWay}" ... />
```

### Fix 3: Re-entry guard in code-behind

```csharp
private bool _isSettingAction;

private void ActionRadio_Checked(object sender, RoutedEventArgs e)
{
    if (_isSettingAction) return;

    if (sender is RadioButton radio
        && radio.Tag is string action
        && !string.IsNullOrWhiteSpace(action)
        && DataContext is AddSlotViewModel viewModel)
    {
        _isSettingAction = true;
        try
        {
            viewModel.SetAction(action);
        }
        finally
        {
            _isSettingAction = false;
        }
    }
}
```

### Fix 4: Filter `Action` property in `OnSlotPropertyChanged`

The ViewModel listens to `slot.PropertyChanged` for suggestions and state refresh. The `Action` property is exclusively managed by `SetAction/_setAction`. Allowing `PropertyChanged(Action)` to re-trigger `NotifyStateChanged` creates a second UI refresh that races with the first.

```csharp
private void OnSlotPropertyChanged(object? sender, PropertyChangedEventArgs e)
{
    if (_isApplyingSuggestions) return;

    // Action changes are managed exclusively by SetAction/_setAction.
    // Re-triggering here causes a feedback loop that clears Slot.Action.
    if (string.Equals(e.PropertyName, nameof(PluginSlot.Action), StringComparison.Ordinal))
        return;

    ApplySuggestions();
    NotifyStateChanged();
}
```

### Fix 5: Build `actionOptions.IsSelected` AFTER resolving `slot.Action`

In `InitializeSlotMetadata`, determine the fallback `actionMetadata` and fix `slot.Action` **before** constructing the `actionOptions` list. Otherwise, `IsSelected` is computed against a stale/empty `Action` value.

```csharp
// WRONG order:
var actionOptions = BuildOptions(slot.Action);   // slot.Action may still be empty
var actionMetadata = Resolve(slot.Action) ?? fallback;
if (...) slot.Action = actionMetadata.Name;       // too late, IsSelected already wrong

// CORRECT order:
var actionMetadata = Resolve(slot.Action) ?? fallback;
if (...) slot.Action = actionMetadata.Name;       // fix first
var actionOptions = BuildOptions(slot.Action);   // now IsSelected is correct
```

---

## Debugging Approach

When `slot.Action` keeps getting cleared, add structured logging to both the setter and the metadata initializer:

```csharp
_logger.LogInformation("[SetSlotDraftAction] DONE: slot.Action='{Action}'", slot.Action);
_logger.LogInformation("[InitializeSlotMetadata] actionMetadata={Name}", actionMetadata?.Name ?? "NULL");
```

The giveaway pattern in the log is:

```
[SetSlotDraftAction] DONE: slot.Action='run'
[SetSlotDraftAction] DONE: slot.Action=''     ← second call with empty value
```

This confirms a second call is occurring after the first — trace back to identify which event handler is invoking `SetAction` the second time.

---

## Files Changed

| File | Change |
|---|---|
| `Views/Dialogs/Contents/AddSlotContent.xaml` | `IsChecked` → `Mode=OneWay`; removed `GroupName`; `Click` → `Checked` |
| `Views/Dialogs/Contents/AddSlotContent.xaml.cs` | Added `_isSettingAction` re-entry guard |
| `ViewModels/Dialogs/AddSlotViewModel.cs` | Filter `Action` in `OnSlotPropertyChanged` |
| `ViewModels/SettingsViewModel.cs` | Removed equality guard in `SetSlotDraftAction`; reordered `InitializeSlotMetadata` |

---

## Related Documents

- [WPF_USERCONTROL_BINDING_BREAKS.md](./WPF_USERCONTROL_BINDING_BREAKS.md) - Similar visual tree binding issues
- [WPF_THEME_INJECTION_PITFALLS.md](./WPF_THEME_INJECTION_PITFALLS.md) - Resource dictionary timing
- [UI_BEST_PRACTICES.md](../guides/UI_BEST_PRACTICES.md) - General WPF UI guidelines

---

**Change History**:  
- v1.0.0 (2026-03-25): Initial version — captured from Slot wizard action selection bug
