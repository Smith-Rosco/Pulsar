## Context

Pulsar's built-in plugin catalog has evolved plugin-by-plugin. As a result, display names, documentation titles, action labels, and runtime compatibility behavior are no longer aligned. `SimpleCommandPlugin` exposes a generic `run` action while `WinSwitcherPlugin` splits adjacent app-launching intents across `activate`, `launch`, and `switch`; `PkiPlugin` already distinguishes a user-facing primary action (`fill`) from a legacy alias (`inject`); `SystemCommandPlugin` still behaves like an internal command bus and relies on wrapper verbs plus nested command arguments instead of a metadata-defined action list.

This change is cross-cutting because plugin identity is surfaced in the plugin picker, metadata registry, slot editor, logs, and plugin documentation. It also touches backward compatibility: existing slot configurations may reference older action names or command shapes that should keep working even after the product converges on a cleaner user-facing contract.

## Goals / Non-Goals

**Goals:**
- Establish a consistent naming model for built-in plugins so `DisplayName`, docs, and plugin-picker language describe the same capability.
- Define a shared action taxonomy that separates primary user-facing actions from compatibility aliases and internal-only behavior.
- Make common user intents map to a clear recommended plugin/action pair in both metadata and documentation.
- Bring internal system-control commands into the same metadata-driven action model used by other built-in plugins.
- Preserve existing slot execution compatibility wherever reasonable while removing ambiguity from the authoring experience.

**Non-Goals:**
- Renaming plugin IDs such as `com.pulsar.command` or `com.pulsar.winswitcher`.
- Changing the core `ExecuteAsync(action, args, context)` plugin runtime contract.
- Redesigning the plugin picker UI beyond the naming and action semantics needed to reflect the new contract.
- Solving application-launch edge cases such as Store/MSIX activation in this change unless they are required to support the new naming contract.
- Introducing a new plugin packaging system or third-party plugin migration tooling.

## Decisions

### 1. Standardize on a user-intent-first display identity

Decision:
- Built-in plugins SHALL present user-facing names that describe the capability users select in the slot editor, and docs SHALL use the same canonical names.

Rationale:
- The current mix of names such as `Simple Command`, `System Command`, `Window Switcher`, and `PKI Credentials Manager` combines implementation-centric and user-intent-centric language.
- A stable display identity reduces picker ambiguity and gives docs, metadata, and settings surfaces the same vocabulary.

Alternatives considered:
- Keep current display names and only update documentation: rejected because the ambiguity originates in the product UI itself.
- Rename plugin IDs together with display names: rejected because persisted slot configs should not need plugin-ID migration for this cleanup.

### 2. Distinguish primary actions from compatibility aliases

Decision:
- Each built-in plugin SHALL expose an explicit set of primary user-facing actions in metadata, while legacy action names remain runtime-compatible aliases when needed and are not surfaced as first-class choices in authoring flows.

Rationale:
- `PkiPlugin` already demonstrates the correct pattern: expose `fill` to the UI while still accepting `inject` at runtime.
- This keeps saved slots working while allowing the UI and docs to teach one clear action model.

Alternatives considered:
- Rename all actions and break old slots: rejected because it creates unnecessary migration risk for existing `Profiles.json` content.
- Expose every legacy alias in the UI: rejected because it preserves the ambiguity this change is meant to remove.

### 3. Keep domain-specific actions where they communicate different intent

Decision:
- The action taxonomy SHALL prefer a small, consistent verb set, but SHALL preserve distinct domain actions when they represent materially different user intent, such as `fill`, `sendkeys`, `activate`, `launch`, and `switch`.

Rationale:
- Forcing every plugin into a single `run` verb would hide important behavioral distinctions.
- The real inconsistency is not that multiple verbs exist, but that some plugins expose wrapper verbs (`run`, `execute`) while others expose user intent directly.

Alternatives considered:
- Collapse all actions to `run`: rejected because it would make the slot editor less explicit and push semantics into descriptions and parameters.
- Preserve the existing verb spread with no taxonomy: rejected because it leaves no guidance for future plugins.

### 4. Internal system controls must become explicit metadata-defined actions

Decision:
- Internal Pulsar control plugins SHALL expose explicit actions such as opening settings or quick-adding a profile directly through metadata, rather than using wrapper actions plus nested `command` arguments.

Rationale:
- `SystemCommandPlugin` currently behaves unlike every other built-in plugin and collides conceptually with the generic command-runner plugin.
- Explicit actions allow the plugin picker and docs to describe system controls using the same metadata-driven path as other plugins.

Alternatives considered:
- Leave `SystemCommandPlugin` undocumented in authoring flows and treat it as a hidden internal hook: rejected because it already has public docs and slot use cases.
- Keep the nested command pattern but improve docs: rejected because it still produces a different authoring model than the rest of the plugin system.

### 5. Recommended usage guidance belongs in metadata-adjacent documentation, not ad hoc tribal knowledge

Decision:
- The plugin contract SHALL document intent-to-plugin mapping for common jobs such as opening an app, switching to an existing app, launching a new instance, sending keys, filling credentials, running browser scripts, and running VBA scripts.

Rationale:
- The current overlap between `BasicCommand` and `WinSwitcher` makes users guess which plugin is the preferred choice for launching an application.
- A documented mapping creates a stable baseline for future runtime fixes and UX refinements.

Alternatives considered:
- Let descriptions alone imply recommended usage: rejected because descriptive text is too inconsistent and easy to drift.

## Risks / Trade-offs

- [Legacy slots still use old action names or nested system commands] -> Keep runtime alias handling in place and treat UI-facing cleanup as additive over compatibility behavior.
- [Display name changes confuse users who already recognize current plugin names] -> Update docs, picker descriptions, and action labels together so the new vocabulary appears consistently everywhere.
- [The action taxonomy becomes too abstract for plugin authors] -> Document the distinction between primary actions, aliases, and internal-only behavior in `PLUGIN_DEVELOPMENT.md` with built-in examples.
- [Overlapping launch capabilities remain confusing] -> Add explicit recommended-usage guidance that differentiates generic opening from app-switching behavior instead of pretending the overlap does not exist.
- [SystemCommand migration introduces metadata fallback inconsistencies] -> Ensure the plugin either implements `IPluginMetadataProvider` or otherwise exposes complete explicit action metadata before the UI relies on it.

## Migration Plan

1. Define the canonical plugin-display identity requirements, action semantics requirements, and internal command contract in specs.
2. Update built-in plugin metadata so each plugin exposes canonical display names and primary action labels consistent with the specs.
3. Add runtime compatibility aliases where needed so old action names or nested command shapes continue to execute.
4. Refactor `SystemCommandPlugin` into an explicit metadata-defined action model and align its docs with the new contract.
5. Update `PLUGIN_DEVELOPMENT.md` and built-in plugin docs so plugin usage guidance matches the product metadata.
6. Validate that existing built-in plugin slots still execute while newly authored slots see only the canonical naming and action model.

Rollback strategy:
- Revert metadata and documentation changes while preserving any compatibility logic added for older slots.

## Open Questions

- Should the product formally rename `Simple Command` to `Command Runner`, or is there a better canonical name that distinguishes app/file opening from shell-like command execution?
- Should `WinSwitcher` present itself as `App Switcher` or `App Control` in the UI, given that it both switches and launches?
- Should system-control actions stay under namespaced action IDs such as `pulsar.system.open_settings`, or move to shorter canonical actions with runtime aliases for the namespaced forms?
