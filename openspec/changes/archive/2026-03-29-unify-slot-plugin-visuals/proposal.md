## Why

Pulsar currently presents built-in plugin identity inconsistently across the Create Slot flow and the Plugins settings page. Create Slot also assigns strong default plugin colors to new slots, which makes plugin branding compete with user-authored slot appearance and weakens the product's visual hierarchy.

## What Changes

- Unify built-in plugin display identity so Create Slot and the Plugins settings page use the same canonical icon, name, description, and category source.
- Remove strong plugin-specific default colors from the Create Slot picker and from newly created slot drafts so slot color remains optional user-authored polish.
- Align Create Slot plugin-type cards with the established plugin visual language while preserving the page's selection-oriented layout.
- Replace duplicated slot-plugin display mappings in view models and slot suggestion helpers with metadata-driven behavior where plugin identity is concerned.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `plugin-display-identity`: built-in plugin icon and related display identity must remain canonical across plugin management and slot creation surfaces.
- `slot-editor-information-hierarchy`: Create Slot must present plugin selection with a more restrained visual treatment so optional appearance choices do not dominate the authoring flow.
- `slot-editor-shared-icon-rendering`: Create Slot must use the same icon source and rendering semantics as other plugin-facing surfaces rather than page-specific icon overrides.

## Impact

- Affected code includes `Pulsar/Pulsar/ViewModels/SettingsViewModel.cs`, `Pulsar/Pulsar/ViewModels/Dialogs/AddSlotViewModel.cs`, `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml`, `Pulsar/Pulsar/Views/Pages/SettingsPluginsPage.xaml`, and supporting plugin metadata/display helpers.
- This change reduces duplicated icon/color mappings and clarifies the boundary between plugin identity metadata and per-slot appearance customization.
- No external API changes are expected, but built-in plugin metadata becomes the required source of truth for plugin visuals in slot authoring surfaces.
