## Why

The web-script pillar has execution (bookmarklet-runner-execution) and, with `bookmarklet-script-editor`, in-app authoring — but new users still lack starting content and a guided path to "write and run your first script on a legacy web page". An example library plus an onboarding scenario closes that gap and directly serves M2's "示例库/场景引导" goal for the second pillar.

## What Changes

- **Example script library**: a built-in set of example `.js` scripts targeting common legacy web-page tasks (form fill, table/data extraction, link traversal), each with a short localized description; users browse the library and import an example into their own scripts in one click.
- **Import-to-editor integration**: an imported example opens in the in-app script editor (`bookmarklet-script-editor`) ready to edit and save under `%APPDATA%\Pulsar\Scripts\`.
- **First-web-script onboarding scenario**: a guided scenario (reusing the existing `TutorialScenario` / `TutorialStepLoader` mechanism) that walks a new user from opening a legacy page to running their first web script, with a steps JSON mirroring the existing scenario structure.

## Capabilities

### New Capabilities
- `bookmarklet-example-library`: Defines the built-in example script library — registration of curated example scripts with localized metadata, browsing, and one-click import that places an editable copy into the user's scripts directory.
- `bookmarklet-onboarding-scenario`: Defines the guided onboarding scenario for the first web script — scenario metadata, step sequence, and prerequisite handling that leads a new user to run a script on a legacy web page.

### Modified Capabilities
- None. The scenario reuses the existing `scenario-core` mechanism (registry / step loader) without changing its requirements; the example library is a new service.

## Impact

- **Assets**: curated example scripts (reuse/extend `DemoScripts/browser_demo.js`) and a new `TutorialSteps.webscript.json` under `Resources/Tutorial/`.
- **Services**: a small example-library catalog service and a web-script scenario registration (extending the `TutorialScenarioRegistry` pattern).
- **Editor integration**: import hands the copied script to `bookmarklet-script-editor`.
- **Localization**: example titles/descriptions and scenario copy in `Strings.resx` + `Strings.zh-CN.resx`.
- No `Profiles.json`, plugin-contract, or execution-mechanism changes.
