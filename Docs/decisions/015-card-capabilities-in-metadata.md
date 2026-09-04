# ADR-015: Declare Plugin Card Capabilities in Metadata Instead of ID Special-Cases

**Status**: Accepted (2026-09-04)
**Date**: 2026-09-04
**Deciders**: Pulsar Development Team
**Related**: architecture review 2026-09-04 (candidate F, "worth exploring"); ADR-012 (narrow runtime seams — same review)
**Implementation**: `Core/Plugin/Metadata/PluginCapabilities.cs` (4 new UI flags), `Plugins/Core/WinSwitcher/WinSwitcherPlugin.cs`, `Plugins/Extensions/BookmarkletRunner/BookmarkletRunnerPlugin.cs` (metadata declarations), `ViewModels/Settings/PluginViewModel.cs`, `ViewModels/Settings/PluginManagerViewModel.cs`, `ViewModels/Settings/ExternalPluginViewModel.cs`, `ViewModels/Settings/ExternalPluginManagerViewModel.cs`, `ViewModels/Dialogs/PluginSettingsDialogViewModel.cs` (explicit injection + capability gating), tests in `Pulsar.Tests/ViewModels/Settings/PluginCapabilityGatingTests.cs`

---

## Context

The settings plugin card VM (`PluginViewModel`) and the generic configuration dialog (`PluginSettingsDialogViewModel`) hard-coded **concrete plugin IDs** to decide what UI a plugin card offers and which dialog "Configure" opens:

- `PluginViewModel.IsScriptEditorVisible` / `IsExampleLibraryVisible` — `Id == "com.pulsar.bookmarklet"`.
- `PluginViewModel.GetOptionsProvider()` and `Configure()` — `Id == "com.pulsar.winswitcher"` (process-list options for the `ExcludeProcesses` schema property; the custom `ProcessBlacklistViewModel` dialog).
- `PluginSettingsDialogViewModel.IsWindowInspectorVisible` — `Id == WindowInspectorViewModel.WinSwitcherPluginId`.

At the same time the same VMs hid their real dependencies behind `IServiceProvider.GetService<T>()` (`IWindowService`, `IProcessRegistryService`, `IScriptFileService`, `IScriptValidationService`, `ExampleLibraryService`, `ILogger<T>`), which the parent managers received from the container and forwarded wholesale.

Problems this creates:

- **A generic VM knows concrete plugins.** Every new plugin needing a card entry or a custom dialog forces an `if (Id == ...)` edit in the generic card code — knowledge that belongs to the plugin itself.
- **Hidden dependencies defeat the DI graph.** Tests must build a service provider with `GetService` stubs; nothing at the constructor expresses what the card actually consumes; a production `GetService` returning null silently disables features.
- **Inherited leakage to external cards.** `ExternalPluginViewModel` inherits the ID branches, so the hard-coding is not even confined to built-in cards.

The governing principle (deep modules, locality): a plugin should *self-describe* what its card can do; the generic shell renders and routes by that description.

## Decision

1. **Four UI-card capability flags are added to `PluginCapabilities` (metadata), all defaulting to `false`:**
   - `SupportsScriptEditor` — card shows the in-app script editor entry (Web Scripts).
   - `HasBuiltinExamples` — card shows the example-library entry (Web Scripts).
   - `HasCustomConfigDialog` — "Configure" opens the plugin's custom dialog (requires the host to also supply the dialog's services).
   - `SupportsWindowInspector` — the generic settings dialog shows the Window Inspector entry.

2. **The owning plugins declare their own capabilities in `GetMetadata()`:** WinSwitcher declares `HasCustomConfigDialog` + `SupportsWindowInspector`; BookmarkletRunner (Web Scripts) declares `SupportsScriptEditor` + `HasBuiltinExamples`. Because built-in descriptors are built from `GetMetadata()` at discovery (`PluginLoader.CreateDescriptor`), the flags reach the card without activation.

3. **`PluginViewModel` becomes explicit and capability-driven:**
   - `IServiceProvider` constructor parameter and the public `ServiceProvider` property are removed; the VM takes explicit optional collaborators (`ILogger<PluginViewModel>`, `IWindowService`, `IProcessRegistryService`, `IScriptFileService`, `IScriptValidationService`, `ExampleLibraryService`).
   - `IsScriptEditorVisible` / `IsExampleLibraryVisible` / `HasCustomConfigDialog` / `SupportsWindowInspector` read the metadata capabilities.
   - `GetOptionsProvider()` and the custom-dialog path in `Configure()` are gated by `HasCustomConfigDialog` (+ non-null services), no ID comparison.

4. **`PluginSettingsDialogViewModel` takes its collaborators explicitly** (`IWindowService`, `IConfigService`, `ILocalizationService`, all optional) instead of resolving through the card VM's service provider; `IsWindowInspectorVisible` reads the card's metadata capability.

5. **Managers wire explicitly.** `PluginManagerViewModel` and `ExternalPluginManagerViewModel` drop `IServiceProvider` and receive/forward the card feature services and item loggers from DI. `ExternalPluginViewModel` takes an explicit `ILogger<ExternalPluginViewModel>` and passes null feature services to the base (external plugins never declare these UI capabilities today).

6. **No behavior change for un-opted plugins.** All flags default to `false`, so any plugin that never declared a capability renders exactly as a plugin whose ID matched none of the historical strings.

## Considered Options

- **Keep the ID special-cases** (minimal churn) — rejected: exactly the finding of the review; generic UI carrying concrete plugin knowledge is the locality violation this ADR removes. It also leaked into the external-card subclass.
- **Capability flags without removing `IServiceProvider`** — rejected: the flags remove the *ID* knowledge but leave the *hidden dependency* problem (features silently vanish when `GetService` returns null; tests need a container). The review explicitly asked for both ("能力声明进 metadata；VM 显式注入").
- **A per-plugin "feature provider" interface the card calls** (e.g. `IPluginCardFeature`) — rejected as over-engineering for two features: the metadata flags + optional collaborators keep the card VM shallow, and the custom-config dialog already has a single host site (`Configure`). If a third custom card feature appears, that abstraction can be introduced then.
- **Move script-editor/example-library entry points into the plugin itself** — rejected for now: those entry points open app-shell dialogs hosted by the settings page; modeling them as plugin-provided UI is a larger surface change than the review scoped.

## Consequences

- **The generic card VM no longer knows any plugin ID.** Adding a card feature for a future plugin = declare the capability in its metadata (one file), no edits in generic VM/dialog code.
- **Dependencies are explicit and the DI graph validates them.** Tests construct the card VM without any `IServiceProvider`; a missing feature service is a null optional dependency that shows up at the construction site, not a silent `GetService` miss.
- **Self-describing metadata is testable at the source**: `WinSwitcherPlugin.GetMetadata()` / `BookmarkletRunnerPlugin.GetMetadata()` are asserted to carry the flags, and `PluginCapabilityGatingTests` proves the card follows capabilities — even a `com.pulsar.bookmarklet` Id without the flags shows nothing.
- **Behavior parity**: identical strings, dialog sizes, and routing for the two built-ins; all other plugins (external included) keep their previous rendering because flags default false. Full suite green at 0 warnings / 0 errors.
- **Residual ID references** remain only where the code *is* plugin-owned functionality (e.g. `WindowInspectorViewModel` persisting `ExcludeRules` into the WinSwitcher profile) or where a feature legitimately targets a concrete plugin (tutorials, presets, feedback patterns) — out of this ADR's scope.

---

**Change History**:
- v1.0.0 (2026-09-04): Initial version — implements architecture-review candidate F (settings card VM + generic config dialog special-cased `com.pulsar.winswitcher` / `com.pulsar.bookmarklet` and resolved collaborators through `IServiceProvider`; capabilities now declared in plugin metadata and collaborators injected explicitly).
