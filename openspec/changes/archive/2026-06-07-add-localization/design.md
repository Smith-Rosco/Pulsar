## Context

Pulsar is a WPF (.NET 8.0) application using MVVM with CommunityToolkit.Mvvm and dependency injection. Currently all ~316 user-visible strings are hardcoded — English strings in XAML attributes (`Content`, `Header`, `Title`, `Text`, `ToolTip`, `PlaceholderText`) and in C# code (notification titles/bodies, dialog titles, validation messages, tray menu items). A handful of Chinese strings are also hardcoded in tutorial and first-launch wizard code, showing ad-hoc bilingualism without infrastructure.

The project uses `DynamicResource` for theme brushes already, has a plugin system that defines its own metadata strings, and stores configuration in `Profiles.json`. Themes (`Theme.Dark.xaml`, `Theme.Light.xaml`) contain only visual tokens (colors, spacing), not string resources.

## Goals / Non-Goals

**Goals:**
- Extract all core framework UI strings into .NET RESX files with English and Chinese variants
- Support runtime language switching without application restart
- Provide a clean XAML binding syntax via WPF markup extension
- Persist language preference in existing Profiles.json configuration
- Maintain zero breaking changes to plugin contracts and existing APIs
- Establish a fallback chain (zh-CN → en → key name) so missing translations degrade gracefully

**Non-Goals:**
- Translating plugin metadata strings (plugins remain self-describing with English defaults; an optional `IPluginLocalizationProvider` extension point may be added later)
- Translating tutorial JSON content (already Chinese; can be addressed separately)
- Supporting RTL (right-to-left) languages
- Dynamic string editing UI or translation management tools
- Replacing existing hardcoded Chinese strings in first-launch wizard and permission dialogs (these are content-level strings that will be covered by core string extraction)

## Decisions

### 1. RESX over JSON for core strings

**Decision**: Use .NET RESX files (`Strings.resx`, `Strings.zh-CN.resx`) for core framework strings.

**Rationale**:
- Built-in `ResourceManager` handles culture fallback and satellite assembly loading natively
- Visual Studio/Rider provide rich RESX editing with parallel key comparison
- Compile-time key access via generated designer class (`Strings.ResourceManager`) eliminates magic-string typos
- Industry standard for .NET desktop apps — maintainable and well-understood

**Alternative considered**: Pure JSON — more flexible for runtime updates but lacks compile-time safety, requires custom serialization/deserialization, and has no built-in fallback mechanism. Rejected for core strings but remains viable for plugin translations in the future.

### 2. Custom WPF MarkupExtension over x:Static bindings

**Decision**: Create a `LocaleExtension` (inheriting `MarkupExtension`) for XAML bindings: `Text="{lex:Locale Settings.General.Title}"`.

**Rationale**:
- `x:Static` can only bind to static properties — can't support runtime language switching because the value is resolved once at XAML parse time
- MarkupExtension with `ProvideValue` can subscribe to `LanguageChanged` event and update the target dependency property, enabling hot-switching
- Cleaner syntax: single key string vs. verbose `{x:Static resx:Strings.Settings_General_Title}`

**Alternative considered**: Using `DynamicResource` — but WPF's built-in resource system doesn't support string resources with culture fallback. MarkupExtension is the standard pattern for WPF localization (similar to `{l:Translate Key}` in many open-source WPF i18n libraries).

### 3. Singleton ILocalizationService with LanguageChanged event

**Decision**: `ILocalizationService` is a DI singleton that wraps `ResourceManager`, exposes `GetString(key)`, `SetLanguage(culture)`, and fires `LanguageChanged` event on switch.

**Rationale**:
- Singleton ensures all consumers see the same current language
- `LanguageChanged` event enables both XAML (via LocExtension) and C# (via ViewModel subscriptions) to react to language switches
- `SetLanguage()` updates `Thread.CurrentThread.CurrentUICulture` so any framework calls (e.g., `ToString` with culture formatting) also respect the choice
- Integration with existing DI pattern — constructors already accept many injected services

### 4. Language stored in Profiles.json

**Decision**: Add `Language` property (string, default `"en"`) to `ProfileSettings` in `ProfilesConfig.cs`, persisted alongside other settings.

**Rationale**:
- Single source of truth for all user configuration
- Survives app restarts naturally through existing `IConfigService.SaveAsync/LoadAsync`
- Zero new persistence mechanism needed — just one property

**Alternative considered**: Windows registry or isolated settings file — rejected for violating single-config-source architecture.

### 5. Language switcher in General Settings under Appearance

**Decision**: Add a ComboBox to `SettingsGeneralPage.xaml` in the Appearance section, alongside existing Launcher Theme and Settings Theme controls.

**Rationale**:
- Language is an appearance/locale concern, logically grouped with theme selection
- SettingsGeneralPage already has the infrastructure for appearance-related controls
- SettingsViewModel already manages `GeneralSettings` and has access to DI services

### 6. Key naming convention: Module.Section.Label

**Decision**: Use dotted hierarchical key names: `Settings.General.Title`, `Dialog.ConfirmDeletion.Title`, `Validation.Selection.Required`.

**Rationale**:
- Clear grouping for maintainability in RESX editor
- Enables future tooling to auto-generate key maps
- Matches the organizational structure of the UI (settings pages → sections → labels)

### 7. Plugin strings use English defaults with optional extension point

**Decision**: Plugin metadata strings (DisplayName, Description, action labels, parameter labels) remain in plugin classes as English defaults. An optional `IPluginLocalizationProvider` interface is defined but not required — plugins that implement it can provide their own translation dictionaries merged at runtime.

**Rationale**:
- Zero breaking changes for existing plugins
- Plugin strings are dynamic and discovery-driven — loading translations from RESX at compile time doesn't work for plugin DLLs loaded at runtime
- The optional interface provides a clear extension path for plugins that want Chinese translations

## Risks / Trade-offs

- **[Risk]**: `LocaleExtension` holds weak references to targeted DependencyObjects. If not cleaned up properly, could cause memory leaks. → **Mitigation**: Use `WeakReference` pattern; unsubscribe from `LanguageChanged` when target is garbage collected. Existing WPF localization libraries (e.g., WPFSharp.Globalizer) use this pattern successfully.

- **[Risk]**: ~316 string keys is a large manual translation effort. → **Mitigation**: Start with RESX generation of all English keys (copy hardcoded strings as values). Chinese translations can be done incrementally — the fallback chain ensures English appears where Chinese is not yet translated.

- **[Risk]**: Runtime language switching may cause flicker or momentary inconsistency as windows refresh. → **Mitigation**: `LanguageChanged` fires synchronously on UI thread; all `LocaleExtension.ProvideValue` updates happen before the next render frame. UI is repainted atomically.

- **[Risk]**: SettingsPageCatalog registers page titles at construction time (before language switch can take effect). → **Mitigation**: Page titles are resolved via `ILocalizationService` when displayed, not when registered. The catalog stores keys; the UI reads values.

- **[Trade-off]**: RESX files require recompilation to add or change translations (vs. JSON which allows hot-reload). → Acceptable: translations change rarely, and the compile-time safety of RESX outweighs the flexibility of runtime-edit.

## Open Questions

- Should the first-launch setup wizard be included in this pass, or left as a separate change? (Current implementation has hardcoded Chinese strings)
- Should we add a `Reset Language to System Default` option alongside the language selector?
- Should plugin action/parameter labels be localizable in a future iteration via `IPluginLocalizationProvider`?
