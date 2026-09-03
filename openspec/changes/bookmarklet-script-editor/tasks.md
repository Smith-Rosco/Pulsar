## 1. Script storage & validation reuse

- [ ] 1.1 Add a `ScriptFileService` under `Services/` that resolves/saves `.js` files under `%APPDATA%\Pulsar\Scripts\` (create dir, unique names, `.js` extension); verify a unit test writes and re-reads a saved script
- [ ] 1.2 Expose live validation through `ScriptPreprocessor.ProcessScriptContent` from the editor layer; verify a unit test that valid content passes and invalid content returns errors/warnings

## 2. Editor UI (dialog pattern)

- [ ] 2.1 Add `BookmarkletScriptEditorViewModel : IDialogViewModel` (new/open/edit/save state, inline validation feed) and verify its state transitions via unit tests (no XAML)
- [ ] 2.2 Add `BookmarkletScriptEditorContent` UserControl in `Views/Dialogs/Contents/`, register its DataTemplate in `DialogHostWindow.xaml`, and open via `DialogService.ShowCustomAsync<T>()` with explicit `DialogSizeConstraints`; verify the app builds and the dialog opens in a manual smoke test
- [ ] 2.3 Add an entry point ("New/Edit script") on the web-scripts settings surface and verify it opens the editor

## 3. Run-time parameter interpolation

- [ ] 3.1 Add `{{name}}` placeholder interpolation in the bookmarklet run path, reading values from the slot's parameter arguments (with `{{{{` escape); verify a unit test that a value is interpolated into the executed payload
- [ ] 3.2 Verify a missing-placeholder-value run fails with a user-meaningful message through the standard action-feedback path (unit test asserting plugin failure result)

## 4. Localization & integration

- [ ] 4.1 Add editor UI strings (titles, buttons, validation hints, help text) to `Strings.resx` + `Strings.zh-CN.resx`; verify no hardcoded user-facing strings in the new code/XAML
- [ ] 4.2 Run full test suite (`dotnet test`) and verify all tests pass with 0 warnings/errors; manual smoke: create a script in-app, save it, select it through `run`, and confirm it executes in a browser
