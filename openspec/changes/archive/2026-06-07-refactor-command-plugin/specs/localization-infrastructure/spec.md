## ADDED Requirements

### Requirement: Plugin runtime strings must use ILocalizationService

The system SHALL require that all user-facing strings generated at runtime by plugins (error messages, success notifications, status text) are resolved through `ILocalizationService` rather than hardcoded as string literals.

#### Scenario: Plugin error message uses localized string

- **WHEN** a plugin returns `PluginResult.Error(...)` with a message
- **THEN** the error message SHALL be obtained via `ILocalizationService.GetString(key)` or `_loc["key"]`, not a hardcoded string literal

#### Scenario: Plugin success message uses localized string

- **WHEN** a plugin returns `PluginResult.Ok(...)` with a message
- **THEN** the success message SHALL be obtained via `ILocalizationService`, not a hardcoded string literal

#### Scenario: Plugin strings adapt to language switch

- **WHEN** the user switches language from `"en"` to `"zh-CN"`
- **AND** a plugin that previously returned `"Keys sent"` is invoked again
- **THEN** the success message SHALL now display the Chinese translation

### Requirement: Plugin metadata labels and descriptions must be localizable

The system SHALL support localization of plugin metadata fields (`Label`, `Description`, `Placeholder`, `InputHint`, `ValidationHint`) through the existing convention-based key lookup and explicit `ILocalizationService` calls.

#### Scenario: Slot action label resolved via convention key

- **WHEN** a `SlotActionMetadata` has `Label = "Open Target"`
- **THEN** the system SHALL attempt to resolve `SlotAction.OpenTarget` through the localization service
- **AND** if found, display the translated string instead of the raw label

#### Scenario: Slot parameter label resolved via convention key

- **WHEN** a `SlotParameterMetadata` has `Label = "Path"`
- **THEN** the system SHALL attempt to resolve `SlotParam.Path` through the localization service

#### Scenario: Explicit localization keys for plugin settings

- **WHEN** a plugin defines `PluginSettingDefinition.Label = "Default Delay"`
- **THEN** the label text SHALL be sourced from a localization key rather than hardcoded
