# Tasks: fix-plugin-config-ui

## 1. Schema-to-Setting Adapter Implementation

- [x] 1.1 Create `SchemaToSettingAdapter` class in `Core/Plugin/`
- [x] 1.2 Implement conversion from `PropertySchema` to `PluginSettingDefinition`
- [x] 1.3 Map PropertySchema validators to PluginSettingViewModel validators
- [x] 1.4 Handle all PropertySchema types: bool, int, string, enum

## 2. Update PluginSettingsDialogViewModel

- [x] 2.1 Modify constructor to accept ConfigSchema (optional)
- [x] 2.2 Add adapter usage as primary settings source
- [x] 2.3 Fallback to IPluginConfigurable if no Schema provided
- [x] 2.4 Test with existing WinSwitcher settings

## 3. PkiPlugin Configuration Fix

- [x] 3.1 Add IPluginConfigurable implementation to PkiPlugin
- [x] 3.2 Implement GetSettingsDefinition() returning autoSubmit, injectionDelay, useUiaFirst
- [x] 3.3 Implement UpdateSettings() to apply config to PkiExecutionService
- [x] 3.4 Add validation for injectionDelay (0-1000ms range)
- [x] 3.5 Verify settings appear in UI

## 4. MultiSelect Setting Type Support

- [x] 4.1 Add `MultiSelect` to `PluginSettingType` enum
- [x] 4.2 Create `MultiSelectSettingViewModel` class
- [x] 4.3 Add XAML template for multi-select list in PluginSettingsDialogContent.xaml
- [x] 4.4 Add tokenization support for comma-separated values
- [x] 4.5 Add validation for multi-select (max items, allowed values)

## 5. WinSwitcher Hardcoded Logic Removal

- [x] 5.1 Add ExcludeProcesses as MultiSelect type in Schema
- [x] 5.2 Create dynamic option provider for running processes
- [x] 5.3 Remove hardcoded WinSwitcher special case in PluginViewModel.cs lines 312-366
- [x] 5.4 Verify blacklist UI works via schema-driven approach
- [ ] 5.5 Delete ProcessBlacklistViewModel if no longer needed

## 6. Extension Plugin Configuration

- [x] 6.1 SimpleCommandPlugin: Add defaultDelay setting (integer, 0-10000ms)
- [x] 6.2 VbaRunnerPlugin: Add defaultTargetApp setting (selection: Excel/WPS/Auto)
- [x] 6.3 BookmarkletRunnerPlugin: Add inputMethod setting (selection: UIA/Clipboard/Fallback)
- [x] 6.4 Implement IPluginConfigurable for all three plugins

## 7. Testing & Validation

- [x] 7.1 Run dotnet build to verify no compilation errors
- [ ] 7.2 Test PkiPlugin settings dialog opens and saves
- [ ] 7.3 Test WinSwitcher blacklist configuration works
- [ ] 7.4 Test extension plugin settings save correctly
- [ ] 7.5 Verify settings persist after app restart
