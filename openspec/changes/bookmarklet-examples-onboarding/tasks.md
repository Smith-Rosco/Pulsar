## 1. Example library

- [x] 1.1 Add `ExampleLibraryService` under `Services/` registering curated examples (id + localized title/description) with content sourced from bundled `.js` assets (reuse/extend `DemoScripts/browser_demo.js`); verify a unit test enumerates all examples and looks one up by id (null on miss)
- [ ] 1.2 Add import that copies an example to `%APPDATA%\Pulsar\Scripts\` via `ScriptFileService` under a distinct name (suffix on collision, built-in untouched); verify a unit test that import creates the copy, keeps the built-in unchanged, and avoids name collision

## 2. Import-to-editor integration

- [ ] 2.1 Wire import to open the copied script in the in-app script editor (`bookmarklet-script-editor`) when available; verify the flow in a manual smoke test (import → editor opens with copied content)

## 3. Web-script onboarding scenario

- [ ] 3.1 Add `TutorialSteps.webscript.json` under `Resources/Tutorial/` mirroring the standard step structure with web-script copy (invoke web-script slot, run script, observe page change); verify it loads through `TutorialStepLoader`
- [ ] 3.2 Register the web-script scenario in `TutorialScenarioRegistry` with a browser `PrerequisiteProvider` and a primary `com.pulsar.bookmarklet` `run` slot; verify a unit test that the registry returns the scenario and `BuildInitialConfig` produces the primary bookmarklet slot when a browser is available
- [ ] 3.3 Verify the scenario falls back gracefully when no browser is present (readable message, no silent failure)

## 4. Localization & integration

- [ ] 4.1 Add example titles/descriptions and scenario copy to `Strings.resx` + `Strings.zh-CN.resx`; verify no hardcoded user-facing strings in new code/XAML
- [ ] 4.2 Run full test suite (`dotnet test`) and verify all tests pass with 0 warnings/errors; manual smoke: browse the example library, import an example, follow the web-script scenario steps to first run
