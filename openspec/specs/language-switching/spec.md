## ADDED Requirements

### Requirement: Language selector ComboBox in General Settings

The system SHALL provide a language selection ComboBox in the General Settings page, positioned in the Appearance section alongside Launcher Theme and Settings Theme controls.

#### Scenario: ComboBox displays current language
- **WHEN** General Settings page is opened
- **AND** the user's language preference is `"zh-CN"`
- **THEN** the language selector SHALL show `"中文 (Chinese)"` as the selected item

#### Scenario: ComboBox lists all supported languages
- **WHEN** the language selector dropdown is opened
- **THEN** it SHALL display at minimum `"English"` and `"中文 (Chinese)"` as selectable options

#### Scenario: Selecting a language triggers immediate UI refresh
- **WHEN** the user selects a different language from the ComboBox
- **THEN** `ILocalizationService.SetLanguage()` is called with the selected culture code
- **AND** the `LanguageChanged` event fires
- **AND** all open WPF windows SHALL refresh their localized text

### Requirement: Language preference is saved to Profiles.json

The system SHALL persist the user's language choice by writing to `ProfileSettings.Language` and calling `IConfigService.SaveAsync()`.

#### Scenario: Language persists across settings save
- **WHEN** the user changes the language and clicks "Save Changes"
- **THEN** the new language value is written to `Profiles.json` under `Settings.Language`
- **AND** the language persists after application restart

#### Scenario: Default language for new installations is English
- **WHEN** `Profiles.json` does not contain a `Language` field (first launch or upgrade from older version)
- **THEN** the application SHALL default to `"en"` (English)

### Requirement: Hot-switch immediately updates all open windows

The system SHALL update all visible WPF windows when the language changes, without requiring a window close/reopen or application restart.

#### Scenario: Settings window page titles update on language switch
- **WHEN** the Settings window is open showing page titles in English
- **AND** the user switches language to Chinese via the selector
- **THEN** navigation sidebar items, page headers, CardExpander headers, button labels, and text descriptions SHALL display in Chinese

#### Scenario: Dialog content updates on language switch
- **WHEN** a dialog (e.g., Add Slot) is open
- **AND** the user switches language
- **THEN** dialog header, button text, labels, placeholder text, and status messages SHALL update to the new language

#### Scenario: Plugin manager section updates on language switch
- **WHEN** the Plugin Manager page is open
- **AND** the user switches language
- **THEN** group headers ("Core Plugins (N)", "Extension Plugins (N)"), search placeholder, and tab labels SHALL update

### Requirement: Language combobox binds via SettingsViewModel

The system SHALL manage language selection state through `SettingsViewModel` properties.

#### Scenario: SettingsViewModel exposes supported languages
- **WHEN** `SettingsViewModel` is constructed
- **THEN** it SHALL populate a `SupportedLanguages` observable collection with language display models (code + display name)
- **AND** the collection is provided by `ILocalizationService.SupportedLanguages`

#### Scenario: SettingsViewModel exposes selected language
- **WHEN** the user selects a language in the ComboBox
- **THEN** `SettingsViewModel.SelectedLanguage` is updated via two-way binding
- **AND** the setter calls `ILocalizationService.SetLanguage()` with the selected code

#### Scenario: SettingsViewModel initializes from current language
- **WHEN** `SettingsViewModel` is constructed
- **THEN** `SelectedLanguage` SHALL reflect the current `ILocalizationService.CurrentLanguage`

### Requirement: Language code / display name mapping

The system SHALL map culture codes to human-readable display names in both English and the native language.

#### Scenario: English language display
- **WHEN** the language code `"en"` is mapped to a display name
- **THEN** the display name SHALL be `"English"`

#### Scenario: Chinese language display
- **WHEN** the language code `"zh-CN"` is mapped to a display name
- **THEN** the display name SHALL be `"中文 (Chinese)"`
