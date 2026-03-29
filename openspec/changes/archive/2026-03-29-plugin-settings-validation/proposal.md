## Why

The current plugin settings validation in Pulsar is a one-time check that happens only during config load, providing no real-time feedback to users. When a user makes an invalid configuration change, they receive a generic MessageBox error after attempting to save, with no indication of which specific field is invalid or how to fix it. This leads to a poor user experience and makes configuration errors difficult to debug.

Additionally, the existing PluginSettingViewModel lacks validation-related properties, and the three Setting ViewModels (Boolean, String, Selection) are insufficient—they only cover 3 of the 6 defined PluginSettingType enum values (Path, Integer, and Secret types have no corresponding ViewModels).

## What Changes

1. **Add real-time field-level validation to PluginSettingViewModel**
   - Add `IsValid`, `ValidationMessage`, and `HasValidation` properties
   - Add `Validate()` method that runs on every setting change
   - Display validation state inline in the UI (error/warning icons, colored borders)

2. **Create missing Setting ViewModels**
   - Add `PathSettingViewModel` for file/folder picker support
   - Add `IntegerSettingViewModel` for numeric input
   - Add `SecretSettingViewModel` for password/masked input

3. **Implement reusable ISettingValidator interface**
   - Create `RequiredValidator`, `RangeValidator`, `RegexValidator`
   - Integrate validators into PluginSettingDefinition

4. **Build PluginSettingsDialog**
   - Create a proper configuration dialog (instead of the current placeholder MessageBox)
   - Show validation errors inline per field
   - Display Save button with error count badge
   - Add Reset to Defaults functionality

5. **Enhance OnSettingChanged to block invalid saves**
   - Validate before persisting to ConfigService
   - Provide immediate visual feedback on invalid input

## Capabilities

### New Capabilities
- `plugin-setting-validation`: Real-time, field-level validation for plugin settings with inline UI feedback and reusable validators
- `plugin-settings-dialog`: A proper modal dialog for configuring plugins instead of placeholder MessageBoxes

### Modified Capabilities
- None—these are net-new capabilities that don't change existing spec behavior

## Impact

**Affected Code:**
- `ViewModels/Settings/PluginSettingViewModel.cs` — add validation properties and methods
- `ViewModels/Settings/PluginViewModel.cs` — integrate validation into OnSettingChanged
- `Core/Plugin/PluginSettingDefinition.cs` — add validation configuration properties
- `Views/Pages/SettingsPluginsPage.xaml` — use existing Setting templates
- New files: `PluginSettingsDialog.xaml`, `PluginSettingsDialogViewModel.cs`, `SettingValidators.cs`

**Dependencies:**
- CommunityToolkit.Mvvm (already in use)
- WPF UI controls (already in use)

**No Breaking Changes** — all additions are backward compatible with existing plugins.
