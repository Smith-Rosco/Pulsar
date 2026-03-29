## Why

The Edit Slot dialog (SlotConfigurationDialogContent) has two issues that were already fixed in the Create Slot dialog (AddSlotContent):

1. **UI Feedback Loop Bug**: The ComboBox action selector uses TwoWay binding (default), causing a feedback loop that clears `Slot.Action` to an empty string when the dialog loads or when the user interacts with it.

2. **Missing UI Polish**: The Edit Slot dialog lacks validation error banners, status indicators, and consistent layout styling that exists in the Create Slot dialog.

These issues cause "action": "" to be saved to Profiles.json, resulting in validation errors or plugin execution failures when the configuration is reloaded.

## What Changes

- **SlotConfigurationDialogContent.xaml**: Apply same fixes as AddSlotContent:
  - Change ComboBox SelectedValue binding from default (TwoWay) to Mode=OneWay
  - Add validation error banner for action field
  - Add HeaderStatusText with consistent styling
  - Use unified action selection (HasSingleAction, HasMultipleActions pattern)

- **SlotConfigurationDialogViewModel.cs**: Add Action property change filter:
  - Skip PropertyChanged handling for Action property (same as AddSlotViewModel lines 433-437)
  - This prevents re-entry that causes action to be cleared

- **SlotConfigurationDialogContent.xaml.cs**: Add re-entry guard:
  - Add `_isSettingAction` flag to prevent double-execution of SetAction
  - Same pattern as AddSlotContent.xaml.cs

## Capabilities

### New Capabilities
None - this is a refactoring of existing functionality.

### Modified Capabilities
None - no requirement changes, purely implementation fixes.

## Impact

- Files modified:
  - `Views/Dialogs/Contents/SlotConfigurationDialogContent.xaml`
  - `ViewModels/Dialogs/SlotConfigurationDialogViewModel.cs`
  - `Views/Dialogs/Contents/SlotConfigurationDialogContent.xaml.cs`

- No breaking changes to APIs or user-visible behavior
- Fixes a bug that causes configuration save/load failures
