## Context

The completed `improve-plugin-parameter-ux` change established metadata-driven slot parameters, inline validation, and required/optional/advanced groupings. That solved discoverability, but it also shifted too much configuration depth into the expanded slot card on `SettingsSlotsPage.xaml`. The slots page is fundamentally a collection-management surface where users scan many slots, compare health states, reorder items, and make quick edits. The current expanded presentation breaks that rhythm by allowing a single slot to become a long form with explanatory text, validation copy, multiple parameter sections, appearance controls, and movement actions all at once.

This follow-up change is cross-cutting because it affects slot card composition, parameter summarization rules, dialog usage, plugin metadata expectations, and the contract between plugin authors and the settings UI. It must also preserve the metadata-first direction from the previous change rather than reintroducing plugin-specific UI branching.

## Goals / Non-Goals

**Goals:**
- Preserve list scanability on the slots page even when plugins expose many parameters or verbose help text.
- Split editing into a lightweight inline quick-edit layer and a dedicated full-configuration layer without changing the runtime slot execution contract.
- Define deterministic presentation rules so metadata can drive which parameters appear in summaries, quick edit, and advanced configuration.
- Require plugin metadata to expose enough information for summary generation, parameter prioritization, and escalation to advanced editing.
- Keep complex editing flows aligned with existing dialog infrastructure already used elsewhere in settings.

**Non-Goals:**
- Redesigning all settings surfaces outside slot management.
- Replacing metadata-driven parameter rendering with per-plugin hard-coded templates.
- Changing `PluginSlot.Args` or plugin `ExecuteAsync(action, args, context)` semantics.
- Building a new side-panel or multi-window configuration shell in this change.
- Solving every third-party plugin authoring edge case beyond a safe baseline metadata contract.

## Decisions

### 1. Treat the slots page as a scan-and-orchestrate surface first

Decision:
- Slot cards SHALL prioritize scanning, status recognition, ordering, and quick adjustment over full parameter authoring.

Rationale:
- The page is used to manage multiple slots at once, not to deeply edit a single slot in isolation.
- Large expanded cards break list rhythm, hide neighboring slots, and reduce users' ability to compare state across the collection.

Alternatives considered:
- Keep the current full editor inline and try to reduce spacing: rejected because density alone does not solve the cognitive and structural overload.
- Move all editing into dialogs: rejected because high-frequency edits like label or action changes would become too click-heavy.

### 2. Use a two-layer editing model: quick edit inline, full configuration in a dialog

Decision:
- Expanded slot cards SHALL expose only a compact quick-edit layer, while complete parameter authoring and verbose explanations move to a dedicated configuration dialog.

Rationale:
- Inline editing remains efficient for frequent, low-friction changes.
- A dialog creates a focused editing context for long help text, advanced parameters, picker-driven fields, and multi-step correction workflows.
- The project already uses `IDialogService` and dialog view models for focused configuration tasks, so this fits existing interaction patterns.

Alternatives considered:
- Automatically choose either inline or dialog per plugin with no common quick-edit layer: rejected because it creates inconsistent interaction expectations.
- Use a right-side details panel instead of a dialog: rejected for now because it is a larger layout change and conflicts with the current page composition.

### 3. Introduce explicit presentation tiers driven by metadata

Decision:
- Each parameter SHALL be classifiable into summary, quick-edit, or full-configuration presentation tiers using metadata plus shared heuristics.

Rationale:
- Field count alone is not enough; a small number of fields can still be complex if they require long guidance, pickers, or dependency reasoning.
- A tiered model gives the UI a stable decision framework while keeping rendering metadata-driven.

Alternatives considered:
- Use only raw parameter count thresholds: rejected because it ignores explanation density and interaction complexity.
- Let every plugin define custom tiering logic in code: rejected because it would re-fragment the authoring contract.

### 4. Summaries must report configuration state, not reproduce the form

Decision:
- Slot cards SHALL show concise summary tokens and validation state rather than full parameter values or long prose.

Rationale:
- Scanability depends on stable height and high signal-to-noise information.
- For many parameters the useful list-level information is whether a required value is configured, missing, or derived from a known choice, not the entire text body.

Alternatives considered:
- Render full required parameter values in the card body: rejected because long paths, scripts, and freeform text quickly dominate the card.
- Hide all parameter state until expansion: rejected because users need to see health and readiness at a glance.

### 5. Full configuration dialog owns long-form guidance and advanced flows

Decision:
- Action descriptions, examples, advanced parameters, multi-line validation details, and picker-heavy interactions SHALL live in the dedicated dialog rather than the list card.

Rationale:
- These elements are valuable for correctness but expensive in list real estate.
- A focused surface improves comprehension and gives the UI room to organize fields without harming the parent collection view.

Alternatives considered:
- Keep advanced parameters collapsed inside the inline card: rejected because the card still becomes a nested mini-editor and drags complexity back into the list.

### 6. Extend plugin metadata requirements to support layered editing

Decision:
- Plugin slot parameter metadata SHALL include presentation-oriented hints such as user-facing labels, short summaries, priority/tier hints, advanced/edit escalation hints, and disclosure-safe state text where needed.

Rationale:
- The UI cannot summarize or prioritize fields well if plugin metadata only describes validation and control type.
- This creates a clear plugin development contract so built-in and future plugins feed the layered editor consistently.

Alternatives considered:
- Infer everything from existing metadata names and required flags: rejected because that is too lossy for meaningful summaries and quick-edit selection.

## Risks / Trade-offs

- [Quick edit becomes too shallow for common workflows] -> Keep the quick-edit layer focused on the highest-frequency fields and add a prominent transition into full configuration.
- [Metadata requirements become burdensome for plugin authors] -> Keep the new contract intentionally narrow and document defaults/fallback behavior for omitted hints.
- [Summary logic leaks sensitive or overly verbose values] -> Favor state-oriented summaries such as configured/missing/selected labels and allow metadata to mark values as non-displayable.
- [Dialog editing feels disconnected from the list] -> Preserve slot identity, action, and validation context in the dialog header and return users to the same card on close.
- [Legacy plugins cannot fully participate] -> Provide conservative fallback behavior that still supports full configuration even when quick-edit summaries are minimal.

## Migration Plan

1. Define the layered editing requirements and plugin metadata obligations in specs.
2. Add or extend presentation metadata so built-in plugin parameter definitions can expose summary and quick-edit intent.
3. Refactor the slot card to show compact summaries, status, and limited inline editing.
4. Introduce a dedicated dialog flow for full parameter editing and connect it from each slot card.
5. Validate that existing built-in plugins still load saved slot arguments and produce useful summary/status output.
6. Remove or relocate long-form parameter content from the inline card once the dialog reaches parity.

Rollback strategy:
- Revert slot cards to the previous inline parameter editor while keeping runtime slot argument handling unchanged.

## Open Questions

- Should the full-configuration dialog also own appearance editing, or should icon/color remain in quick edit by default?
- Do unknown third-party plugins get only a minimal summary plus dialog fallback, or should they also receive a generic quick-edit heuristic?
- Should the layered presentation heuristics be purely metadata-driven, or should the view model maintain a small global complexity scoring fallback for incomplete plugin definitions?
