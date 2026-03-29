## Why

Plugin configuration in Pulsar has a dual-track system where settings can be defined in either `PluginMetadata.Schema` or `IPluginConfigurable.GetSettingsDefinition()`, but only the latter renders in the UI. This causes user-facing bugs (PkiPlugin settings invisible) and architectural issues (hardcoded special cases). Additionally, extension plugins lack configurability entirely. Unifying this system improves maintainability and user experience.

## What Changes

1. **Implement IPluginConfigurable for PkiPlugin** - The PKI plugin has settings defined in its Schema (`autoSubmit`, `injectionDelay`, `useUiaFirst`) but users cannot configure them because `IPluginConfigurable` is not implemented.

2. **Remove WinSwitcher hardcoded special handling** - `PluginViewModel.cs` contains hardcoded logic for WinSwitcher blacklist configuration. Replace with schema-driven UI rendering.

3. **Unify configuration definition source** - Establish `PluginMetadata.Schema` as the single source of truth for plugin settings. Create a code generator or adapter that converts Schema to UI ViewModels automatically.

4. **Add basic configuration to extension plugins** - Give SimpleCommandPlugin, VbaRunnerPlugin, and BookmarkletRunnerPlugin basic configurable options.

5. **Add MultiSelect/Array setting type** - Support comma-separated and multi-select settings in the UI framework (needed for WinSwitcher blacklist).

## Capabilities

### New Capabilities
- `plugin-schema-driven-config`: Schema-driven plugin configuration UI that automatically renders settings from `PluginMetadata.Schema` without requiring separate `IPluginConfigurable` implementation

### Modified Capabilities
- None - this is a refactoring that doesn't change user-facing behavior requirements

## Impact

- **Files Modified**: 
  - `Plugins/Core/Pki/PkiPlugin.cs` - Add IPluginConfigurable implementation
  - `ViewModels/Settings/PluginViewModel.cs` - Remove hardcoded WinSwitcher logic
  - `Core/Plugin/PluginSettingDefinition.cs` - Add new setting types
  - `ViewModels/Settings/PluginSettingViewModel.cs` - Add new setting type renderers
  - `Views/Dialogs/Contents/PluginSettingsDialogContent.xaml` - Add new UI templates
- **New Files**: Schema-to-ViewModel adapter/generator code
- **Breaking Changes**: None - backward compatible refactoring
- **Dependencies**: None new required
