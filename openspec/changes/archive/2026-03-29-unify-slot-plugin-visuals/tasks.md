## 1. Canonical Plugin Identity Wiring

- [x] 1.1 Audit built-in plugin metadata and runtime icon values, then normalize any mismatched canonical icon/name/description/category fields that Create Slot and the Plugins page must share.
- [x] 1.2 Replace `SettingsViewModel.BuildAddSlotOptions()` page-local plugin identity data with a metadata-driven source for Create Slot plugin types.
- [x] 1.3 Introduce or refine a shared display helper/model so Create Slot and plugin-management surfaces consume the same canonical built-in plugin identity fields and icon-rendering inputs.

## 2. Slot Draft Defaults And Suggestion Ownership

- [x] 2.1 Remove strong plugin-specific default colors from slot draft construction in `SettingsViewModel.BuildSlotTemplate()` while preserving valid draft initialization.
- [x] 2.2 Refactor `AddSlotViewModel` suggestion logic so base plugin identity values come from canonical metadata and only slot-specific overrides remain in suggestion helpers.
- [x] 2.3 Preserve action-specific icon suggestions that are truly slot-specific, and verify known cases such as command `sendkeys` continue to surface the intended icon after selection.

## 3. Create Slot Visual Alignment

- [x] 3.1 Update `AddSlotContent.xaml` plugin-type cards to use a restrained neutral baseline treatment that emphasizes canonical iconography, naming, and selection state instead of plugin accent fill.
- [x] 3.2 Ensure the Create Slot preview and draft state remain visually valid when no user-selected slot color is present.
- [x] 3.3 Verify that the Plugins page and Create Slot present the same built-in plugin identity while remaining appropriate for their different workflows.

## 4. Validation

- [x] 4.1 Add or update tests covering metadata-driven Create Slot option generation, canonical icon reuse across settings surfaces, and no forced plugin-color default on new slot drafts.
- [ ] 4.2 Run the relevant build and test validation, then manually verify Create Slot and the Plugins page for icon consistency, restrained picker styling, and optional slot color behavior.
