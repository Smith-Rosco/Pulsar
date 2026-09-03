## Context

See proposal.md - Why. Bookmarklet scripts are currently external `.js` files referenced by the `run` action's `scriptPath` parameter (`BookmarkletRunnerPlugin`). `ScriptPreprocessor.ProcessScriptContent(content, logger)` already validates content (empty check, BOM removal, `javascript:` prefix handling) and returns `ValidationResult { IsValid, ProcessedScript, Errors, Warnings }` — the single validation source the editor will reuse. The app has an established dialog pattern (AGENTS.md): `IDialogViewModel` + UserControl in `Views/Dialogs/Contents/` + DataTemplate registration in `DialogHostWindow.xaml` + `DialogService.ShowCustomAsync<T>()` with `DialogSizeConstraints`.

## Goals / Non-Goals

**Goals:**
- Provide an in-app editor to create/open/edit/save `.js` bookmarklet scripts under `%APPDATA%\Pulsar\Scripts\`.
- Reuse `ScriptPreprocessor` as the one validation engine (no duplicated logic between editor and runner).
- Add run-time parameter interpolation without changing the execution mechanism.

**Non-Goals:**
- No change to `bookmarklet-runner-execution` requirements (UIA injection / fail-fast / retry-safety stay).
- No syntax-highlighting IDE; a functional editor with validation is the target.
- No change to `Profiles.json` or the plugin contract.

## Decisions

1. **Reuse `ScriptPreprocessor.ProcessScriptContent` for live validation.**
   The editor calls the same validator as the runner; errors/warnings surface inline but do not block saving (spec: user may save with issues shown).
   *Alternative considered*: a lighter inline regex validator — rejected; would drift from the runner's rules.

2. **Editor UI follows the existing dialog pattern.**
   A `BookmarkletScriptEditorViewModel : IDialogViewModel` + `BookmarkletScriptEditorContent` UserControl registered in `DialogHostWindow.xaml`, opened via `DialogService.ShowCustomAsync<T>()` with explicit `DialogSizeConstraints`.
   *Alternative considered*: a new settings page — rejected; the editor is a focused authoring action, and the dialog pattern matches the slot editor precedent.

3. **Scripts stored under `%APPDATA%\Pulsar\Scripts\`.**
   New scripts save there with a `.js` extension (the runner's own `Placeholder` already points at `%APPDATA%\Pulsar\Scripts\example.js`), so saved files are immediately selectable through the `run` file picker.
   *Alternative considered*: storing content in `Profiles.json` — rejected; keeps config lean and files editable outside Pulsar.

4. **Parameterization via `{{name}}` placeholders interpolated from slot arguments.**
   Scripts may declare `{{name}}` placeholders; at run time the runner interpolates them from the slot's parameter values, and a missing value fails with a readable message through the standard action-feedback path.
   *Alternative considered*: prompt-at-run dialogs per placeholder — rejected for scope; slot-argument interpolation covers the primary "fill form/login" cases.

5. **Entry point from the web-scripts settings surface.**
   A "New/Edit script" entry on the Bookmarks/web-scripts settings surface opens the editor; all strings via `ILocalizationService`.

## Risks / Trade-offs

- [Placeholder interpolation collides with real script syntax] → use a distinctive delimiter (`{{...}}`) and escape support (`{{{{` → literal `{{`), documented in the editor help text.
- [Validation stricter than user expects] → editor shows warnings non-blocking; runner keeps its existing fail-fast contract.
- [Scripts directory grows untracked] → clear naming on save; a future "manage scripts" list is deferred.

## Migration Plan

- Additive: new editor + storage directory; existing `scriptPath`-based slots keep working unchanged (no migration).

## Open Questions

- Exact placeholder delimiter and whether interpolation applies to all bookmarks or is opt-in per script — safe to finalize at implementation; does not change the spec.
- Whether the editor is opened from a dedicated settings page entry or the Add-Slot flow — a UI-placement decision for apply.
