## 1. Enhance PluginSettingViewModel with Validation

- [x] 1.1 Add validation properties to PluginSettingViewModel base class (IsValid, ValidationMessage, HasValidation)
- [x] 1.2 Add Validate() method to PluginSettingViewModel base class
- [x] 1.3 Add validation properties to PluginSettingDefinition (IsRequired, MinLength, MaxLength, Pattern, MinValue, MaxValue)
- [x] 1.4 Implement validation logic in BooleanSettingViewModel.Validate()
- [x] 1.5 Implement validation logic in StringSettingViewModel.Validate()
- [x] 1.6 Implement validation logic in SelectionSettingViewModel.Validate()

## 2. Create Missing Setting ViewModels

- [x] 2.1 Create PathSettingViewModel with file/folder picker support
- [x] 2.2 Create IntegerSettingViewModel with numeric input
- [x] 2.3 Create SecretSettingViewModel with masked input
- [x] 2.4 Update PluginSettingViewModel.Create() factory to handle Path, Integer, Secret types
- [x] 2.5 Add validation logic to new ViewModels

## 3. Add Validation UI Templates

- [x] 3.1 Add PathSettingTemplate to SettingsPluginsPage.xaml
- [x] 3.2 Add IntegerSettingTemplate to SettingsPluginsPage.xaml
- [x] 3.3 Add SecretSettingTemplate to SettingsPluginsPage.xaml
- [x] 3.4 Update PluginSettingTemplateSelector to handle new types
- [x] 3.5 Add validation error display (red border, error icon, message) to templates

## 4. Create PluginSettingsDialog

- [x] 4.1 Create PluginSettingsDialogViewModel class
- [x] 4.2 Create PluginSettingsDialog.xaml with header (icon, name, description)
- [x] 4.3 Wire up settings ItemsControl with template selector
- [x] 4.4 Add Save, Cancel, and Reset to Defaults buttons
- [x] 4.5 Implement Save logic: validate all, persist to ConfigService, call UpdateSettings()
- [x] 4.6 Implement Cancel logic: discard changes, close dialog
- [x] 4.7 Implement Reset to Defaults: restore DefaultValue for all settings

## 5. Integrate Validation into OnSettingChanged

- [x] 5.1 Modify PluginViewModel.OnSettingChanged to call Validate() before saving
- [x] 5.2 Block save when IsValid is false
- [x] 5.3 Add visual feedback for blocked save (e.g., toast notification)

## 6. Wire Configure Command to Dialog

- [x] 6.1 Update PluginViewModel.Configure() to open PluginSettingsDialog
- [x] 6.2 Pass PluginViewModel to dialog for settings access
- [x] 6.3 Handle dialog result (save or cancel)
- [x] 6.4 Update SettingsPluginsPage.xaml to use dialog instead of MessageBox

## 7. Update WinSwitcher Plugin Settings

- [x] 7.1 Add validation attributes to WinSwitcher ExcludeProcesses setting (MinLength, Pattern for process names)
- [ ] 7.2 Remove hardcoded ProcessBlacklistViewModel usage, use new dialog system
- [ ] 7.3 Test WinSwitcher configuration works with new validation system

## 8. Testing and Polish

- [x] 8.1 Run dotnet build to verify compilation
- [ ] 8.2 Test validation UI shows errors inline
- [ ] 8.3 Test Save button disabled when validation fails
- [ ] 8.4 Test Reset to Defaults restores correct values
- [ ] 8.5 Verify all 6 setting types render correctly in dialog
