## Why

PKI secret selection currently renders as a generic editable text box that is bound to the raw `secretId` value. After the user picks a secret, the field can remain blank because picker updates bypass the slot change-notification path, and even a refreshed field would show an internal GUID instead of the secret label the user expects.

## What Changes

- Replace the PKI secret parameter's editable text entry with a display-only selector experience in slot add/edit dialogs.
- Separate stored parameter values from user-facing display text so secret-backed parameters persist `secretId` while showing resolved secret metadata such as label and account.
- Standardize picker-driven slot parameter updates through a notification-safe write path so dialog state, summaries, and validation refresh immediately after selection.
- Clarify slot labeling behavior so choosing a secret updates the parameter display without relying on the slot's main label as a fallback display channel.

## Capabilities

### New Capabilities
- `slot-parameter-selectors`: Render picker-driven slot parameters as selector controls with display values, immediate refresh, and PKI secret-specific presentation.

### Modified Capabilities
- None.

## Impact

- Affected UI: `Pulsar/Pulsar/Views/Dialogs/Contents/SlotConfigurationDialogContent.xaml`, `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml`
- Affected view models and models: `Pulsar/Pulsar/Models/SlotParameterEditorModels.cs`, `Pulsar/Pulsar/Models/ProfilesConfig.cs`, `Pulsar/Pulsar/ViewModels/SettingsViewModel.cs`, `Pulsar/Pulsar/ViewModels/Dialogs/SecretPickerViewModel.cs`
- Affected plugin metadata: `Pulsar/Pulsar/Plugins/Core/Pki/PkiPlugin.cs`
- No external API changes or new dependencies are expected.
