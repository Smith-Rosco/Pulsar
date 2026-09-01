# AGENTS.md - Operational Guide for AI Agents

Operational guide for agents working on the **Pulsar** codebase (.NET 8, WPF/WinForms, MVVM + DI).

---

## 1. Project Snapshot

**Pulsar** is a high-performance productivity launcher for Windows featuring a radial menu interface.

- **Framework**: .NET 8.0 (WPF + WinForms), MVVM (CommunityToolkit.Mvvm) + Dependency Injection
- **Core Features**: Radial Menu, Global Hotkeys, PKI/Secret Management, Extensible Plugin System

**Key Primitives**:
- **PulsarContext**: Immutable context snapshot captured at radial menu invocation (lazy-loaded). Per-execution correlation data (PluginId, Action, ExecutionId) lives in stack-scoped `PluginExecutionContext` (AsyncLocal), never on `PulsarContext`.
- **Plugin Tiers**: Core (essential, fail-fast) vs Extension (optional, Circuit Breaker: 3 crashes in 1 min = 60s disable)
- **Configuration**: `Profiles.json` - single source of truth

**Deep Dive**: [ARCHITECTURE.md](./ARCHITECTURE.md), [Docs/architecture/](./Docs/architecture/)

---

## 2. Non-Negotiable Invariants

### Plugins
- Never query live window state in plugins; always use `PulsarContext`.
- External plugins are permission-gated: `PluginPermissionService` blocks execution until every manifest permission is present in `PluginProfile.GrantedPermissions`. External descriptors are built from `plugin.manifest.json` without instantiating types; constructors run only after consent.
- Core plugins (`Plugins/Core/`): essential, cannot be disabled, crashes are fatal. Extension plugins (`Plugins/`): optional, Circuit Breaker protected.

**Deep Dive**: [Docs/architecture/PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md), [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md)

### UI
- **Do NOT use `Appearance="Primary"`** on Wpf.Ui buttons; use `PulsarPrimaryButtonStyle` / `PulsarSecondaryButtonStyle` / `PulsarDangerButtonStyle` (dynamic resource inheritance breaks with Multi-Headed UI → invisible text on hover).
- Multi-Headed UI: `App.xaml` has NO global styles; inject themes manually via `IThemeService.ApplyTheme()` for every Window/Page.
- Call `ApplyTheme()` **after** `InitializeComponent()` for Pages (loading a Page replaces its Resources dictionary).

**Deep Dive**: [Docs/lessons/WPFUI_BUTTON_PRIMARY_BUG.md](./Docs/lessons/WPFUI_BUTTON_PRIMARY_BUG.md), [Docs/lessons/WPF_THEME_INJECTION_PITFALLS.md](./Docs/lessons/WPF_THEME_INJECTION_PITFALLS.md)

### Localization
- NEVER hardcode user-facing strings in C# or XAML. Use `ILocalizationService` (`_loc["Key"]`, XAML `{lex:Locale Key}`) or plugin parameter label conventions.
- Plugin metadata labels auto-localize by convention: parameter → `SlotParam.{AlphaNumOnly(Label)}`, action → `SlotAction.{AlphaNumOnly(Label)}`; fallback = raw label text.
- Plugin error/success messages via `PluginResult.Error()` / `PluginResult.Ok()` MUST use `ILocalizationService`. If matched by `ActionFeedbackService`, update BOTH the plugin and the pattern matching (bilingual).
- Adding translations: add `<data>` to `Resources/Strings.resx` (EN) + `Resources/Strings.zh-CN.resx` (ZH); name `Category.SubCategory.Description`; use `{0}`/`{1}` placeholders with `string.Format(...)`.

**Key files**: `Resources/Strings.resx`, `Resources/Strings.zh-CN.resx`, `Core/Localization/LocalizationService.cs`, `Core/Localization/LocExtension.cs`, `Models/SlotParameterEditorModels.cs` (convention lookup, lines 48-58)

**Deep Dive**: [Docs/architecture/PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md)

---

## 3. Critical Pitfalls (quick reference)

| Symptom | Root cause / Fix | Deep Dive |
|---|---|---|
| Theme DynamicResources missing, blank visuals | `ApplyTheme()` ran **before** `InitializeComponent()` (Page load replaces its Resources). Call it **after**. | [WPF_THEME_INJECTION_PITFALLS.md](./Docs/lessons/WPF_THEME_INJECTION_PITFALLS.md) |
| First tray right-click menu shows wrong theme | `ThemeService.CurrentTheme` not initialized from `Profiles.json` before `TrayIconService.BuildContextMenu()`. Bootstrap `IThemeService.Initialize(config.Settings.ThemeEnum)` in `AppStartupCoordinator` before `ITrayService.Initialize()`. | [WPF_THEME_INJECTION_PITFALLS.md](./Docs/lessons/WPF_THEME_INJECTION_PITFALLS.md) |
| XAMLParseException "Resources property can only be set once" | Mixed `<ResourceDictionary>` wrapper with resources outside it. Put everything inside one `<ResourceDictionary>`. | [WPF_RESOURCES_HYGIENE.md](./Docs/lessons/WPF_RESOURCES_HYGIENE.md) |
| Buttons inside UserControl have `Command = NULL` | UserControls break `RelativeSource` visual-tree bindings. Bridge via code-behind `Loaded` event → set `Tag`. | [WPF_USERCONTROL_BINDING_BREAKS.md](./Docs/lessons/WPF_USERCONTROL_BINDING_BREAKS.md) |
| ContextMenu items unstyled | ContextMenu renders in a separate visual tree (Popup). Manually inject `ui:ControlsDictionary` into `ContextMenu.Resources`. | [CONTEXTMENU_RESOURCE_INHERITANCE.md](./Docs/lessons/CONTEXTMENU_RESOURCE_INHERITANCE.md) |
| `Profiles.json` reverts after deleting slots/settings | `HotkeyService` holds a stale `_config`; `UpdateHotkey()` re-saves it. In `SettingsViewModel.Save()` use `RebuildCache()` (refreshes `_config` from `_configService.Current`) instead of `UpdateHotkey()`. | [HOTKEY_SERVICE_STALE_CONFIG_OVERWRITE.md](./Docs/lessons/HOTKEY_SERVICE_STALE_CONFIG_OVERWRITE.md) |
| Settings save fails ("保存更改失败"), esp. 2nd consecutive save | Long-lived `ConfigEditSession` commits against a stale revision; every save bumps `ConfigService.CurrentRevision`. `CommitAsync` re-arms revision after success; on `ConfigConcurrencyException` `RebaseAsync` merges untouched regions and `SettingsViewModel` retries. Never commit unchanged drafts. | [CONFIG_EDIT_SESSION_STALE_REVISION.md](./Docs/lessons/CONFIG_EDIT_SESSION_STALE_REVISION.md) |
| Pulsar buttons: black text on accent bg, hover/checked no text change | Template text presenter was a `ContentPresenter`; string→TextBlock conversion freezes foreground. Use explicit `<TextBlock Text="{TemplateBinding Content}"/>` or set `Foreground` on the control. | [WPF_BUTTON_TEMPLATE_FROZEN_FOREGROUND.md](./Docs/lessons/WPF_BUTTON_TEMPLATE_FROZEN_FOREGROUND.md) |
| Pulsar buttons: accent-on-accent text (蓝底蓝字) or fallback greys | `Accent*` Fluent tokens don't resolve: `ApplicationAccentColorManager` writes to a detached dict unless the `Application` merges a `"wpf.ui;"` dict (Pulsar's `App.xaml` doesn't). `ThemeService.ApplyAccent` must bridge `UiApplication.Current.Resources` → `Application.Current.Resources`; button text on accent fill must use `TextOnAccentFillColorPrimaryBrush`, never `AccentTextFillColorPrimaryBrush` (that's accent-coloured text for links). | [WPF_FLUENT_ACCENT_TOKENS_UNRESOLVED.md](./Docs/lessons/WPF_FLUENT_ACCENT_TOKENS_UNRESOLVED.md) |
| Scrollbars visible despite `VerticalScrollBarVisibility="Hidden"` | Internal control templates override implicit styles. Hide ScrollViewers at runtime via code-behind visual-tree walk. | [WPF_SCROLLVIEWER_VISIBILITY.md](./Docs/lessons/WPF_SCROLLVIEWER_VISIBILITY.md) |

**More lessons**: [ASYNC_SHUTDOWN_DEADLOCK.md](./Docs/lessons/ASYNC_SHUTDOWN_DEADLOCK.md) · [FOREGROUND_WINDOW_ACTIVATION_RELIABILITY.md](./Docs/lessons/FOREGROUND_WINDOW_ACTIVATION_RELIABILITY.md) · [SENDINPUT_FOREGROUND_ACTIVATION.md](./Docs/lessons/SENDINPUT_FOREGROUND_ACTIVATION.md) · [POWERSHELL_5_1_COMPRESS_ARCHIVE_BROKEN.md](./Docs/lessons/POWERSHELL_5_1_COMPRESS_ARCHIVE_BROKEN.md) · [GH_CLI_HASH_PATH_BUG.md](./Docs/lessons/GH_CLI_HASH_PATH_BUG.md) · [WINDOW_ELIGIBILITY_PHYSICAL_RULE.md](./Docs/lessons/WINDOW_ELIGIBILITY_PHYSICAL_RULE.md) · [WPF_FLUENT_ACCENT_TOKENS_UNRESOLVED.md](./Docs/lessons/WPF_FLUENT_ACCENT_TOKENS_UNRESOLVED.md)

---

## 4. Task Router & Docs Index

| Task | Where |
|---|---|
| Build/Run commands | [Docs/ops/BUILD_AND_RUN.md](./Docs/ops/BUILD_AND_RUN.md) |
| Add/modify plugin | [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md), [Docs/architecture/PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md) |
| Add dialog | [Docs/architecture/DIALOG_SYSTEM.md](./Docs/architecture/DIALOG_SYSTEM.md) - **Always specify DialogSizeConstraints AND register DataTemplate!** |
| Modify UI (XAML) | [Docs/guides/UI_BEST_PRACTICES.md](./Docs/guides/UI_BEST_PRACTICES.md), [Docs/guides/COMPONENT_LIBRARY.md](./Docs/guides/COMPONENT_LIBRARY.md) |
| Radial Menu interaction/state | `ViewModels/MenuSession.cs` (state machine), `ViewModels/RadialMenuViewModel.cs` (thin binding projection). Strategies depend on `IMenuSession`, never the VM. [Docs/decisions/008-menu-session-refactor.md](./Docs/decisions/008-menu-session-refactor.md) |
| Config persistence/writes | `Services/ConfigService.cs`, `Services/ConfigEditSession.cs`. `GetSnapshot()` = deep copy, never mutate; all writes via `ConfigEditSession` (revision-guarded). [Docs/decisions/009-config-snapshot-seam.md](./Docs/decisions/009-config-snapshot-seam.md), [005-config-single-writer.md](./Docs/decisions/005-config-single-writer.md) |
| Input injection (PKI) | [Docs/architecture/INPUT_INJECTION.md](./Docs/architecture/INPUT_INJECTION.md) |
| WPF UI issues | [Docs/lessons/](./Docs/lessons/) |
| Architectural decisions / docs standards | [Docs/decisions/](./Docs/decisions/), [Docs/CONTRIBUTING.md](./Docs/CONTRIBUTING.md) |
| Architecture overview | [ARCHITECTURE.md](./ARCHITECTURE.md), [Docs/README.md](./Docs/README.md) |
| Thread safety & concurrency | [Docs/architecture/PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md) (`ConcurrentDictionary`, `Interlocked`, `Dispatcher.InvokeAsync`) |

---

## 5. Code Style & Conventions

### General
- C# 12 / .NET 8.0, Nullable Reference Types enabled, Allman braces, 4-space indent, UTF-8.
- Naming: types `PascalCase` · interfaces `I`+`PascalCase` · methods `PascalCase` · async suffix `Async` · props `PascalCase` · fields `_camelCase` · params/locals `camelCase` · handlers `On[EventName]`.

### Project Structure
`Core/` (interfaces, base types, plugin core) · `Plugins/Core/` (essential infra: PKI, Hotkey) · `Plugins/` (extension plugins) · `Services/` (business logic) · `ViewModels/` (CommunityToolkit.Mvvm) · `Views/` (XAML) · `Helpers/` (static utils) · `Models/` (DTOs, config)

### Coding Patterns
- **DI**: constructor injection, register in `App.xaml.cs`. Plugin runtime via `serviceCollection.AddPluginRuntime(pluginDir)` in `App.xaml.cs`.
- Inject `IPluginRegistry` interface, not the concrete class.
- Execution correlation: `PluginExecutionContext.Current` (stack-scoped AsyncLocal, restores previous scope on Dispose).
- Thread safety: `ConcurrentDictionary`; hotkey actions dispatch via `Dispatcher.InvokeAsync()`.
- MVVM: `[ObservableProperty]` / `[RelayCommand]` from CommunityToolkit.
- Async: `async Task` for I/O; avoid `async void` except event handlers.
- Errors: `try/catch` around volatile ops; `ILogger<T>` (never `Debug.WriteLine`).
- Native interop: `LibraryImport` / `DllImport` in `NativeMethods` classes.

---

## 6. Common Workflows

### Adding a Service
1. Interface in `Services/Interfaces/` → 2. impl in `Services/` → 3. register in `App.xaml.cs` (`ConfigureServices`).

### Adding a Plugin (modern)
1. Choose tier (Core vs Extension) → 2. inherit `PluginBase<T>` in the right location → 3. constructor-inject dependencies → 4. implement `ExecuteAsync()` + metadata → 5. use `PulsarContext` only (never live window state).

**Example**: `Pulsar/Pulsar/Plugins/Extensions/Command/CommandPlugin.cs`, `CommandPluginMetadata.cs`

**Deep Dive**: [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md) · [Docs/guides/PLUGIN_MIGRATION_GUIDE.md](./Docs/guides/PLUGIN_MIGRATION_GUIDE.md) · [Docs/architecture/PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md)

### Adding a Dialog
1. `IDialogViewModel` in `ViewModels/Dialogs/` → 2. UserControl in `Views/Dialogs/Contents/` → 3. **register DataTemplate in `Views/Dialogs/DialogHostWindow.xaml`** → 4. `DialogService.ShowCustomAsync<T>()` with `DialogSizeConstraints`.

**Deep Dive**: [Docs/architecture/DIALOG_SYSTEM.md](./Docs/architecture/DIALOG_SYSTEM.md)

### Modifying UI (XAML)
1. Find view in `Views/` → 2. bind to VM properties → 3. `StaticResources` from `Themes/Theme.*.xaml` → 4. `ApplyTheme()` after `InitializeComponent()` → 5. Pulsar button styles.

**Deep Dive**: [Docs/guides/UI_BEST_PRACTICES.md](./Docs/guides/UI_BEST_PRACTICES.md)

### Secrets (PKI)
`PkiPlugin` (`Pulsar/Pulsar/Plugins/Core/Pki/`) + `CredentialsManager`; use `[JsonIgnore]` on sensitive data models.

---

## 7. Error Handling & Logging (Pulsar Sentinel)

- **Serilog** structured logging; logs at `%AppData%\Pulsar\Logs\pulsar-yyyyMMdd.log`; registered in `App.xaml.cs` via `.AddSerilog()`.
- **Global safety net** in `App.xaml.cs`: 1) `DispatcherUnhandledException` (UI, logs Fatal, keeps app alive) 2) `UnobservedTaskException` (background, logs Error, prevents termination) 3) `AppDomain.UnhandledException` (catastrophic, logs Fatal before crash).
- Usage: constructor-inject `ILogger<T>`; `LogInformation` / `LogError(ex, ...)` in catch blocks.

### Circuit Breaker
Extension plugins crashing 3x in 1 min are auto-disabled for 60s; user notified via `ITrayService.ShowNotification` (Windows Toast); Half-Open after cooldown (single retry).

---

## 8. Agent Behavior Rules & AI-First Development

### The "AI Programming Triangle" (MUST follow when building features / fixing bugs)
1. **Isolate Side-Effects (Everything is Mockable)**: never couple code to OS APIs (`SendKeys`, `Process.Start`, `File.Write`, Registry, UI Automation). Define interfaces (`IInputSimulator`, `IClipboardMonitor`, `IProcessLauncher`) + Windows impl; verify with `Moq` in `Pulsar.Tests`.
2. **ViewModel Unit Testing (State over UI)**: state transitions verifiable without touching XAML. xUnit tests in `Pulsar.Tests/ViewModels/`; invoke commands programmatically and assert state.
3. **Headless Execution & Self-Correction**: run plugins without the WPF shell: `dotnet run --project Pulsar/Pulsar.Simulator/Pulsar.Simulator.csproj -- --plugin "com.x" --args "{...}"`; iterate on JSON output until `"Success": true`.

**Standard sequence**: 1. Interfaces + failing tests → 2. implement → 3. `dotnet test` + simulator until green → 4. bind XAML → ask human for visual QA.

### General Agent Rules
- **Proactiveness**: fix obvious issues (missing null checks, etc.) when spotted.
- **Context**: always read files before editing to preserve local conventions.
- **Safety**: never commit secrets or API keys.
- **Validation**: run `dotnet build` after changes.
- **Documentation**: update relevant docs on architectural changes.
- **Debug via Logs**: when fixing a bug, add `ILogger` debug statements along the call chain first and locate the issue from Serilog output (`%AppData%\Pulsar\Logs\pulsar-yyyyMMdd.log`) before guessing — rely on logs, not speculation.

---

## 9. Quick Commands

```bash
dotnet build Pulsar/Pulsar/Pulsar.csproj
dotnet run --project Pulsar/Pulsar/Pulsar.csproj
dotnet restore Pulsar/Pulsar/Pulsar.csproj
```

**Full reference**: [Docs/ops/BUILD_AND_RUN.md](./Docs/ops/BUILD_AND_RUN.md)

---

## Agent skills

- **Issue tracker**: Issues and PRDs live as GitHub issues; see `docs/agents/issue-tracker.md`.
- **Domain docs**: single-context — `CONTEXT.md` + `docs/adr/` at the repo root; see `docs/agents/domain.md`.

---

*Last Updated: 2026-08-28*
*Version: 3.0.2 (Added debug-via-logs rule; generic agent rules moved to user-level AGENTS.md)*
