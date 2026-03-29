## Context

The current plugin settings system has two core problems:

1. **No real-time validation**: Settings are validated only when `ConfigService.LoadAsync()` runs, which happens at startup. Users receive no feedback when editing settings in the Settings UI—they must save and restart to discover errors.

2. **Incomplete ViewModel coverage**: The codebase defines 6 `PluginSettingType` enum values (Boolean, String, Path, Integer, Selection, Secret), but only 3 corresponding ViewModels exist (Boolean, String, Selection).

**Current Architecture:**
- `PluginSettingDefinition` → defines schema (type, label, description)
- `PluginSettingViewModel` (base + 3 subclasses) → binds to UI
- `PluginViewModel.Settings` → holds all settings for a plugin
- `OnSettingChanged` → saves directly without validation

**Constraints:**
- Must work with existing `IPluginConfigurable` interface
- Must be backward compatible with all existing plugins
- Validation should be opt-in via `PluginSettingDefinition` attributes

## Goals / Non-Goals

**Goals:**
- Add real-time field-level validation with inline UI feedback
- Block saves when validation fails
- Create missing ViewModel types (Path, Integer, Secret)
- Build a proper PluginSettingsDialog instead of placeholder MessageBox
- Provide reusable validators that plugins can declare in `PluginSettingDefinition`

**Non-Goals:**
- Modify `ConfigValidationPipeline` or server-side validation (this is UI-focused)
- Add validation to Profile/Slot configuration (separate concern)
- Persist validation state across sessions (validation runs on load)
- Support custom validator implementations from plugins (use declarative validators)

## Decisions

### 1. Declarative Validation in PluginSettingDefinition

**Decision:** Add validation properties directly to `PluginSettingDefinition` rather than requiring plugins to implement custom validation logic.

```csharp
public class PluginSettingDefinition
{
    // Existing properties...
    
    // New validation properties
    public bool IsRequired { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public string? Pattern { get; set; }  // Regex
    public int? MinValue { get; set; }
    public int? MaxValue { get; set; }
}
```

**Rationale:** Plugins already declare their settings via `GetSettingsDefinition()`. Adding validation attributes there keeps the configuration declarative and self-documenting. Plugins like WinSwitcher currently hard-code validation in `ValidateSettings()`—this approach makes validation part of the schema definition.

**Alternative Considered:** Interface-based validators (`ISettingValidator` implementable by plugins). Rejected because it requires more boilerplate from plugin authors and doesn't integrate with the UI layer.

### 2. Validation Runs Before Save

**Decision:** Call `Validate()` on the setting being changed before persisting to `ConfigService`. Invalid settings are not saved.

**Rationale:** Prevents invalid state from reaching the backend. Users see immediate feedback and can correct before losing valid existing settings.

**Alternative Considered:** Save then validate, show errors. Rejected because it creates inconsistent state and requires rollback logic.

### 3. One Error Per Field Display

**Decision:** Each field shows only its first validation error or warning, not a list.

**Rationale:** Simpler UI, less clutter. Most validation failures are obvious (e.g., "required field" or "invalid format"). Complex multi-error cases are rare.

### 4. Dialog Over Inline Expansion

**Decision:** Plugin configuration opens in a modal dialog, not inline in the plugin card.

**Rationale:** Settings may be numerous or complex. A dialog provides focused UX, scrollable content, and clear Save/Cancel actions. The current XAML already defines setting templates (`BooleanSettingTemplate`, `StringSettingTemplate`, `SelectionSettingTemplate`) that can be reused in the dialog.

## Risks / Trade-offs

| Risk | Impact | Mitigation |
|------|--------|------------|
| Breaking existing plugins that rely on MessageBox placeholder | Low — plugins don't currently have config UI | Deprecation warning in release notes |
| Validation regex causing performance issues | Low — validation runs on keystroke | Debounce input (250ms) before validating |
| Validator attributes not covering all plugin needs | Medium — some plugins may need custom logic | Keep `IPluginConfigurable.ValidateSettings()` as escape hatch |

## Migration Plan

1. **Phase 1**: Add validation properties to `PluginSettingViewModel` and implement missing ViewModels
2. **Phase 2**: Build `PluginSettingsDialog` using existing XAML templates
3. **Phase 3**: Integrate validation into `OnSettingChanged` and wire dialog to Configure button

No data migration required—validation is additive.

## Open Questions

1. **Should validation errors be logged?** Currently validation runs in the UI layer. Consider logging validation failures at Debug level for troubleshooting.

2. **How to handle plugins that define settings in multiple places?** WinSwitcher uses both `PluginSettingDefinition` and a custom `ProcessBlacklistViewModel`. We should deprecate the custom approach in favor of the declarative one.
