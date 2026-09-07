# Slot Dialog Refactor Handoff

## What changed

- Reworked slot creation into a unified two-step wizard:
  - Step 1: choose behavior and fill required details
  - Step 2: polish appearance and save
- Reworked slot editing into a single-page editor instead of a three-step wizard.
- Removed the old create flow that added a slot first and asked the user to edit it afterward.
- Creation now happens on a draft `PluginSlot` and is committed only after confirmation.

## Primary files touched

- `Pulsar/Pulsar/ViewModels/Dialogs/AddSlotViewModel.cs`
- `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml`
- `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml.cs`
- `Pulsar/Pulsar/ViewModels/Dialogs/SlotConfigurationDialogViewModel.cs`
- `Pulsar/Pulsar/Views/Dialogs/Contents/SlotConfigurationDialogContent.xaml`
- `Pulsar/Pulsar/ViewModels/SettingsViewModel.cs`

## Key architectural decisions

- Keep the refactor inside existing dialog infrastructure instead of introducing a new window host.
- Use a draft slot for creation so unsaved changes are not written into `CurrentSlots` too early.
- Preserve existing metadata-driven parameter rendering and picker logic.
- Preserve delete as an edit-only action in the single-page editor.

## Validation performed

- `dotnet build "Pulsar/Pulsar/Pulsar.csproj" -p:OutDir="G:\0_Playground\Pulsar_Project\artifacts\slot-refactor-build\\"`
- Build succeeded with `0` warnings and `0` errors.

## Notes

- Default `bin/Debug` build was locked by a running `Pulsar.exe`, so validation used a temporary output directory.
- The repository already had unrelated local changes before this refactor; they were left untouched.

## Suggested next improvements

- Add selected-card visual state for slot type cards in the create dialog.
- Add plugin-metadata-driven create options so `BuildAddSlotOptions()` no longer duplicates display text.
- Consider extracting shared preview/parameter section templates used by create and edit dialogs.
- Add UI tests or ViewModel tests for creation blocking rules and draft commit behavior.
