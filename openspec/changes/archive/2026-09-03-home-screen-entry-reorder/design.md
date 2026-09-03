## Context

See proposal.md - Why. M0 re-narrated Pulsar as an office-automation workbench; this change reorders in-app entry points so the three pillars surface first.

Existing structure to build on:
- `SettingsPageCatalog` (`Services/`) registers settings pages in a fixed order (General → Slots → Plugins → Analytics → About) consumed by `SettingsWindow` to build navigation.
- `PluginManagerViewModel` already exposes `GroupedPlugins` (`ObservableCollection<PluginGroup>`) plus a flat `Plugins` list — a grouping seam already exists; this change defines what the groups/order are.
- First-launch wizard: `FirstLaunchSetupWizardViewModel` presents onboarding usage selection (`OnboardingUsageProfile`).
- Localization via `ILocalizationService` (`_loc["Key"]`); no hardcoded user-facing strings.

## Goals / Non-Goals

**Goals:**
- Define a stable pillar-priority mapping (three office pillars first, system behind) and apply it to settings navigation, the plugin management list, and first-launch presentation.
- Reuse the existing `GroupedPlugins` mechanism instead of introducing a new UI model.
- Keep the change purely presentational: no configuration-format, plugin-contract, or data-model changes.

**Non-Goals:**
- No change to radial-menu interaction or slot layout.
- No change to `settings-shell-navigation` / `plugin-display-identity` requirements (mechanism and identity stay as-is).
- No user-visible reordering of custom user slots.

## Decisions

1. **Single pillar-priority mapping (source of truth).**
   Add a small `WorkbenchPillar` catalog (e.g. `Services/WorkbenchPillarCatalog`) mapping the office pillars to their plugin ids and entry ids: VBA/macro (`com.pulsar.vbarunner`), web scripts (`com.pulsar.bookmarklet`), form-fill/PKI (`com.pulsar.pki`) as front-line; everything else (winswitcher, command, system) as background. All surfaces read ordering from this one catalog.
   *Alternative considered*: inline ordering in each ViewModel — rejected, would drift across surfaces.

2. **Settings navigation reorder via `SettingsPageCatalog`.**
   Update the page registration list so workbench-oriented entries lead and analytics/about trail, and expose the grouping through the registration. `SettingsWindow` continues to render in catalog order.
   *Alternative considered*: hardcoding order in XAML nav items — rejected; catalog is the single source today.

3. **Plugin list ordering inside the existing `GroupedPlugins`.**
   When `PluginManagerViewModel` rebuilds groups, order groups by pillar priority (pillar group first, system group after) and order members within each group deterministically. No new UI control; only the data feeding the existing grouped view changes.
   *Alternative considered*: a new grouped control with headers — unnecessary; `PluginGroup` already carries the grouping surface.

4. **First-launch option order aligned to the narrative.**
   Reorder how `FirstLaunchSetupWizardViewModel` presents usage profiles so office-automation options appear first. Keep behavior (selection → `BuildInitialConfig`) unchanged.
   *Alternative considered*: rewriting the wizard — out of scope; only presentation order changes.

5. **Localized framing through `ILocalizationService`.**
   Update entry titles/descriptions in `Strings.resx` + `Strings.zh-CN.resx` to workbench framing; code/XAML keep referencing keys only.
   *Alternative considered*: inline strings — violates the localization invariant (AGENTS.md).

## Risks / Trade-offs

- [Pillar ids hardcoded and drift with plugin renames] → centralized `WorkbenchPillarCatalog`; renames update one file.
- [Existing tests assert the old order (e.g. settings catalog / grouped plugins)] → update those assertions to the new deterministic order as part of the change; document the new order in the tasks.
- [System plugins become hard to find] → keep them grouped and labeled, not hidden; trailing group is still discoverable via search/filter.

## Migration Plan

- Purely presentational and additive; no config migration. The change is deployed with the next release; the new order is immediately visible after build.

## Open Questions

- Exact group labels (e.g. "办公自动化 / Office Automation" vs "系统 / System") — a wording choice finalized during apply; does not affect the spec.
- Whether analytics/about should be a separate trailing group or merged with system — a presentation detail, safe to decide at implementation.
