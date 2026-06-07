## ADDED Requirements

### Requirement: XAML files use LocExtension instead of hardcoded strings

The system SHALL replace all hardcoded English strings in XAML files that represent user-visible text with `LocaleExtension` bindings referencing resource keys from `Strings.resx`.

#### Scenario: SettingsWindow.xaml uses localized strings
- **WHEN** `SettingsWindow.xaml` is loaded
- **THEN** the title bar text, button content, and tooltip text SHALL be resolved via `{lex:Locale ...}` bindings

#### Scenario: SettingsGeneralPage.xaml uses localized strings
- **WHEN** `SettingsGeneralPage.xaml` is loaded
- **THEN** all CardExpander headers, TextBlock labels, Button content, and PlaceholderText attributes SHALL be resolved via `{lex:Locale ...}` bindings

#### Scenario: SettingsSlotsPage.xaml uses localized strings
- **WHEN** `SettingsSlotsPage.xaml` is loaded
- **THEN** page title, button content, placeholder text, context menu headers, and status text SHALL be resolved via `{lex:Locale ...}` bindings

#### Scenario: SettingsPluginsPage.xaml uses localized strings
- **WHEN** `SettingsPluginsPage.xaml` is loaded
- **THEN** tab titles, button content, search placeholder, group labels, health labels, empty-state text, and context menu headers SHALL be resolved via `{lex:Locale ...}` bindings

#### Scenario: SettingsAboutPage.xaml uses localized strings
- **WHEN** `SettingsAboutPage.xaml` is loaded
- **THEN** page title, CardExpander headers, link labels, and button content SHALL be resolved via `{lex:Locale ...}` bindings

#### Scenario: SettingsMarketplacePage.xaml uses localized strings
- **WHEN** `SettingsMarketplacePage.xaml` is loaded
- **THEN** button content, toggle content, search placeholder, and status labels SHALL be resolved via `{lex:Locale ...}` bindings

#### Scenario: AddSlotContent.xaml uses localized strings
- **WHEN** `AddSlotContent.xaml` is loaded
- **THEN** section labels, hint text, placeholder text, expander headers, and button content SHALL be resolved via `{lex:Locale ...}` bindings

#### Scenario: All dialog content XAML files use localized strings
- **WHEN** any dialog content UserControl is loaded (SecretPicker, PluginSettings, PluginLogViewer, etc.)
- **THEN** all user-visible text attributes SHALL be resolved via `{lex:Locale ...}` bindings

#### Scenario: RadialMenuWindow.xaml uses localized strings
- **WHEN** `RadialMenuWindow.xaml` is loaded
- **THEN** the window title SHALL be resolved via `{lex:Locale ...}` binding

### Requirement: C# ViewModels and Services use ILocalizationService for user-visible strings

The system SHALL replace all hardcoded English strings in C# files that produce user-visible text with calls to `ILocalizationService.GetString()`.

#### Scenario: SettingsViewModel notifications use localized strings
- **WHEN** `SettingsViewModel` shows a notification (success, error, confirmation)
- **THEN** the notification title and body SHALL be resolved via `_localizationService.GetString()`

#### Scenario: SettingsViewModel dialog titles use localized strings
- **WHEN** `SettingsViewModel` opens a dialog (Select Secret, Select Application, Select Icon, etc.)
- **THEN** the dialog title SHALL be resolved via `_localizationService.GetString()`

#### Scenario: AddSlotViewModel UI strings use localized strings
- **WHEN** `AddSlotViewModel` constructs header text, status text, button text, or validation messages
- **THEN** all user-visible strings SHALL be resolved via `_localizationService.GetString()`

#### Scenario: PluginSettingViewModel validation messages use localized strings
- **WHEN** `PluginSettingViewModel` validates a setting and produces a validation error message
- **THEN** the message text SHALL be resolved via `_localizationService.GetString()` with format arguments for dynamic values

#### Scenario: ActionFeedbackService feedback messages use localized strings
- **WHEN** `ActionFeedbackService` generates user-facing feedback (e.g., "could not switch to app")
- **THEN** the feedback title and body SHALL be resolved via `_localizationService.GetString()`

#### Scenario: TrayIconService menu items use localized strings
- **WHEN** `TrayIconService` creates the tray icon context menu
- **THEN** menu item labels SHALL be resolved via `_localizationService.GetString()`

#### Scenario: SettingsPageCatalog page titles use localized strings
- **WHEN** `SettingsPageCatalog` provides page registrations
- **THEN** page titles (General, Slots, Plugins, About) SHALL be resolved via `_localizationService.GetString()`

#### Scenario: RadialMenuViewModel center text uses localized strings
- **WHEN** `RadialMenuViewModel` sets the center orb text or sub-menu back text
- **THEN** the text SHALL be resolved via `_localizationService.GetString()`

#### Scenario: SlotViewModel health badges use localized strings
- **WHEN** `SlotViewModel` or `SlotPresentation` sets health badge text (Ready, Error, Warning)
- **THEN** the badge text SHALL be resolved via `_localizationService.GetString()`

#### Scenario: Tutorial step titles use localized strings
- **WHEN** `TutorialStepLoader` builds tutorial step models
- **THEN** step titles (Welcome, Switch Mode, etc.) SHALL be resolved via `_localizationService.GetString()`

### Requirement: Plugin metadata strings remain as English defaults

The system SHALL preserve plugin-defined English strings (DisplayName, Description, action labels, parameter labels) as the default display values and SHALL NOT require plugins to provide translations.

#### Scenario: Plugin displays English name when no translation is available
- **WHEN** a plugin has `DisplayName = "Command Runner"`
- **AND** no Chinese translation is available for this plugin
- **THEN** the plugin SHALL display as "Command Runner" in the UI
