## Context

Pulsar's current settings and startup composition have grown incrementally around working product needs. `SettingsViewModel` now owns multiple responsibilities at once: shell view selection, context selection, dirty-state tracking, slot editing orchestration, dialog coordination, and configuration persistence. At the same time, `App.xaml.cs` performs a large amount of initialization work inline, including logging setup, DI registration, plugin loading, config bootstrap, tray startup, input service startup, and tutorial checks.

This design addresses three cross-cutting concerns together because they reinforce one another:

1. Settings shell navigation should become its own concern so page selection and shell state are not embedded inside the main configuration editor ViewModel.
2. Local UI-only preferences should be stored separately so shell restoration and minor UX state do not expand the responsibility of `Profiles.json`.
3. Startup responsibilities should be categorized so the application can preserve correctness while allowing non-critical warm-up to happen after core readiness.

Constraints:
- `Profiles.json` remains the authoritative business configuration store.
- Existing plugin initialization semantics, tray behavior, hotkey readiness, and tutorial behavior must keep working.
- WPF theme injection and shell composition must respect the existing project lessons in `AGENTS.md`.

Stakeholders:
- End users, who benefit from a more predictable settings shell and better startup responsiveness.
- Developers, who need smaller responsibilities, clearer extension points, and lower regression risk when evolving settings or startup flows.

## Goals / Non-Goals

**Goals:**
- Establish a dedicated settings shell layer for page registration, navigation state, and page restoration.
- Keep configuration editing responsibilities focused on configuration data and edit workflows.
- Introduce a local UI preferences service for shell-level state such as the last-opened page and window state.
- Define a staged startup model with explicit blocking and deferred phases.
- Preserve current user-visible behavior except where the new capabilities intentionally improve restoration or startup sequencing.

**Non-Goals:**
- Rewriting the complete settings experience or redesigning all settings pages.
- Replacing `Profiles.json` or changing the business meaning of existing configuration fields.
- Changing plugin contracts, plugin discovery rules, or plugin execution semantics.
- Turning startup into a fully asynchronous fire-and-forget pipeline with weaker readiness guarantees.

## Decisions

### 1. Introduce a dedicated settings shell service and shell ViewModel

The settings window will gain a shell-level abstraction responsible for:
- page registration and metadata lookup
- current page identifier
- page navigation requests
- initial page resolution
- restoration of the last-opened page from local UI preferences

The existing configuration editor ViewModel will stop owning shell navigation concerns directly. It will continue to own edit state, dirty tracking, save/discard workflows, and configuration-backed page data.

Rationale:
- This reduces the current responsibility overload in `SettingsViewModel`.
- It enables future settings search and deep-linking without further bloating the editor ViewModel.
- It aligns with the direction observed in RyTuneX, but adapted to Pulsar's WPF architecture instead of copying WinUI implementation details.

Alternatives considered:
- Keep navigation in `SettingsViewModel` and only extract helper methods: rejected because it preserves the same mixed ownership model.
- Move navigation logic entirely to code-behind: rejected because Pulsar already uses service/ViewModel-driven coordination and should remain testable.

### 2. Use centralized settings page registration

Settings pages will be registered in one place with stable identifiers and metadata. The shell uses those registrations to resolve pages for navigation and restoration.

Rationale:
- Eliminates duplicated page identifiers, `Tag` values, and navigation assumptions.
- Gives a single source for restoration, future search indexing, and shell analytics.

Alternatives considered:
- Continue using XAML `Tag` strings as the source of truth: rejected because it spreads identifiers across view markup and code.

### 3. Add a local UI preferences service separate from business configuration

The design introduces a dedicated local preferences abstraction for UI-only state. This service should be best-effort, resilient to corruption, and intentionally limited in scope.

Expected early data examples:
- last-opened settings page
- settings window size and position
- other shell-level preferences that should remain device-local

Rationale:
- Prevents `Profiles.json` from accumulating machine-local shell state.
- Makes shell restoration safe and conceptually separate from business configuration.

Alternatives considered:
- Store everything in `Profiles.json`: rejected because it weakens the boundary between business configuration and local UI preferences.
- Use ad hoc per-feature files: rejected because it creates fragmented persistence rules.

### 4. Keep unsaved-change protection owned by the editor, but enforced by the shell

The shell will not decide whether there are unsaved edits. Instead, the editor exposes a guard contract that the shell consults before navigation or close operations complete.

Rationale:
- Dirty-state truth belongs with the editor state, not the shell.
- Shell-level navigation still needs a consistent enforcement point.

Alternatives considered:
- Move dirty-state tracking into the shell: rejected because the shell should not understand page-specific edit semantics.

### 5. Introduce staged startup coordination around explicit readiness phases

Startup work will be classified into at least two buckets:
- blocking startup: required before Pulsar can safely provide core runtime behavior
- deferred warm-up: helpful but not required for initial correctness

The implementation should use a coordinator abstraction rather than continuing to let `App.xaml.cs` directly sequence every concern inline.

Likely blocking examples:
- logging bootstrap
- DI container build
- configuration availability needed by core startup
- plugin loading required for core runtime correctness
- tray and input services needed for core interaction

Likely deferred examples:
- non-critical analytics or recommendation warm-up
- secondary cache warm-up
- optional startup inspections that are not required before the app becomes usable

Rationale:
- Preserves correctness while making startup policy explicit.
- Creates a place to reason about startup work instead of encoding policy in `App.xaml.cs` ordering alone.

Alternatives considered:
- Leave `App.xaml.cs` as the orchestration root and only add comments: rejected because it does not create durable boundaries or testable policy.
- Defer plugin loading aggressively: rejected because Pulsar's plugin architecture is central to runtime behavior and must preserve readiness guarantees.

## Risks / Trade-offs

- [Settings shell split increases indirection] -> Mitigation: keep the shell abstraction narrow and avoid introducing page-specific orchestration into the shell layer.
- [Last-page restoration can interact poorly with dirty-state prompts] -> Mitigation: treat restoration as initial shell state only, and route later navigation through the unsaved-change guard contract.
- [A local preferences service may become a second general-purpose config store] -> Mitigation: document that it is only for UI-local state and keep business settings in `Profiles.json`.
- [Startup classification can be misapplied and accidentally defer required work] -> Mitigation: define explicit readiness criteria and start conservatively by deferring only clearly non-critical tasks.
- [Refactoring `SettingsViewModel` can create regressions in page behavior] -> Mitigation: extract shell responsibilities incrementally and preserve page-facing edit APIs where possible.

## Migration Plan

1. Introduce settings shell abstractions and centralized page registration without removing the existing editor logic yet.
2. Move initial page selection and shell navigation wiring from `SettingsViewModel`/window code into the shell layer.
3. Add local UI preferences storage and use it for last-page restoration first.
4. Adapt dirty-state prompting so shell navigation calls into the editor guard before page changes complete.
5. Introduce a startup coordinator and move startup responsibilities into explicit blocking/deferred groups.
6. Keep deferred work conservative in the first pass and verify that tray, hotkeys, plugins, and tutorial behavior remain correct.

Rollback strategy:
- The shell split can be rolled back by routing the settings window back to the prior navigation path while keeping page registrations unused.
- Local UI preference restoration can be disabled without affecting business configuration.
- Deferred startup tasks can be reclassified back to blocking if readiness regressions are observed.

## Open Questions

- Which startup responsibilities are safe to defer in the first implementation pass without weakening Pulsar's current readiness guarantees?
- Should settings page registration live in a dedicated service, a static registry, or be composed through DI registrations?
- Should local UI preferences use a new JSON file, a reusable settings service abstraction, or an extension of an existing config helper?
- How much of `SettingsWindow` page instantiation should move into shell services versus remain in the window layer?
