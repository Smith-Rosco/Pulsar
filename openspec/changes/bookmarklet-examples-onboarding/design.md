## Context

See proposal.md - Why. The web-script pillar needs starting content and a guided path. Existing pieces to build on:
- `DemoScripts/browser_demo.js` — a ready example script to seed the library.
- `TutorialScenarioRegistry` + `TutorialStepLoader` + `Resources/Tutorial/TutorialSteps.*.json` — the established onboarding scenario mechanism (see `scenario-core` / `scenario-excel-vba`); scenario instances are added as first-party registrations without changing the model.
- `scenario-prerequisite-validation` already provides a browser check usable by the new scenario.
- `bookmarklet-script-editor` (sibling change) defines `ScriptFileService` (`%APPDATA%\Pulsar\Scripts\`) and the in-app editor — import hands the copy to it.

## Goals / Non-Goals

**Goals:**
- Ship a small built-in example library (browse + one-click import into the user's scripts directory, opening in the editor).
- Add a first-web-script onboarding scenario reusing the existing tutorial mechanism (metadata + steps JSON + browser prerequisite).

**Non-Goals:**
- No change to `scenario-core` requirements (reuse, don't modify).
- No change to execution or plugin contracts.
- No marketplace/remote example download; examples are first-party assets shipped with the app.

## Decisions

1. **Example library = static catalog service over bundled assets.**
   An `ExampleLibraryService` registers curated examples in code (id + localized title/description), with content read from bundled `.js` assets (extend the existing `DemoScripts/` files). Mirrors `TutorialScenarioRegistry`.
   *Alternative considered*: a JSON manifest + dynamic loading — unneeded for a small first-party set; code registration is the established pattern.

2. **Import writes a copy via `ScriptFileService`, never the built-in.**
   Import copies the example content to `%APPDATA%\Pulsar\Scripts\` under a distinct name (suffix on collision) and opens the result in the editor. Built-in assets stay read-only.
   *Alternative considered*: referencing built-ins directly — rejected; users must own their scripts.

3. **Onboarding scenario = a new first-party scenario registration.**
   Add the web-script scenario to `TutorialScenarioRegistry` with `StepsJsonPath = Resources/Tutorial/TutorialSteps.webscript.json` and a browser `PrerequisiteProvider`; the steps JSON follows the standard 6-step structure with web-script copy and a primary `com.pulsar.bookmarklet` `run` slot.
   *Alternative considered*: extending the existing browser scenario — rejected; keeps the web-script walkthrough distinct and independently testable.

4. **Browser prerequisite reuses the existing checker.**
   The scenario's `PrerequisiteProvider` uses the browser detection already provided by the prerequisite-validation capability; unmet → readable message, no silent failure.

## Risks / Trade-offs

- [Example content rots as pages change] → keep examples deliberately small and page-agnostic (form-fill, data extraction primitives); documented as starting points, not evergreen automations.
- [Scenario ordering conflicts with existing scenarios] → new scenario is added to the registry without reordering existing ones; first-launch still picks a primary scenario.
- [Import naming surprises] → deterministic suffix strategy documented in the import UI copy.

## Migration Plan

- Additive: new assets + new scenario registration + new service. Existing configs and scenarios untouched; no rollback path needed beyond not registering the scenario.

## Open Questions

- Exact set of initial examples (which legacy-page tasks to cover first) — a content decision finalized at implementation; does not change the spec.
- Whether the web-script scenario appears in first-launch by default or is user-invoked later — a UX-selection decision safe to finalize during apply.
