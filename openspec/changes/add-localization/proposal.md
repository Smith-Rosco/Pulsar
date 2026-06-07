## Why

Pulsar currently has zero localization infrastructure — all ~316 user-visible strings are hardcoded in English (with scattered Chinese strings hardcoded separately). Adding i18n support now enables Chinese-speaking users to use Pulsar in their native language, and establishes the foundation for adding more languages later with minimal effort.

## What Changes

- New `Resources/Strings.resx` and `Strings.zh-CN.resx` containing all core framework UI strings
- New `ILocalizationService` (DI singleton) wrapping ResourceManager with runtime language switching
- New `LocaleExtension` (WPF markup extension) for clean XAML binding: `{lex:Locale Key.Name}`
- New `Language` property in `ProfilesConfig.Settings` to persist user preference
- Language switcher ComboBox in General Settings page under Appearance
- All ~20 XAML files refactored to use markup extension instead of hardcoded strings
- All ~35 C# files (ViewModels, Services) refactored to use `ILocalizationService` instead of string literals
- Hot-switching: changing language immediately updates all open windows without restart
- Fallback chain: requested culture → English (default) → resource key name
- Plugin strings remain self-describing (English defaults) with an optional `IPluginLocalizationProvider` extension point for future plugin translations

## Capabilities

### New Capabilities
- `localization-infrastructure`: Core localization service, RESX resource files, WPF markup extension, DI registration, and fallback chain
- `localized-core-strings`: All framework UI strings (settings pages, dialogs, notifications, tray, radial menu, validation messages) extracted into RESX and served through ILocalizationService
- `language-switching`: User-facing language selector, runtime hot-switching without restart, persistence to Profiles.json

### Modified Capabilities
<!-- No existing specs have spec-level requirement changes. Implementation changes are internal. -->

## Impact

- **Affected code**: `App.xaml.cs` (DI registration), `ProfilesConfig.cs` (Language property), ~20 XAML files (Views/), ~35 C# files (ViewModels/, Services/), `SettingsPageCatalog.cs`, `SettingsViewModel.cs`, `PluginSettingViewModel.cs`, `ActionFeedbackService.cs`, `TrayIconService.cs`
- **New files**: `Core/Localization/ILocalizationService.cs`, `Core/Localization/LocalizationService.cs`, `Core/Localization/LocExtension.cs`, `Resources/Strings.resx`, `Resources/Strings.zh-CN.resx`
- **Dependencies**: Uses .NET's built-in `System.Resources.ResourceManager` — no external packages needed
- **Breaking changes**: None. All existing APIs and plugin contracts are preserved. XAML bindings change from string literals to markup extensions, but this is transparent at runtime.
