## 1. Settings Shell Foundation

- [x] 1.1 Define a centralized settings page registration model with stable page identifiers and metadata.
- [x] 1.2 Add a dedicated settings shell service or shell ViewModel to own current page selection, initial page resolution, and navigation requests.
- [x] 1.3 Update `SettingsWindow` to use the shell navigation layer instead of relying on scattered page tags and editor-owned view switching.

## 2. Editor and Dirty-State Integration

- [x] 2.1 Extract shell navigation responsibilities out of `SettingsViewModel` while preserving existing configuration editing behavior.
- [x] 2.2 Define a guard contract that allows the shell to query the editor for unsaved changes before page navigation or window close completes.
- [x] 2.3 Update unsaved-change flows so page switching prompts the user to save, discard, or cancel when required.

## 3. Local UI Preferences

- [x] 3.1 Introduce a local UI preferences persistence service dedicated to device-local shell state.
- [x] 3.2 Persist and restore the last-opened settings page through the new local preferences service.
- [x] 3.3 Add best-effort handling for missing or invalid local preference data so the settings shell falls back safely.

## 4. Staged Startup Coordination

- [x] 4.1 Identify and document current startup responsibilities in `App.xaml.cs` as blocking or deferred.
- [x] 4.2 Introduce a startup coordinator abstraction that sequences blocking initialization separately from deferred warm-up tasks.
- [x] 4.3 Move conservative non-critical startup work into deferred execution while keeping logging, plugin readiness, tray startup, and input readiness correct.

## 5. Verification

- [ ] 5.1 Verify settings navigation, last-page restoration, and dirty-state prompts across all existing settings pages.
- [x] 5.2 Verify startup still initializes plugins, tray services, hotkeys, and tutorial entry conditions correctly after coordination changes.
- [x] 5.3 Update any affected documentation that describes settings architecture or startup responsibilities.
