## 1. XAML Fixes (SlotConfigurationDialogContent.xaml)

- [x] 1.1 Change ComboBox SelectedValue binding to Mode=OneWay (line ~289, 297)
- [x] 1.2 Add validation error banner for action field (similar to AddSlotContent lines 485-497)
- [x] 1.3 Add HeaderStatusText with consistent styling (similar to AddSlotContent)

## 2. ViewModel Fixes (SlotConfigurationDialogViewModel.cs)

- [x] 2.1 Add OnSlotPropertyChanged handler with Action property filter
- [x] 2.2 Add HasActionValidationError and ActionValidationMessage properties
- [x] 2.3 Update NotifyPresentationChanged to include new validation properties

## 3. Code-Behind Fixes (SlotConfigurationDialogContent.xaml.cs)

- [x] 3.1 Add _isSettingAction re-entry guard field
- [x] 3.2 Add guard check in ActionComboBox_SelectionChanged
- [x] 3.3 Add guard check in ActionRadio_Click (if used)

## 4. Testing

- [x] 4.1 Build the project to verify no compilation errors
- [ ] 4.2 Test Edit Slot dialog opens correctly with existing action
- [ ] 4.3 Test changing action saves correctly
- [ ] 4.4 Test cancel does not corrupt configuration

## 5. UI Refactoring

- [x] 5.1 Simplify preview - remove large right-side preview card
- [x] 5.2 Reduce card usage - use simple layout with CardExpanders
- [x] 5.3 Lower cognitive load - single column layout, consistent left-right grid pattern
- [x] 5.4 Match AddSlotContent style

## 6. Documentation

- [ ] 6.1 Update Docs/lessons/ if additional learnings from this fix
