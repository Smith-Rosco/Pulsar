## Why

Plugin slots in Pulsar already support execution arguments, but the current settings UX does not explain that capability clearly enough for users to discover, understand, or safely configure it. This is becoming a product usability issue now because multiple built-in plugins already depend on action parameters, while the current editor still presents them as sparse, plugin-specific text boxes with limited guidance and mostly runtime-only error feedback.

## What Changes

- Introduce a first-class slot parameter authoring experience in the slot editor so users can see required vs optional parameters, understand what each parameter does, and learn the expected input format without reading code or documentation.
- Add action-aware parameter presentation so plugins with multiple execution modes can expose the relevant parameter set for the selected action instead of showing a static, partially explained field layout.
- Define plugin action parameter metadata as a formal contract that can describe parameter names, types, required state, display labels, help text, examples, picker hints, and validation rules.
- Add pre-save and in-editor validation for slot parameters so common configuration mistakes are caught before execution, while preserving runtime validation as a final safety net.
- Align existing built-in plugins and slot editing flows with the new metadata contract, including unifying mismatched parameter terminology where needed.

## Capabilities

### New Capabilities
- `slot-parameter-authoring`: Define and present plugin slot action parameters as metadata-driven, user-friendly forms with guidance, examples, and validation.

### Modified Capabilities

## Impact

- Affected UI: `Pulsar/Pulsar/Views/Pages/SettingsSlotsPage.xaml`, slot template selection, and slot editing presentation in settings.
- Affected models/metadata: plugin metadata contracts, action/parameter schema definitions, plugin metadata registry usage, and slot validation pipeline behavior.
- Affected plugins: at minimum `com.pulsar.winswitcher`, `com.pulsar.command`, `com.pulsar.pki`, `com.pulsar.bookmarklet`, and `com.pulsar.vbarunner`.
- Affected UX flows: add-slot flow, edit-slot flow, save validation, and error feedback before execution.
