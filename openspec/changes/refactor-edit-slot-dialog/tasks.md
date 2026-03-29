## 1. XAML Fixes (SlotConfigurationDialogContent.xaml)

- [ ] 1.1 Change ComboBox SelectedValue binding to Mode=OneWay (line ~289, 297)
- [ ] 1.2 Add validation error banner for action field (similar to AddSlotContent lines 485-497)
- [ ] 1.3 Add HeaderStatusText with consistent styling (similar to AddSlotContent)

## 2. ViewModel Fixes (SlotConfigurationDialogViewModel.cs)

- [ ] 2.1 Add OnSlotPropertyChanged handler with Action property filter
- [ ] 2.2 Add HasActionValidationError and ActionValidationMessage properties
- [ ] 2.3 Update NotifyPresentationChanged to include new validation properties

## 3. Code-Behind Fixes (SlotConfigurationDialogContent.xaml.cs)

- [ ] 3.1 Add _isSettingAction re-entry guard field
- [ ] 3.2 Add guard check in ActionComboBox_SelectionChanged
- [ ] 3.3 Add guard check in ActionRadio_Click (if used)

## 4. Testing

- [ ] 4.1 Build the project to verify no compilation errors
- [ ] 4.2 Test Edit Slot dialog opens correctly with existing action
- [ ] 4.3 Test changing action saves correctly
- [ ] 4.4 Test cancel does not corrupt configuration

## 5. Documentation

- [ ] 5.1 Update Docs/lessons/ if additional learnings from this fix
