## Context

See proposal.md - Why. Pulsar has been repositioned as an office-automation workbench (M0); a preset pack lowers the path to the first working automation.

Existing infrastructure to reuse (not reinvent):
- `CommandSlotTemplate` (`Features/Tutorial/Models/`) already models a plugin slot to create: `PluginId`, `Action`, `Args`, `LabelKey`, `IconKey`, `IsTutorialPrimary`.
- `OnboardingTemplateService.BuildInitialConfig(TutorialScenario, selectedApps)` already converts slot templates into `PluginSlot` entries written into `ProfilesConfig.Profiles["Global"].CommandMode`.
- `ConfigEditSession` is the revision-guarded single-writer path for all `Profiles.json` mutations (commit/rebase on `ConfigConcurrencyException`).
- `IPluginPermissionService` gates external-plugin permissions; install-time permission grant flow exists (`RefreshDiscoveryAsync` → grant → activate).
- `TutorialScenarioRegistry` shows the established static-registry pattern for first-party scenario/pack definitions.

## Goals / Non-Goals

**Goals:**
- Define a preset-pack model, a first-party catalog, and a one-click install/uninstall lifecycle that writes slots through `ConfigEditSession`.
- Gate pack installs that touch PKI / web-script / VBA capabilities behind `IPluginPermissionService`.
- Let first-launch seed initial configuration from a selected pack.
- Reuse `CommandSlotTemplate` + `OnboardingTemplateService` so packs and tutorials share one slot-authoring path.

**Non-Goals:**
- No new plugin-runtime contract: pack actions still run on existing plugins (`com.pulsar.vbarunner`, `com.pulsar.pki`, `com.pulsar.bookmarklet`).
- No online pack marketplace / remote download this round; only the built-in catalog.
- No change to the `TutorialScenario` model or `scenario-core` requirements.
- No removal or restructuring of existing user slots.

## Decisions

1. **Pack model = metadata + `CommandSlotTemplate` list.**
   `PresetPack` carries `Id`, `TitleKey`/`DescriptionKey`, `CommandSlotTemplates`, optional `PrerequisiteProvider`, and the permission ids its actions need. Reusing `CommandSlotTemplate` means `BuildInitialConfig` can consume a pack directly.
   *Alternative considered*: a dedicated `PresetAction` DTO — rejected as redundant duplication of the tutorial slot model.

2. **Static first-party catalog, content under `Assets/Presets/`.**
   A `PresetCatalogService` registers the initial packs in code (same pattern as `TutorialScenarioRegistry`); pack payload data (macro/vba JSON, form definitions, bookmarklet scripts) ships under `Assets/Presets/<pack-id>/`.
   *Alternative considered*: dynamic pack discovery from a plugin-style directory — deferred; unneeded for the first-party catalog and adds load-order complexity.

3. **Install writes through `ConfigEditSession`; installed state recorded in `Profiles.json`.**
   Install converts pack templates to `PluginSlot`s, appends them to the Global profile's `CommandMode` (dedupe against existing slot ids), and records `installedPresetPacks` (id + version) so uninstall can trace and remove exactly the pack's own slots. Reuses the existing revision-conflict rebase path; never mutates the file directly.
   *Alternative considered*: append directly to the loaded config — rejected, violates the single-writer invariant (AGENTS.md).

4. **Permission gating via `IPluginPermissionService` before any slot write.**
   If a pack declares permissions (e.g. PKI / web-script), install blocks until the user grants them; no slots are written before consent.
   *Alternative considered*: skip gating for first-party packs — rejected; packs still execute through external-capability plugins and must respect the same consent boundary.

5. **First-launch seeding = an additional `OnboardingTemplateService` overload.**
   `BuildInitialConfig(PresetPack pack, IReadOnlyList<OnboardingAppSelection> selectedApps)` mirrors the existing scenario overload; the first-launch wizard offers a pack selection step and passes the chosen pack through.
   *Alternative considered*: generalize `TutorialScenario` into a pack — rejected; keeps the tutorial model untouched.

## Risks / Trade-offs

- [Install conflicts with a concurrent user edit] → route through `ConfigEditSession`; on `ConfigConcurrencyException` rebase + retry; surface a readable retry message.
- [Uninstall could remove a slot the user customized] → only slots traced to the pack (via `installedPresetPacks` + slot provenance) are removed; user-modified copies are left alone, noted in the uninstall result.
- [Preset content bloat in the package] → cap the initial catalog at 3 packs (macro / form-fill / sign-in); keep payloads small and localized via resx keys.
- [Prerequisite UX: pack blocked because Excel/VBA/browser missing] → reuse the existing prerequisite-checker contract; the install UI shows which prerequisite is unmet and how to satisfy it.

## Migration Plan

- Additive only: new services, new optional `installedPresetPacks` field on `Profiles.json` (absent = no packs installed). Existing configs load unchanged; no rollback path needed beyond uninstall.
- First-launch seeding is opt-in: existing users who skip onboarding keep their config; only new first-launch flows present the pack-selection step.

## Open Questions

- Exact pack payload contents (e.g. which Excel/VBA macro template, which form fields in the sign-in flow) are deferred to implementation tasks; they do not change the spec or the architecture.
- Whether pack install is surfaced as a dedicated settings page or only inside first-launch wizard — a UI-placement decision that can be finalized during apply without affecting the spec.
