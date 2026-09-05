---
type: "Reference"
title: "Settings Shell & Slot Editor"
openwiki_generated: true
verified:
  - by: openwiki/0.5.0
    at: 2026-09-05T05:46:24.085Z
sources:
  - id: openwiki-source-38411fe6ac821f934ca3a6d9
    resource: repo://Pulsar/Pulsar.Tests/Config/PluginSlotSubActionsTests.cs
  - id: openwiki-source-2a4eed5b104ed8a5fbbef33a
    resource: repo://Pulsar/Pulsar.Tests/Services/SettingsPageCatalogTests.cs
  - id: openwiki-source-04d2cd6efe08de35d8d3baeb
    resource: repo://Pulsar/Pulsar.Tests/ViewModels/SettingsViewModelDirtyStateTests.cs
  - id: openwiki-source-73f7a03c9cf199779f8625ce
    resource: repo://Pulsar/Pulsar.Tests/ViewModels/SlotEditorWorkspaceTests.cs
  - id: openwiki-source-c73febc63d68a90fe100b3c4
    resource: repo://Pulsar/Pulsar.Tests/ViewModels/SlotWheelEditorViewModelTests.cs
  - id: openwiki-source-562ca2d8594e04022c78e660
    resource: repo://Pulsar/Pulsar/App.xaml.cs
  - id: openwiki-source-61ebc43b6582ee013f887a1c
    resource: repo://Pulsar/Pulsar/Core/Messages/OpenSettingsMessage.cs
  - id: openwiki-source-66fba55a9a4d82ca4e3c44ad
    resource: repo://Pulsar/Pulsar/Core/Rendering/StyleRendererFactory.cs
  - id: openwiki-source-b36aa7b6c48f91e534c81ec2
    resource: repo://Pulsar/Pulsar/Helpers/SlotListMutator.cs
  - id: openwiki-source-518d8749f2742b670216c408
    resource: repo://Pulsar/Pulsar/Models/ProfilesConfig.cs
  - id: openwiki-source-d0dd3c16a71a9b66bf42aaab
    resource: repo://Pulsar/Pulsar/Models/SubSlotDescriptor.cs
  - id: openwiki-source-510c788765725e517011f9a2
    resource: repo://Pulsar/Pulsar/Plugins/Core/SystemCommand/SystemCommandPlugin.cs
  - id: openwiki-source-c640d149a347994c32ccf705
    resource: repo://Pulsar/Pulsar/Services/ConfigEditSession.cs
  - id: openwiki-source-840ea3f3954ba24f259ff1e6
    resource: repo://Pulsar/Pulsar/Services/ConfigService.cs
  - id: openwiki-source-ee995b3a323a35b846a6683e
    resource: repo://Pulsar/Pulsar/Services/LocalUiPreferencesService.cs
  - id: openwiki-source-5a7c5e7d99b023b9e01fa69f
    resource: repo://Pulsar/Pulsar/Services/SettingsNavigationGuard.cs
  - id: openwiki-source-283dc9200cecc4c6fcf5b9a8
    resource: repo://Pulsar/Pulsar/Services/SettingsPageCatalog.cs
  - id: openwiki-source-33d4b31b14ede3228d65f989
    resource: repo://Pulsar/Pulsar/Services/SettingsPageFactory.cs
  - id: openwiki-source-272b5bf99885f18720640a8e
    resource: repo://Pulsar/Pulsar/Services/SlotLayoutEngine.cs
  - id: openwiki-source-39d26d7a88e1b91946524f12
    resource: repo://Pulsar/Pulsar/Services/SmartSubActionDefaults.cs
  - id: openwiki-source-70c0323e92bbf706e846fce6
    resource: repo://Pulsar/Pulsar/Services/TrayIconService.cs
  - id: openwiki-source-1dca30bdf62346b0f4682216
    resource: repo://Pulsar/Pulsar/Services/Validation/ConfigValidationPipeline.cs
  - id: openwiki-source-c67693fd026b61b5a8a9377b
    resource: repo://Pulsar/Pulsar/ViewModels/Dialogs/SlotEditorViewModel.cs
  - id: openwiki-source-346477d4b03741a82887a4fa
    resource: repo://Pulsar/Pulsar/ViewModels/Dialogs/SubSlotEditorRow.cs
  - id: openwiki-source-c20d4835bdf354f19d3547f8
    resource: repo://Pulsar/Pulsar/ViewModels/Settings/SettingsEditorSession.cs
  - id: openwiki-source-804dbed754060389127c7a4e
    resource: repo://Pulsar/Pulsar/ViewModels/Settings/SettingsShellViewModel.cs
  - id: openwiki-source-c121136fda42ab3dcdbe4294
    resource: repo://Pulsar/Pulsar/ViewModels/Settings/SlotEditorWorkspace.cs
  - id: openwiki-source-4e8e3ccc0fb6f01f32eb122a
    resource: repo://Pulsar/Pulsar/ViewModels/Settings/SlotWheelEditorViewModel.cs
  - id: openwiki-source-319d6ca33f4279ed7bbd0256
    resource: repo://Pulsar/Pulsar/ViewModels/SettingsViewModel.cs
  - id: openwiki-source-ef527be5711e2bfa5cdfc109
    resource: repo://Pulsar/Pulsar/ViewModels/SettingsViewModel.General.cs
  - id: openwiki-source-ffcd496463813b31d36abbe8
    resource: repo://Pulsar/Pulsar/ViewModels/Strategies/CreateProfileStrategy.cs
  - id: openwiki-source-fe2a5c75f8ebf3d21f59f1e1
    resource: repo://Pulsar/Pulsar/Views/Controls/SlotWheelEditor.xaml.cs
  - id: openwiki-source-6ac556ba8563d9e7fb69aacf
    resource: repo://Pulsar/Pulsar/Views/Pages/SettingsSlotsPage.xaml
  - id: openwiki-source-3a453d2e0556172125e4f51c
    resource: repo://Pulsar/Pulsar/Views/Pages/SettingsSlotsPage.xaml.cs
  - id: openwiki-source-5cbe7e11c7d7d898a1b10689
    resource: repo://Pulsar/Pulsar/Views/SettingsWindow.xaml
  - id: openwiki-source-66819f93c0b0536cb99c1b60
    resource: repo://Pulsar/Pulsar/Views/SettingsWindow.xaml.cs
generated: { by: "openwiki/0.5.0", at: "2026-09-05T05:46:24.085Z" }
---


# Settings Shell & Slot Editor

The Settings window is the user-facing configuration surface of Pulsar. It is a
WPF `FluentWindow` whose content is a left `NavigationView` (auto-populated from a
static page catalog) hosting lazily created page instances in a `RootFrame`, with
all state owned by a transient `SettingsViewModel`. Editing state is split across
three collaborators: `SettingsShellViewModel` (navigation + unsaved-change guard),
`SettingsEditorSession` (the persistence seam wrapping `ConfigEditSession`), and
`SlotEditorWorkspace` (pure slot-editing state machine). Slot authoring — including
cascade sub-actions and per-slot cascade layout — happens inside `SlotEditorViewModel`
dialogs and the wheel-based `SettingsSlotsPage`, and only reaches `Profiles.json` on
an explicit Save through `ConfigEditSession`.

## Responsibilities and ownership boundaries

- **`SettingsWindow`** (`Pulsar/Pulsar/Views/SettingsWindow.xaml.cs`) is the shell:
  it builds `NavigationViewItem`s from the catalog, owns the `Frame` navigation and
  the custom animated nav indicator, hosts the title-bar Save button and the
  unsaved-changes badge, registers the `SnackbarMessage` channel into its
  `SnackbarPresenter`, and forwards theme/language changes to the active pages.
- **`SettingsPageCatalog`** (`Pulsar/Pulsar/Services/SettingsPageCatalog.cs`) is the
  static navigation source of truth: five registrations (Slots, Plugins, General,
  Analytics, About) in display order, grouped into Workbench (Slots + Plugins) and
  System (General + Analytics + About). Grouping is presentational only — the window
  inserts a `NavigationViewItemSeparator` at group boundaries.
- **`SettingsPageFactory`** (`Pulsar/Pulsar/Services/SettingsPageFactory.cs`)
  constructs each page with its own view-model dependencies; the window caches one
  `Page` per page id and applies the current theme to it on creation.
- **`SettingsShellViewModel`** (`Pulsar/Pulsar/ViewModels/Settings/SettingsShellViewModel.cs`)
  resolves the initial page (last-opened page id from
  `ILocalUiPreferencesService`, falling back to the catalog default `Slots`),
  performs validated navigation, and persists the last-opened page id after each
  successful user navigation.
- **`SettingsViewModel`** (`Pulsar/Pulsar/ViewModels/SettingsViewModel.cs` +
  `.General.cs`) is the transient editor view-model: it owns the working config
  draft (via the session), the slot editor workspace, general/theme/hotkey/logging
  settings bindings, dialog-driven flows, and the Save/Reset commands.
- **`SlotEditorWorkspace`** (`Pulsar/Pulsar/ViewModels/Settings/SlotEditorWorkspace.cs`)
  is the "pure-logic state machine" of slot editing: context selection, the working
  `PluginSlot` list, slot CRUD/reorder, metadata/validation/presentation refresh,
  secret staging, and dirty tracking. All collaborators are interfaces or providers
  so it can be constructed in tests without a WPF shell.
- **`SettingsEditorSession`** (`Pulsar/Pulsar/ViewModels/Settings/SettingsEditorSession.cs`)
  is the persistence seam: it owns the `ConfigEditSession` lifecycle
  (begin/lazy-begin/commit), the working draft, and the secret-store pipeline, so
  every config write from the Settings window flows through one place.

## Control flow

### Opening the window

`SettingsWindow` is registered transient (`App.xaml.cs` line 400) behind a
`Func<SettingsWindow>` factory so `CreateProfileStrategy` and other flows can open it
on demand. `TrayIconService`, the `SystemCommandPlugin`, and
`CreateProfileStrategy` all find an existing window first (via
`Application.Current.Windows.OfType<SettingsWindow>()`) and only resolve a new one
otherwise. `CreateProfileStrategy` and the system plugin then send
`OpenSettingsMessage(profileName, viewName)` through the `WeakReferenceMessenger`.

`SettingsViewModel` registers for `OpenSettingsMessage` in its constructor and, on
receipt, reloads settings (discarding previous unsaved changes), refreshes contexts,
selects the requested profile, and switches to the requested legacy view name
(`SwitchView` maps "Slots"/"Settings" through the catalog's legacy view name
resolution). This is the decoupled "open settings and jump to a profile" path.

### Navigation and the unsaved-changes guard

`SettingsWindow` builds nav items in catalog order with the page id in `Tag`;
clicking or keyboard-activating one calls
`SettingsShellViewModel.NavigateAsync(pageId, userInitiated: true)`. The shell
rejects unknown page ids, and — for user-initiated navigation — asks
`ISettingsNavigationGuard` whether it may leave the current page. The concrete
`SettingsNavigationGuard` is attached to the editor by the window constructor; when
`HasUnsavedChanges` is true it shows the Save/Don't-Save/Cancel dialog and, on Save,
calls `SettingsViewModel.Save()` (returning false only if the save fails), on
Don't-Save reloads the draft (`LoadSettings()`), and on Cancel aborts the
navigation. The same guard backs `CanCloseAsync`, which the window's `OnClosing`
uses: closing is cancelled first and re-invoked programmatically only after the
guard permits it.

Page transitions go through `NavigateToCurrentShellPage`, which creates the page via
`SettingsPageFactory.CreatePage` if not cached, applies the theme, and navigates
`RootFrame` with a fade/slide animation. A custom `NavIndicator` rectangle animates
(stretch then snap) between the old and new nav items; it is repositioned on pane
open/close, size changes, and DPI changes because it otherwise drifts (a known
defect tracked as "P2").

### Load / save lifecycle

`OnLoaded` calls `SettingsViewModel.LoadSettings()` once, then navigates to the
shell's current page. `LoadSettings` runs inside
`SlotEditorWorkspace.WithSuppressedDirtyAsync`: it loads a fresh edit-session draft
(`_session.LoadAsync()`), loads persisted secrets, hands both to
`_slotEditor.Load(config, secrets)` (which rebuilds contexts, selects the first
context, and resets dirty), then rebinds `GeneralSettings`, language, theme, and
hotkey surfaces and notifies the bound properties.

`Save()` (the `SaveCommand`, also bound to Ctrl+S in the window's
`PreviewKeyDown`) performs, in order:

1. `_slotEditor.SyncSlotsToConfig()` — flush the current slot list into the draft.
2. `_slotEditor.RefreshSlotParameterMetadata()` — re-resolve action/parameter
   metadata so valid actions are persisted.
3. `_session.CommitAsync(_slotEditor.PendingSecrets)` — merge pending secrets into
   the secret store, save them, then commit the config draft; the merged secret map
   becomes the new persisted baseline via `ReplacePersistedSecrets`.
4. `ResyncSettingsReferences()` — if a rebase replaced the `Settings` region,
   re-point the bound `GeneralSettings` at the committed draft (suppressing dirty).
5. Send `SlotsPerPageChangedMessage` so the radial menu re-lays out immediately,
   and `_hotkeyService.RebuildCache()` so hotkeys are refreshed from the current
   config rather than a stale reference.
6. `_slotEditor.ResetDirty()` and a success snackbar.

On failure, the catch block surfaces the first validation error (from
`IConfigService.LastValidationResult`) or a generic save error, and refreshes the
slot validation summaries from the validation result.

`ResetConfig` backs up `Profiles.json` to `<path>.bak`, calls
`ConfigService.ResetToFirstLaunchAsync()`, reloads the UI, and notifies.

### The transactional edit session

`ConfigEditSession` (`Pulsar/Pulsar/Services/ConfigEditSession.cs`) is the
concurrency-safe write path shared by the Settings editor and all one-shot config
mutations. `BeginAsync` snapshots the store, deep-clones it into a `Draft` and a
`Base`, and records `store.CurrentRevision`. `CommitAsync` saves the draft guarded
by that revision; on `ConfigConcurrencyException` it rebases: untouched regions
(Settings, InstalledPresetPacks, and each profile/plugin that still equals the base
snapshot) are replaced with the store's current values while user-edited regions
survive, then it retries once. After a successful commit the session re-arms to the
store's new revision so a long-lived editor can save repeatedly — this is exactly
what `SettingsSaveSessionTests.Save_SecondConsecutiveSave_ShouldAlsoPersist` and
`Save_AfterExternalWriterCommitted_ShouldPersistUserEdits_AndKeepExternalChanges`
pin down. `ConfigService.SaveAsync` performs the optimistic-concurrency check
inside its write lock, runs the `ConfigValidationPipeline` (which rejects invalid
configs), writes to a unique temp file, atomically replaces `Profiles.json`, keeps a
rolling `.bak`, and raises `ConfigUpdated` outside the lock.

`SettingsEditorSession.CommitAsync` additionally merges `PendingSecrets` into the
secret store before committing the config, so a save persists both the draft and the
staged secrets. `CommitConfigAsync` commits only the draft (used by profile
deletion). `RunAsync` runs a one-shot mutation against a short-lived session and
skips the commit when the draft is unchanged — used by the tutorial-reset flow that
must not share the editor's long-lived session.

## Slot editor workspace

### Contexts and the working slot list

`SlotEditorWorkspace.RefreshContexts` rebuilds `AvailableContexts` as three
kinds of selectable context: `Launcher` (edits `Global.SwitchMode`), `Global`
(edits `Global.CommandMode`), and one context per profile key except `"Global"`
(edits that profile's `CommandMode`), each with a slot count for display. The
previous context is re-selected when possible. Switching context first syncs the
outgoing list into the config (`SyncSlotsToConfig`, skipped while slot sync is
suppressed) and then loads the incoming context's slots ordered by `Slot` number
into `CurrentSlots`, rewiring per-slot `PropertyChanged` handlers and refreshing
metadata and context visuals — all inside `WithSuppressedDirty`, so context
switching is navigation, not an edit.

`SyncSlotsToConfig` writes the working list back into the right profile/mode,
creating the profile if needed. `OnCurrentSlotsCollectionChanged` marks dirty and
re-hooks slot property handlers; `OnSlotPropertyChanged` re-initializes metadata
and re-presents the slot on action changes and re-presents on `Item[]` (args
indexer) changes, and always marks dirty. Any user edit therefore flips
`HasUnsavedChanges` on, and `CanSave`/`SaveCommand` follow it.

### Slot CRUD, reorder, and smart defaults

- `CreateSlotDraft(pluginId)` builds a detached draft (next slot number, plugin
  icon, metadata/validation/presentation refresh) and injects smart default
  sub-actions — a draft never touches `CurrentSlots` and never marks dirty.
- `CommitCreatedSlot(slot)` renumbers, refreshes, adds to `CurrentSlots`, marks
  dirty, and sends `SlotAddedMessage` (the wheel editor listens to navigate to the
  new slot's page and flash it).
- `SetSlotAction` / `SetSlotDraftAction` swap the action and refresh metadata; the
  draft variant is used while the create dialog is still open.
- `RemoveSlot`, `MoveSlotUp/Down`, and `Reorder` (drag & drop) mutate the shared
  list through `SlotListMutator` (`Pulsar/Pulsar/Helpers/SlotListMutator.cs`),
  which owns the "move and renumber 1..N" semantics for both the settings list and
  the wheel preview; `MoveToInsertPosition` converts the GongSolutions insert index
  convention into a target index.
- `ISmartSubActionDefaults` (`SmartSubActionDefaults`) pre-populates new
  `com.pulsar.command` slots with a clipboard catalog (Cut/Copy/Paste/Select
  All/Undo) and `com.pulsar.system` slots with a system-tools catalog. Injection
  happens only in `CreateSlotDraft`, the single creation seam; editing an existing
  slot or later action changes never re-inject, so user-owned sub-action edits are
  never clobbered.

### Metadata, validation, and presentation

`InitializeSlotMetadata` resolves the plugin's action metadata (canonicalizing
`com.pulsar.system` actions, selecting the first declared action when the current
one is unknown or empty) and rebuilds the slot's `AvailableActions`,
`RequiredParameters`/`OptionalParameters`/`AdvancedParameters`, quick-edit
parameters, and summary tokens — the observable projections the editor UI binds to.
Validation summaries are derived from the config service's
`LastValidationResult` (populated by `ConfigValidationPipeline` during save) by
matching errors whose `PropertyName` contains `:{slot.Slot}]` for the slot's plugin
id; `SlotEditorWorkspace.RefreshSlotValidationSummaries` maps these onto per-slot
`ValidationSummary`/`ValidationSeverity` and refreshes the presentation.
`UpdateSlotPresentation` rebuilds the slot's parameter metadata and presentation
model (`SlotPresentationBuilder`) so the wheel orb, health badge, and summary stay
in sync.

## Slot authoring dialogs and cascade sub-actions

`SettingsViewModel` builds `SlotTypeCard`s from the plugin metadata registry
(`BuildSlotTypeCards`, used by `AddSlotDialog` and `OpenSlotConfiguration`):
primary intent cards (switch app, open target, send keys, fill secret, run script,
system) plus a browsable/searchable catalog of every plugin with at least one
action. Both flows create a `SlotEditorViewModel` with delegate wiring into the
workspace — `CreateSlotDraft`/`SetSlotDraftAction` for create mode, `SetSlotAction`
for edit mode, plus picker delegates (`PickSlotParameterValue`, `PickIcon`,
`PickColor`) and a secret-display resolver — and run it through
`SettingsDialogFlows` (the shared show → confirm → dispatch recipe, architecture
review candidate M).

`SlotEditorViewModel` (`Pulsar/Pulsar/ViewModels/Dialogs/SlotEditorViewModel.cs`)
is a wizard dialog view-model: a picker phase (search + curated cards) and a
configuration phase (action selector, grouped parameter fields, appearance, and the
Sub-Actions section). It enforces blocking validation — type selection, action
selection, and all required parameter values — and queues a focus request on the
first invalid field; `Save` materializes sub-actions and requests close with
`DialogResult.Confirmed`, and `CanCloseAsync(Confirmed)` blocks closing while a
blocking issue exists.

Sub-actions are edited as `SubSlotEditorRow`s (observable wrappers over the
immutable `SubSlotDescriptor` record) bound in the dialog content; rows share their
working `Args` dictionary with a backing `PluginSlot` so the parameter-field
machinery operates on the same dictionary the descriptor is materialized from.
`MaterializeSubActions` writes `Slot.SubActions` (null when empty) and persists
`CascadeLayoutStyle` only for a non-default Ring choice, keeping the optional keys
omitted for Fan so legacy profiles stay byte-compatible. `PluginSlot` serializes
`subActions` and `layoutStyle` with `JsonIgnoreCondition.WhenWritingNull`, and
deserialization tolerates absent keys (`PluginSlotSubActionsTests`).

## The Slots page and wheel editor

`SettingsSlotsPage` (`Pulsar/Pulsar/Views/Pages/SettingsSlotsPage.xaml.cs`) hosts a
header with the context switcher (`ComboBox` bound to `AvailableContexts` /
`CurrentContext`) and profile actions (Edit/Add/Delete), an empty state, and the
`SlotWheelEditor` control with a floating Add Slot FAB. It resolves the shared
`SlotWheelEditorViewModel` from the container, syncs it to the current context and
`SlotsPerPage` on load and on `CurrentSlots`/`CurrentContext`/`GeneralSettings`
changes, and re-hooks the general settings so `SlotsPerPage` edits call
`RefreshLayout` immediately. The wheel raises `EditRequested`,
`DeleteRequested`, and `AddSlotRequested`, which the page routes to
`OpenSlotConfiguration`, `RemoveSlot`, and `AddSlotDialogCommand` respectively.

`SlotWheelEditorViewModel` paginates the shared slot list (clamping `SlotsPerPage`
to 1–60), computes the ring layout through `ISlotLayoutEngine`
(`SlotLayoutEngine` centers at (250,250) with a base radius of 90 and slot-size
scaling), builds one `WheelSlotItem` per ring position with placeholders padding
the last page, and implements hit-testing (`TryResolveDropPosition` rejects the
dead zone and out-of-range drops), in-page reorder with renumbering, move-to-page
`MoveToPageAndSlot`, and a 2-second `Flash` highlight after `SlotAddedMessage`.
The `SlotWheelEditor` control implements the drag interactions: threshold-based
drag ghost, drop-target highlight, mouse-up reorder via the view-model hit test,
click-to-edit, hover buttons, and keyboard reorder (Ctrl+Left/Right), Enter to
edit, and Delete to remove. Clicking an empty placeholder slot raises
`AddSlotRequested`, which opens the create-slot dialog.

## General, theme, and hotkey surfaces

`SettingsViewModel.General` (partial class, `SettingsViewModel.General.cs`) binds
the General page: `CurrentTheme` writes `Settings.Theme` and immediately applies
the theme through `IThemeService` (dispatcher-safe), `RendererStyle` and
`ThemePreset` persist `Settings.RadialRenderer` and `Settings.RadialThemePreset`,
and `ShowGridHotkey`/`ShowSwitcherHotkey` write the hotkey config, apply the
hotkey live, and surface validation. `RendererOptions` enumerates the built-in
radial renderers (Default, ClassicRing, Glassmorphism) plus any plugin-contributed
renderers from `StyleRendererFactory` — re-enumerated on every settings open
because the view model is transient. `SlotsPerPagePreview` renders the angle
derived from the current `SlotsPerPage`. `SelectedLogLevel` applies the minimum
log level immediately through `ILoggingConfigService`. Cache statistics and
cleanup are delegated to `IProcessRegistryService`.

Theme reconciliation is bidirectional: `LoadSettings` applies the persisted theme
to the service if it differs, and the tray icon's theme toggle calls
`SyncThemeFromService` on the open window so an external theme change marks the
draft dirty rather than silently diverging.

## Notifications and external integration

The window registers the `SnackbarMessage` messenger channel and shows each
message in `MainSnackbarPresenter`; `SettingsViewModel.SendNotification` (and the
300 ms debounced variant used by move/reorder feedback) drives it. `SlotAddedMessage`
is sent by `CommitCreatedSlot` and consumed by both the wheel editor (jump + flash)
and the tutorial's `SlotAddedTriggerHandler`. `SlotsPerPageChangedMessage` is sent
after save so `RadialMenuViewModel` updates the live radial menu's page count
without a restart. `DialogService.FindBestOwner` prefers an open `SettingsWindow`
when placing dialogs. The tutorial system drives first-run steps through the
window's `TutorialMarker` ids (e.g. `AddSlotButton`, `SlotsNavigationItem`) and
reads the `NavigationView` via `ISettingsWindowAccessor`.

## Invariants and failure semantics

- **Dirty state is a single source of truth**: `SlotEditorWorkspace.HasUnsavedChanges`
  is the only flag; the guard, `CanSave`, the title-bar badge, and the close flow
  all read it. Loading, context switching, and draft creation suppress dirty;
  collection changes, slot property edits, secret staging, and general-settings
  edits set it.
- **All persistence goes through `ConfigEditSession`**: a stale-revision commit
  rebases untouched regions and retries once, so concurrent writers are never
  silently overwritten and repeated saves from the long-lived editor session
  succeed (the session re-arms its revision after each commit). The save pipeline
  additionally merges staged secrets into the secret store before committing.
- **Validation blocks invalid saves**: `ConfigService.SaveAsync` runs the
  `ConfigValidationPipeline` and throws when errors exist; the editor surfaces the
  first error and re-derives per-slot summaries from
  `LastValidationResult`. Unknown page ids, unknown settings routes, and
  navigation to pages with blocking validation issues are all rejected.
- **Window close and navigation are guarded**: the window cancels its first close
  and re-closes programmatically only after `CanCloseAsync` permits it; nav
  selection application is re-entrancy-guarded (`_isApplyingSelection`).
- **Legacy compatibility**: `subActions` and `layoutStyle` are omitted from JSON
  when absent, and the LegacySlotConverter keeps old slot shapes loadable, so
  Profiles.json stays byte-compatible for profiles without cascades.

## Configuration and operations

- `Profiles.json` lives at `%AppData%\Pulsar\Profiles.json` (overridable in
  tests via the `configPath` ctor); `ConfigService.ConfigFilePath` is the single
  source of truth. Every successful save writes a rolling `.bak` for recovery.
- `LocalUiPreferences.json` (same directory) stores `LastOpenedSettingsPageId`,
  which restores the last-visited settings page on the next open.
- `SlotsPerPage` is clamped to the production range 4–12 (`GetValidatedSlotsPerPage`)
  for runtime layout; the wheel preview clamps to 1–60 internally.

## Focused tests

- `SettingsPageCatalogTests` pins the Workbench-before-System navigation order,
  group membership, and the `Slots` default page.
- `SettingsSaveSessionTests` drives the real `ConfigService` (temp file):
  consecutive saves succeed (re-armed revision), and a save after an external
  writer commits preserves both the user's slot edits and the external change
  (rebased untouched regions).
- `SettingsViewModelDirtyStateTests` pins the dirty contract: load and context
  switching stay clean; commit, label edits, and action-option selection mark
  dirty; cancelled icon picking restores the original icon.
- `SettingsDialogFlowsTests` verifies the show → confirm → dispatch recipe runs
  the delegate only on confirmation, with or without size constraints.
- `SlotEditorWorkspaceTests` drives the workspace headless: context selection
  loads the right profile/mode lists, drafts never touch the current list, and
  move/reorder renumber 1..N.
- `DialogSlotEditorViewModelTests` covers picker/configuration phases and
  metadata-driven suggestions; `PluginSlotSubActionsTests` covers the
  `subActions`/`layoutStyle` round-trip and legacy-tolerance guarantees.
- `SlotWheelEditorViewModelTests` and `SlotWheelRingClippingTests` cover wheel
  pagination, placeholder padding, cross-page reorder targeting, and ring
  clipping behavior. `SlotEditorWorkspaceTests` also drives drag-drop reorder
  insert-position semantics through `SlotListMutator`.
