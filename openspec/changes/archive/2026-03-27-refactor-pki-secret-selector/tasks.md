## 1. Parameter model and mutation flow

- [x] 1.1 Add selector-oriented display properties to `SlotParameterEditorField` so secret-backed parameters can expose label/account display text separately from stored `Value`
- [x] 1.2 Introduce a notification-safe slot argument write path and update secret picker flows to use it instead of mutating `slot.Args[...]` directly
- [x] 1.3 Provide a shared secret metadata resolution path that merges persisted and pending secrets for dialog display

## 2. Dialog rendering

- [x] 2.1 Update `SlotConfigurationDialogContent.xaml` to branch between editable text inputs and selector-style UI for secret picker parameters
- [x] 2.2 Update `AddSlotContent.xaml` to use the same selector-style rendering so add and edit flows stay consistent
- [x] 2.3 Ensure empty-state, selected-state, and action labels (`Select` vs `Change`) are rendered clearly for secret selectors

## 3. PKI behavior alignment and verification

- [x] 3.1 Refine PKI selection behavior so chosen secret display no longer depends on mutating the slot label as a fallback display mechanism
- [x] 3.2 Verify summaries and validation still behave safely while the dialog shows human-readable secret metadata
- [x] 3.3 Run build and targeted validation of add-slot/edit-slot PKI flows to confirm immediate refresh after selecting or creating a secret
