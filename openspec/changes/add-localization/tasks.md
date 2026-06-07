## 1. Infrastructure — RESX Files

- [ ] 1.1 Create `Resources/Strings.resx` with all ~316 English keys organized by module prefix
- [ ] 1.2 Create `Resources/Strings.zh-CN.resx` with Chinese translations (copy English keys, translate values)
- [ ] 1.3 Verify both RESX files compile: `dotnet build Pulsar/Pulsar/Pulsar.csproj`

## 2. Infrastructure — ILocalizationService

- [ ] 2.1 Create `Core/Localization/ILocalizationService.cs` with `GetString`, `SetLanguage`, `CurrentLanguage`, `SupportedLanguages`, `LanguageChanged` event
- [ ] 2.2 Create `Core/Localization/LocalizationService.cs` implementing ILocalizationService with ResourceManager, fallback chain, event firing
- [ ] 2.3 Add `Language` property (string, default "en") to `ProfileSettings` in `Models/ProfilesConfig.cs`
- [ ] 2.4 Register `ILocalizationService` as singleton in `App.xaml.cs` with language initialization from config
- [ ] 2.5 Verify build: `dotnet build Pulsar/Pulsar/Pulsar.csproj`

## 3. Infrastructure — LocExtension (WPF MarkupExtension)

- [ ] 3.1 Create `Core/Localization/LocExtension.cs` inheriting MarkupExtension with ProvideValue, weak reference pattern, LanguageChanged subscription
- [ ] 3.2 Verify LocExtension resolves strings correctly: manual XAML test with known key
- [ ] 3.3 Verify LocExtension hot-switches: change language at runtime, confirm target properties update
- [ ] 3.4 Verify build: `dotnet build Pulsar/Pulsar/Pulsar.csproj`

## 4. XAML Refactoring — Settings Windows & Pages

- [ ] 4.1 Convert `SettingsWindow.xaml`: title bar, button Content, ToolTip → LocExtension bindings
- [ ] 4.2 Convert `SettingsGeneralPage.xaml`: page Title, CardExpander Headers, TextBlock Text, PlaceholderText, button Content → LocExtension bindings
- [ ] 4.3 Convert `SettingsSlotsPage.xaml`: page Title, button Content, placeholder text, context menu headers, empty-state text → LocExtension bindings
- [ ] 4.4 Convert `SettingsPluginsPage.xaml`: tab Content, button Content, search PlaceholderText, group labels, context menu headers, empty-state text → LocExtension bindings
- [ ] 4.5 Convert `SettingsAboutPage.xaml`: page Title, CardExpander Header, TextBlock Text, button Content → LocExtension bindings
- [ ] 4.6 Convert `SettingsMarketplacePage.xaml`: button Content, toggle Content, search PlaceholderText, filter/label Content → LocExtension bindings
- [ ] 4.7 Convert `RadialMenuWindow.xaml`: window Title → LocExtension binding
- [ ] 4.8 Verify build: `dotnet build Pulsar/Pulsar/Pulsar.csproj`

## 5. XAML Refactoring — Dialog Contents

- [ ] 5.1 Convert `Views/Dialogs/Contents/AddSlotContent.xaml`: TextBlock Text, button Content, PlaceholderText, Expander Header → LocExtension bindings
- [ ] 5.2 Convert `Views/Dialogs/Contents/SecretPickerContent.xaml`: TextBlock Text, button Content → LocExtension bindings
- [ ] 5.3 Convert `Views/Dialogs/Contents/PluginSettingsDialogContent.xaml`: button Content, PlaceholderText → LocExtension bindings
- [ ] 5.4 Convert `Views/Dialogs/Contents/PluginLogViewerContent.xaml`: PlaceholderText, button Content, empty-state text → LocExtension bindings
- [ ] 5.5 Convert remaining dialog content XAML files (ProcessPicker, QuickSecrets, etc.)
- [ ] 5.6 Convert `Plugins/Extensions/VbaRunner/SelectorWindow.xaml`: Title, button Content → LocExtension bindings
- [ ] 5.7 Add `xmlns:lex="clr-namespace:Pulsar.Core.Localization"` to each modified XAML file
- [ ] 5.8 Verify build: `dotnet build Pulsar/Pulsar/Pulsar.csproj`

## 6. C# Refactoring — SettingsViewModel

- [ ] 6.1 Inject `ILocalizationService` into `SettingsViewModel` constructor
- [ ] 6.2 Convert all notification titles/bodies (~20 strings): Saved, Error, Reset Complete, validation messages
- [ ] 6.3 Convert all dialog titles (~10 strings): Select Secret, Select Application, Select Icon, Edit Profile, etc.
- [ ] 6.4 Convert default slot template labels (~6 strings): Switch Or Launch App, Open Target, Run Script, etc.
- [ ] 6.5 Convert cache management dialog strings (~4 strings)
- [ ] 6.6 Convert drag-drop notification strings (~3 strings)
- [ ] 6.7 Verify build: `dotnet build Pulsar/Pulsar/Pulsar.csproj`

## 7. C# Refactoring — Dialog ViewModels

- [ ] 7.1 Inject `ILocalizationService` into `AddSlotViewModel`; convert all header, status, button, validation text
- [ ] 7.2 Inject `ILocalizationService` into `SlotConfigurationDialogViewModel`; convert header, status, disclosure text
- [ ] 7.3 Inject `ILocalizationService` into `PluginSettingViewModel`; convert all validation messages (~20 strings)
- [ ] 7.4 Inject `ILocalizationService` into `SecretPickerViewModel`; convert delete confirmation
- [ ] 7.5 Inject `ILocalizationService` into `PluginMarketViewModel` / `ExternalPluginManagerViewModel`; convert status/dialog strings
- [ ] 7.6 Inject `ILocalizationService` into `EditProfileViewModel` / `InputProfileViewModel`; convert dialog titles
- [ ] 7.7 Inject `ILocalizationService` into `FirstLaunchSetupWizardViewModel`; convert header/error strings
- [ ] 7.8 Verify build: `dotnet build Pulsar/Pulsar/Pulsar.csproj`

## 8. C# Refactoring — Services & Other ViewModels

- [ ] 8.1 Inject `ILocalizationService` into `ActionFeedbackService`; convert all feedback messages (~12 strings)
- [ ] 8.2 Inject `ILocalizationService` into `TrayIconService`; convert tooltip and menu item labels (3 strings)
- [ ] 8.3 Inject `ILocalizationService` into `SettingsPageCatalog`; convert page titles (4 strings)
- [ ] 8.4 Inject `ILocalizationService` into `RadialMenuViewModel`; convert center text and sub-menu text (~5 strings)
- [ ] 8.5 Inject `ILocalizationService` into `SlotViewModel` / `SlotPresentation`; convert health badge text (3 strings)
- [ ] 8.6 Inject `ILocalizationService` into `TutorialStepLoader`; convert step titles (6 strings)
- [ ] 8.7 Inject `ILocalizationService` into `RadialMenuVisualStateCoordinator`; convert idle title
- [ ] 8.8 Inject `ILocalizationService` into `PluginViewModel` (settings); convert usage/health display strings
- [ ] 8.9 Convert `ProfilesConfig.cs` `QuickEditBadgeText` and `SummaryFallbackText`
- [ ] 8.10 Verify build: `dotnet build Pulsar/Pulsar/Pulsar.csproj`

## 9. Language Switching UI & Persistence

- [ ] 9.1 Add `SupportedLanguages` and `SelectedLanguage` properties to `SettingsViewModel`
- [ ] 9.2 Create `LanguageDisplayModel` class (Code + DisplayName) in ViewModels or Models
- [ ] 9.3 Add language ComboBox to `SettingsGeneralPage.xaml` Appearance section, bind to `SettingsViewModel`
- [ ] 9.4 Wire `SelectedLanguage` setter to call `_localizationService.SetLanguage()` and `_configService.SaveAsync()`
- [ ] 9.5 Verify language persists across app restart
- [ ] 9.6 Verify build: `dotnet build Pulsar/Pulsar/Pulsar.csproj`

## 10. Hot-Switch Window Refresh

- [ ] 10.1 Verify LocExtension updates all target properties on LanguageChanged
- [ ] 10.2 Verify Settings window refreshes all pages when language switches
- [ ] 10.3 Verify open dialogs refresh when language switches
- [ ] 10.4 Verify radial menu refreshes when language switches
- [ ] 10.5 Handle edge case: language switch while no settings window is open (should not crash)

## 11. Validation & Cleanup

- [ ] 11.1 Run full build: `dotnet build Pulsar/Pulsar/Pulsar.csproj` — zero errors
- [ ] 11.2 Manual QA: launch app in English, verify all pages display English
- [ ] 11.3 Manual QA: switch to Chinese, verify all pages display Chinese
- [ ] 11.4 Manual QA: switch back to English, verify hot-switch works
- [ ] 11.5 Manual QA: switch language then save config, restart app, verify language persists
- [ ] 11.6 Manual QA: test fallback chain — temporarily delete a key from zh-CN, verify English shows
- [ ] 11.7 Run existing tests: `dotnet test` — verify no regressions
