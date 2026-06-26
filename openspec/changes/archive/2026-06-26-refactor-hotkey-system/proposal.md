## Why

The global hotkey system is the primary user interaction surface for Pulsar, yet it has critical gaps: users cannot clear a hotkey (every action must have one), duplicate hotkey combinations go completely undetected, and system-reserved combinations like `Ctrl+Alt+Del` slip through unchecked. Additionally, hotkey changes require an application restart to take effect because the settings UI and the live hotkey engine are disconnected. Multiple refactoring needs (dead code, magic strings, silent error swallowing, inconsistent defaults across three separate locations) have accumulated, making now the right time to fix the system holistically rather than patching incrementally.

## What Changes

- **New**: Real-time hotkey conflict detection with visual warning in settings UI
- **New**: Empty/unassigned hotkey support — users can clear a hotkey with Backspace/Delete
- **New**: System-reserved key combination detection and warning
- **New**: Hotkey changes applied immediately without restart via live cache rebuilding
- **New**: Reusable `HotkeyBox` UI control encapsulating capture, clear, and validation feedback
- **Fix**: Unify three divergent default hotkey definitions into a single source of truth
- **Fix**: Eliminate silent `catch(Exception){}` in `RebuildHotkeyCache()` — log parse failures
- **Refactor**: Extract `HotkeyActionIds` and `HotkeyModifiers` constants, consolidate all hardcoded strings
- **Refactor**: Move hotkey capture business logic from `SettingsGeneralPage.xaml.cs` code-behind into the `HotkeyBox` control
- **Refactor**: Wire up `IHotkeyService.ApplyHotkey()` (previously dead code) to connect settings save with live engine
- **Integrate**: Add hotkey conflict validation stage to `ConfigValidationPipeline`

## Capabilities

### New Capabilities
- `hotkey-configuration-validation`: Conflict detection, system-reserved filtering, empty state handling, and validation pipeline integration
- `hotkey-live-update`: Hotkey changes applied immediately to the running engine without application restart
- `hotkey-control-component`: Reusable `HotkeyBox` WPF user control for hotkey capture, clearing, and conflict feedback

### Modified Capabilities
- `settings-dirty-state-guard`: Hotkey changes now participate in dirty state tracking (already handled via existing `MarkDirty()` in setters, but the refactored control must preserve this contract)

## Impact

- **Core files**: `Services/HotkeyService.cs` (major rewrite), `Models/ProfilesConfig.cs` (`HotkeyConfig` enhancement), `Services/Interfaces/IHotkeyService.cs` (new API)
- **UI files**: `Views/Pages/SettingsGeneralPage.xaml` (replace TextBox with HotkeyBox), `Views/Pages/SettingsGeneralPage.xaml.cs` (remove dead handlers)
- **New files**: `Helpers/HotkeyConstants.cs`, `Views/Controls/HotkeyBox.xaml` + `.xaml.cs`
- **Validation**: `Services/Validation/ConfigValidationPipeline.cs` (new Stage 5)
- **ViewModel**: `ViewModels/SettingsViewModel.cs` (wired live update, validation exposure)
- **Localization**: `Resources/Strings.resx` + `Strings.zh-CN.resx` (5 new keys)
- **Tests**: New `Services/HotkeyServiceTests.cs`, additions to existing model/viewmodel tests
- **Breaking changes**: None — JSON serialization format unchanged, `HotkeyConfig.Key=""` is backward-compatible
