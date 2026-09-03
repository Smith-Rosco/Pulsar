## Why

Legacy web-page scripts (the workbench's second pillar) can only be referenced as external `.js` files via the `scriptPath` parameter — users must leave Pulsar and edit scripts in an external editor, with no in-app authoring, validation, or parameterization. An in-app script editor removes that friction and makes "write your first web script" a within-Pulsar task, matching the M2 goal of lowering the barrier to the first automation.

## What Changes

- **In-app script editor**: a built-in editor surface (dialog/page) to create, open, edit, and save `.js` bookmarklet scripts, with scripts stored under `%APPDATA%\Pulsar\Scripts\`.
- **Live validation**: validate script content as the user edits, reusing `ScriptPreprocessor.ProcessScriptContent` (syntax/`javascript:` prefix handling) and surfacing errors/warnings inline.
- **Parameterization**: support placeholders in scripts that are interpolated with slot arguments at run time (a script can prompt for or read per-invocation values).
- **Run integration**: scripts created/edited in Pulsar become selectable through the existing `run` action's file picker; the execution path itself is unchanged.

## Capabilities

### New Capabilities
- `bookmarklet-script-editor`: Defines the in-app script authoring capability — create/open/edit/save scripts, live content validation against the same rules the runner enforces, and run-time parameter interpolation — without changing the execution mechanism.

### Modified Capabilities
- None. `bookmarklet-runner-execution` (UIA injection, fail-fast, retry-safety, action feedback) stays as-is; the editor only produces the same kind of script files the runner already consumes.

## Impact

- **UI**: new editor ViewModel + view (dialog following `DialogService`/`DialogSizeConstraints` conventions), plus an entry point from the Bookmarks/web-scripts settings or slot picker.
- **Plugin**: `BookmarkletRunnerPlugin` metadata may gain a "new script" action/entry hint; the `run` parameter set is unchanged.
- **Validation**: reuse `ScriptPreprocessor.ProcessScriptContent`; no duplicate validation logic.
- **Storage**: scripts written under `%APPDATA%\Pulsar\Scripts\`; no change to `Profiles.json`.
- **Localization**: editor UI strings in `Strings.resx` + `Strings.zh-CN.resx`.
