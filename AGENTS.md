# AGENTS.md - Operational Guide for AI Agents

Operational guide for agents working on the **Pulsar** codebase (.NET 8, WPF/WinForms, MVVM + DI).

---

## 1. Project Snapshot

**Pulsar** is a high-performance productivity launcher for Windows featuring a radial menu interface.

- **Framework**: .NET 8.0 (WPF + WinForms), MVVM (CommunityToolkit.Mvvm) + Dependency Injection
- **Core Features**: Radial Menu, Global Hotkeys, PKI/Secret Management, Extensible Plugin System
- **Key Primitives**:
  - **PulsarContext**: Immutable context snapshot captured at radial menu invocation (lazy-loaded). Per-execution correlation data (PluginId, Action, ExecutionId) lives in stack-scoped `PluginExecutionContext` (AsyncLocal), never on `PulsarContext`.
  - **Plugin Tiers**: Core (essential, fail-fast) vs Extension (optional, Circuit Breaker: 3 crashes in 1 min = 60s disable)
  - **Configuration**: `Profiles.json` - single source of truth

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

### Localization
- NEVER hardcode user-facing strings in C# or XAML. Use `ILocalizationService` (`_loc["Key"]`, XAML `{lex:Locale Key}`) or plugin parameter label conventions.
- Plugin metadata labels auto-localize by convention: parameter → `SlotParam.{AlphaNumOnly(Label)}`, action → `SlotAction.{AlphaNumOnly(Label)}`; fallback = raw label text.
- Plugin error/success messages via `PluginResult.Error()` / `PluginResult.Ok()` MUST use `ILocalizationService`. If matched by `ActionFeedbackService`, update BOTH the plugin and the pattern matching (bilingual).
- Adding translations: add `<data>` to `Resources/Strings.resx` + `Resources/Strings.zh-CN.resx`; name `Category.SubCategory.Description`; `{0}`/`{1}` placeholders with `string.Format(...)`.

**Key files**: `Resources/Strings.resx`, `Resources/Strings.zh-CN.resx`, `Core/Localization/LocalizationService.cs`, `Core/Localization/LocExtension.cs`, `Models/SlotParameterEditorModels.cs` (convention lookup, lines 48-58)

---

## 3. Critical Pitfalls (Top-5 + pointer)

> Full table lives in [Docs/lessons/](./Docs/lessons/) (one file per pitfall, symptom → root cause → fix). Only the highest-frequency traps are inlined here.

| Symptom | Root cause / Fix |
|---|---|
| Theme DynamicResources missing, blank visuals | `ApplyTheme()` ran **before** `InitializeComponent()` (Page load replaces its Resources). Call it **after**. → [WPF_THEME_INJECTION_PITFALLS.md](./Docs/lessons/WPF_THEME_INJECTION_PITFALLS.md) |
| First tray right-click menu shows wrong theme | `ThemeService.CurrentTheme` not initialized from `Profiles.json` before `TrayIconService.BuildContextMenu()`. Bootstrap `IThemeService.Initialize(config.Settings.ThemeEnum)` in `AppStartupCoordinator` before `ITrayService.Initialize()`. → [WPF_THEME_INJECTION_PITFALLS.md](./Docs/lessons/WPF_THEME_INJECTION_PITFALLS.md) |
| Pulsar buttons: accent-on-accent (蓝底蓝字) or fallback greys | `Accent*` Fluent tokens don't resolve: `ThemeService.ApplyAccent` must bridge `UiApplication.Current.Resources` → `Application.Current.Resources`; button text on accent fill uses `TextOnAccentFillColorPrimaryBrush`, never `AccentTextFillColorPrimaryBrush`. → [WPF_FLUENT_ACCENT_TOKENS_UNRESOLVED.md](./Docs/lessons/WPF_FLUENT_ACCENT_TOKENS_UNRESOLVED.md) |
| `Profiles.json` reverts after deleting slots/settings | `HotkeyService` holds a stale `_config`; `UpdateHotkey()` re-saves it. In `SettingsViewModel.Save()` use `RebuildCache()` (refreshes `_config` from `_configService.Current`) instead of `UpdateHotkey()`. → [HOTKEY_SERVICE_STALE_CONFIG_OVERWRITE.md](./Docs/lessons/HOTKEY_SERVICE_STALE_CONFIG_OVERWRITE.md) |
| Runtime plugin uninstall/overwrite-install fails | Plugin DLLs stay locked (collectible ALC never unloaded). Uninstall: revoke permissions → `DeactivatePluginAsync` (full teardown incl. ALC unload) → delete with retry; force `GC.Collect()` before delete; null `descriptor.ImplementationType` before `RemoveDescriptor`. → [PLUGIN_RUNTIME_INSTALL_UNINSTALL_PITFALLS.md](./Docs/lessons/PLUGIN_RUNTIME_INSTALL_UNINSTALL_PITFALLS.md) |

**All lessons** (theme, config, plugin lifecycle, async shutdown, input injection, …): [Docs/lessons/](./Docs/lessons/)

---

## 4. Task Router & Docs Index

| Task | Where |
|---|---|
| Build/Run commands | [Docs/ops/BUILD_AND_RUN.md](./Docs/ops/BUILD_AND_RUN.md) |
| Add/modify plugin | [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md), [Docs/architecture/PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md) |
| Add dialog | [Docs/architecture/DIALOG_SYSTEM.md](./Docs/architecture/DIALOG_SYSTEM.md) - **Always specify DialogSizeConstraints AND register DataTemplate!** |
| Modify UI (XAML) | [Docs/guides/UI_BEST_PRACTICES.md](./Docs/guides/UI_BEST_PRACTICES.md), [Docs/guides/COMPONENT_LIBRARY.md](./Docs/guides/COMPONENT_LIBRARY.md) |
| Radial Menu interaction/state | `ViewModels/MenuSession.cs` (state machine), `ViewModels/RadialMenuViewModel.cs` (thin binding projection). Strategies depend on `IMenuSession`, never the VM. [008-menu-session-refactor.md](./Docs/decisions/008-menu-session-refactor.md) |
| Config persistence/writes | `ConfigService.cs` + `ConfigEditSession.cs`. `GetSnapshot()` = deep copy, never mutate; all writes via `ConfigEditSession` (revision-guarded). [009](./Docs/decisions/009-config-snapshot-seam.md), [005](./Docs/decisions/005-config-single-writer.md) |
| Input injection (PKI) | [Docs/architecture/INPUT_INJECTION.md](./Docs/architecture/INPUT_INJECTION.md) |
| WPF UI issues | [Docs/lessons/](./Docs/lessons/) |
| Architectural decisions / docs standards | [Docs/decisions/](./Docs/decisions/), [Docs/CONTRIBUTING.md](./Docs/CONTRIBUTING.md) — **Document routing** (spec vs ADR vs lessons vs journal) |
| Architecture overview | [ARCHITECTURE.md](./ARCHITECTURE.md), [Docs/README.md](./Docs/README.md) |
| Thread safety & concurrency | [Docs/architecture/PLUGIN_SYSTEM.md](./Docs/architecture/PLUGIN_SYSTEM.md) (`ConcurrentDictionary`, `Interlocked`, `Dispatcher.InvokeAsync`) |
| Propose / track a spec change | [openspec/](./openspec/) — `/opsx-propose` … `/opsx-archive` (delivery `both`, see `.opencode/commands/`) |
| Cross-session working memory | `Docs/journal/` — single store (ADR-019). `NEXT.md` + per-day files (session-journal skill, ritual §8); oversized days → `Docs/journal/archive/` (ADR-021). Never duplicate into harness-native memory. |
| Roadmap & design proposals | [Docs/roadmap/](./Docs/roadmap/), [Docs/proposals/](./Docs/proposals/) |
| Historical fix reports (not current truth) | [Docs/archive/](./Docs/archive/) — date-prefixed `YYYY-MM-DD-NAME.md` |

---

## 5. Code Style & Conventions

- C# 12 / .NET 8.0, NRT enabled, Allman braces, 4-space indent, UTF-8.
- Naming: types `PascalCase` · interfaces `I`+`PascalCase` · methods `PascalCase` (+`Async`) · props `PascalCase` · fields `_camelCase` · params/locals `camelCase` · handlers `On[EventName]`.
- Structure: `Core/` (interfaces, base types) · `Plugins/Core/` (essential: PKI, Hotkey) · `Plugins/` (extensions) · `Services/` · `ViewModels/` · `Views/` (XAML) · `Helpers/` · `Models/`.
- **DI**: constructor injection, register in `App.xaml.cs`. Plugin runtime = three narrow seams over `PluginRuntimeKernel` (ADR-012) — inject the **narrowest seam**, never the concrete class:
  - `IPluginRegistry` (register/discover/activate/query) · `IPluginExecutor` (ExecuteAsync) · `IPluginRuntimeOps` (rescan/deactivate/grant/unload).
- Circuit breaker is a **pure state machine** (ADR-013): no `ITrayService` / `IPluginHealthMonitor` / `ILocalizationService` injection; it announces `Tripped` / `Recovered`; side effects belong to `PluginBreakerNotificationService` (activated in `AppStartupCoordinator` after tray init).
- Execution correlation: `PluginExecutionContext.Current` (stack-scoped AsyncLocal, restores on Dispose).
- Thread safety: `ConcurrentDictionary`; hotkey actions via `Dispatcher.InvokeAsync()`.
- MVVM: `[ObservableProperty]` / `[RelayCommand]` (CommunityToolkit). Async: `async Task`, avoid `async void`.
- Errors: `try/catch` around volatile ops; `ILogger<T>` (never `Debug.WriteLine`).
- Native interop: `LibraryImport` / `DllImport` in `NativeMethods` classes.

---

## 6. Common Workflows

- **Add a Service**: interface in `Services/Interfaces/` → impl in `Services/` → register in `App.xaml.cs` (`ConfigureServices`).
- **Add a Plugin**: choose tier → inherit `PluginBase<T>` → constructor-inject deps → implement `ExecuteAsync()` + metadata → use `PulsarContext` only. Example: `Pulsar/Pulsar/Plugins/Extensions/Command/CommandPlugin.cs`. Deep dive: [PLUGIN_DEVELOPMENT.md](./PLUGIN_DEVELOPMENT.md) · [Docs/guides/PLUGIN_MIGRATION_GUIDE.md](./Docs/guides/PLUGIN_MIGRATION_GUIDE.md).
- **Add a Dialog**: `IDialogViewModel` in `ViewModels/Dialogs/` → UserControl in `Views/Dialogs/Contents/` → **register DataTemplate in `Views/Dialogs/DialogHostWindow.xaml`** → `DialogService.ShowCustomAsync<T>()` with `DialogSizeConstraints`. Deep dive: [Docs/architecture/DIALOG_SYSTEM.md](./Docs/architecture/DIALOG_SYSTEM.md).
- **Modify UI (XAML)**: find view in `Views/` → bind to VM → `StaticResources` from `Themes/Theme.*.xaml` → `ApplyTheme()` after `InitializeComponent()` → Pulsar button styles. Deep dive: [Docs/guides/UI_BEST_PRACTICES.md](./Docs/guides/UI_BEST_PRACTICES.md).
- **Secrets (PKI)**: `PkiPlugin` (`Plugins/Core/Pki/`) + `CredentialsManager`; `[JsonIgnore]` on sensitive data models.

**Conditional loading** (ADR-022): scenario-specific instructions (test / release / openspec) belong in skills or `.opencode/commands` slash-commands, **not** in this always-on file.

---

## 7. Error Handling & Logging (Pulsar Sentinel)

- **Serilog** structured logging → `%AppData%\Pulsar\Logs\pulsar-yyyyMMdd.log`; registered in `App.xaml.cs` via `.AddSerilog()`.
- **Global safety net** in `App.xaml.cs`: 1) `DispatcherUnhandledException` (UI, logs Fatal) 2) `UnobservedTaskException` (background, logs Error) 3) `AppDomain.UnhandledException` (catastrophic, logs Fatal).
- Usage: constructor-inject `ILogger<T>`; `LogInformation` / `LogError(ex, ...)` in catch blocks.
- **Circuit Breaker**: extension plugins crashing 3× in 1 min auto-disable 60s; user notified via `ITrayService.ShowNotification` (toast); Half-Open after cooldown (single retry).

---

## 8. Agent Behavior Rules & AI-First Development

### Session Start Ritual (MANDATORY, before any real work)
- **Read the journal first (smallest slice)**: run the `session-journal` skill flow (`.agents/skills/session-journal/SKILL.md`, mirrored at `.opencode/skills/session-journal/SKILL.md`): read `Docs/journal/NEXT.md` + tail of newest `YYYY-MM-DD.md` (last `## Session` block; day files size-capped, ADR-021), summarize unfinished 下一步 to the user; don't start work contradicting an unfinished entry without confirming.
- **The skill may be missing from the host's injected skill list** — locate it at the path above and read it before acting; the ritual is governed by `Docs/journal/` + ADR-019, not by the host's list.
- **Session end**: append a `## Session (HH:MM)` block (做了什么 / 关键决策·坑 / 相关引用, **≤ ~25 lines**) and update `Docs/journal/NEXT.md`. Only append; never rewrite or delete past entries.
- **Also peek at `openspec/changes/`** for an active change and mention it when relevant.

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
- **Debug via Logs**: add `ILogger` debug statements along the call chain first; locate from Serilog output (`%AppData%\Pulsar\Logs\pulsar-yyyyMMdd.log`) before guessing.

---

## 9. Quick Commands

**Recommended**: `scripts/dev.ps1` — wraps build/test/commit, auto-repairs Windows env vars stripped by sandboxed shells (fixes NuGet `Value cannot be null (path1)` crash). From bash:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev.ps1 build
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev.ps1 test                          # full suite (~23 s)
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev.ps1 test --filter "FullyQualifiedName~HotkeyService"
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev.ps1 commit -Message "..." [-All]  # -All = include untracked (git add -A)
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev.ps1 all                           # build + full test
```

Direct commands (sandboxed shells may need the env-prefix workaround — see the `scripts/dev.ps1` header):

```bash
dotnet build Pulsar/Pulsar/Pulsar.csproj
dotnet run --project Pulsar/Pulsar/Pulsar.csproj
dotnet restore Pulsar/Pulsar/Pulsar.csproj
```

**Full reference**: [Docs/ops/BUILD_AND_RUN.md](./Docs/ops/BUILD_AND_RUN.md)

---

## 10. Parallel Agent & Git Worktree Discipline (concurrency)

Multiple AI harnesses / agents work this repo in parallel. Rules to avoid cross-agent clobbering, stale working trees, and journal divergence (ADR-021).

- **One worktree per concurrent agent, each on its own branch.** Never run two agents in the same working directory. Main worktree = integration + journal keeper. Example: `git worktree add -b feat/<name> E:\...\Pulsar_Project_wt`.
- **Branch discipline**: one worktree per branch; never check out the same branch in two worktrees (stale HEAD). Rebase onto `main`, fast-forward merge (or PR) when done. Keep `main` green.
- **Build/test isolation is free**: each worktree has its own `bin/obj`; shared `.git` objects and NuGet cache are read-safe → parallel `dotnet build` / `dotnet test` do not clobber each other.
- **`Docs/journal/` + `CHANGELOG.md` are committed on `main` only.** Feature worktrees read them via `git fetch` + `git show main:Docs/journal/…` for the session ritual; never commit a divergent copy on their own branch. Journal is append-only, so even a rare divergence merges cleanly.
- **Before writing journal / before merging**: `git status` + `git fetch`; confirm no uncommitted change from another harness.
- **Access**: agents can operate inside any worktree via absolute paths (`git -C <worktree> …`).

---

## Agent skills

> **These two files are machine-consumed skill contracts, NOT reading material.** Written and read by the vendored skills under `.agents/skills/` (notably `setup-matt-pocock-skills`, `domain-modeling`, `grill-with-docs`, `improve-codebase-architecture`). Do not relocate or delete — paths are hardcoded; re-running `setup-matt-pocock-skills` recreates them.

- **Issue tracker**: Issues and PRDs live as GitHub issues; see [`Docs/agents/issue-tracker.md`](./Docs/agents/issue-tracker.md) — `gh` CLI conventions for skill-driven issue operations.
- **Domain docs**: single-context — `CONTEXT.md` + `docs/adr/` at the repo root; see [`Docs/agents/domain.md`](./Docs/agents/domain.md).

---

*Last Updated: 2026-09-05*
*Version: 4.0.0 (Rules-stack slimming (ADR-022): §3 pitfalls table → Top-5 + pointer to Docs/lessons; §5/§6/§7 tightened; conditional-loading principle added. Invariants (§2), router (§4), ritual (§8), dev.ps1 (§9), worktree discipline (§10) preserved.)*
