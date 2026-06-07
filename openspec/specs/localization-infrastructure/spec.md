## ADDED Requirements

### Requirement: Localization service provides translated strings by key

The system SHALL provide an `ILocalizationService` singleton that returns localized strings by key, supporting English and Chinese cultures, with fallback from the requested culture to English to the raw key name.

#### Scenario: Resolve string in Chinese culture
- **WHEN** the current language is set to `"zh-CN"`
- **AND** `GetString("Settings.General.Title")` is called
- **THEN** the Chinese translation from `Strings.zh-CN.resx` is returned

#### Scenario: Fallback to English when Chinese key is missing
- **WHEN** the current language is set to `"zh-CN"`
- **AND** a key exists in `Strings.resx` but not in `Strings.zh-CN.resx`
- **THEN** the English value from `Strings.resx` is returned

#### Scenario: Fallback to key name when no translation exists
- **WHEN** a key does not exist in either `Strings.resx` or `Strings.zh-CN.resx`
- **THEN** the key name itself is returned as the string

#### Scenario: Indexer access returns same value as GetString
- **WHEN** using the indexer `_service["Settings.General.Title"]`
- **THEN** it SHALL return the same value as `_service.GetString("Settings.General.Title")`

### Requirement: Language can be switched at runtime

The system SHALL allow changing the current language at runtime via `ILocalizationService.SetLanguage(cultureName)`.

#### Scenario: Set language to Chinese
- **WHEN** `SetLanguage("zh-CN")` is called
- **THEN** `CurrentLanguage` property returns `"zh-CN"`
- **AND** `Thread.CurrentThread.CurrentUICulture` is set to `zh-CN`
- **AND** subsequent `GetString()` calls return Chinese translations where available

#### Scenario: Set language to English
- **WHEN** `SetLanguage("en")` is called
- **THEN** `CurrentLanguage` property returns `"en"`
- **AND** all `GetString()` calls return English values

#### Scenario: Invalid culture name does not crash
- **WHEN** `SetLanguage("invalid-culture")` is called
- **THEN** the service SHALL log a warning and fall back to English
- **AND** no exception is thrown

### Requirement: LanguageChanged event notifies all consumers

The system SHALL fire a `LanguageChanged` event when the language is switched.

#### Scenario: Event fires on SetLanguage
- **WHEN** `SetLanguage("zh-CN")` is called
- **THEN** the `LanguageChanged` event is raised with the new culture name as event args

#### Scenario: Event does not fire when setting the same language
- **WHEN** `SetLanguage("en")` is called
- **AND** the current language is already `"en"`
- **THEN** the `LanguageChanged` event is NOT raised

### Requirement: Supported languages are discoverable

The system SHALL expose the list of supported language codes via `ILocalizationService.SupportedLanguages`.

#### Scenario: List contains English and Chinese
- **WHEN** `SupportedLanguages` is read
- **THEN** the collection SHALL contain at minimum `"en"` and `"zh-CN"`

### Requirement: LocExtension resolves localized strings in XAML

The system SHALL provide a WPF `LocaleExtension` (inheriting `MarkupExtension`) that resolves a resource key to its localized string at XAML parse time and updates the target when the language changes.

#### Scenario: LocExtension returns English string
- **WHEN** a XAML binding `Text="{lex:Locale Settings.General.Title}"` is parsed
- **AND** the current language is `"en"`
- **THEN** the TextBlock displays the English translation

#### Scenario: LocExtension updates on language change
- **WHEN** the language is switched from `"en"` to `"zh-CN"`
- **THEN** all active LocExtension bindings SHALL update their target properties to Chinese translations

#### Scenario: LocExtension with unknown key degrades gracefully
- **WHEN** a LocExtension is used with a key that does not exist in any resource file
- **THEN** the key name is displayed as the string value
- **AND** no exception is thrown

### Requirement: LocExtension is usable in common WPF attributes

The LocExtension SHALL function correctly when applied to `Text`, `Content`, `Header`, `Title`, `ToolTip`, and `PlaceholderText` dependency properties.

#### Scenario: LocExtension on Button Content
- **WHEN** `Content="{lex:Locale SettingsWindow.SaveChanges}"` is used on a Button
- **THEN** the Button displays the localized string for "Save Changes"

#### Scenario: LocExtension on TextBlock Text
- **WHEN** `Text="{lex:Locale Settings.General.LauncherTheme}"` is used on a TextBlock
- **THEN** the TextBlock displays the localized string for "Launcher Theme"

### Requirement: Localization service is registered in DI as singleton

The system SHALL register `ILocalizationService` as a singleton in the dependency injection container during application startup.

#### Scenario: Service is available to all ViewModels
- **WHEN** a ViewModel requests `ILocalizationService` via constructor injection
- **THEN** the DI container provides the singleton instance

#### Scenario: Language is initialized from config on startup
- **WHEN** the application starts
- **AND** the user's `Profiles.json` contains `"Language": "zh-CN"`
- **THEN** the localization service SHALL be initialized with Chinese as the current language

### Requirement: Language preference is persisted in Profiles.json

The system SHALL store the user's language preference in `ProfileSettings.Language` within `Profiles.json`.

#### Scenario: Default language is English
- **WHEN** no language preference has been set (fresh install)
- **THEN** `ProfileSettings.Language` SHALL default to `"en"`

#### Scenario: Language preference survives restart
- **WHEN** the user changes the language to `"zh-CN"` and saves configuration
- **AND** the application is restarted
- **THEN** the UI SHALL display in Chinese

### Requirement: Language selector is available in General Settings

The system SHALL provide a language selection ComboBox in the General Settings page under the Appearance section.

#### Scenario: Language selector shows available languages
- **WHEN** the user opens General Settings
- **THEN** a ComboBox is visible under Appearance showing `"English"` and `"中文 (Chinese)"` as options

#### Scenario: Selecting a language switches UI immediately
- **WHEN** the user selects `"中文 (Chinese)"` from the language selector
- **THEN** all visible UI text SHALL switch to Chinese without application restart
- **AND** the preference SHALL be persisted to Profiles.json

### Requirement: Hot-switch refreshes all open windows

The system SHALL refresh all open windows (Settings, Radial Menu, dialogs) when the language changes at runtime.

#### Scenario: Settings window items update on language switch
- **WHEN** Settings Window is open
- **AND** the user switches language via the selector
- **THEN** navigation items, page headers, buttons, and text labels SHALL all update to the new language

#### Scenario: Radial menu updates on language switch
- **WHEN** the radial menu is open
- **AND** the language is switched (via settings window)
- **THEN** the center orb text and any other localized text in the radial menu SHALL update

### Requirement: RESX files cover all core framework UI strings

The system SHALL include all core framework user-visible strings in `Strings.resx` (English) and `Strings.zh-CN.resx` (Chinese), organized by module prefix.

#### Scenario: All settings page strings have keys
- **WHEN** reviewing `Strings.resx`
- **THEN** every hardcoded English string from `SettingsGeneralPage.xaml`, `SettingsSlotsPage.xaml`, `SettingsPluginsPage.xaml`, `SettingsAboutPage.xaml`, and `SettingsMarketplacePage.xaml` has a corresponding key

#### Scenario: All dialog strings have keys
- **WHEN** reviewing `Strings.resx`
- **THEN** every hardcoded English string from dialog content XAML files and dialog ViewModels has a corresponding key

#### Scenario: All notification strings have keys
- **WHEN** reviewing `Strings.resx`
- **THEN** every hardcoded English notification title and body from `SettingsViewModel.cs` and `ActionFeedbackService.cs` has a corresponding key

#### Scenario: All validation message strings have keys
- **WHEN** reviewing `Strings.resx`
- **THEN** every hardcoded English validation message from `PluginSettingViewModel.cs` has a corresponding key

#### Scenario: Tray service strings have keys
- **WHEN** reviewing `Strings.resx`
- **THEN** tray tooltip text and context menu items from `TrayIconService.cs` have corresponding keys

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
