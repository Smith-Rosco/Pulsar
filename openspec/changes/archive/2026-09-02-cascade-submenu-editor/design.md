## Context

See proposal.md — Why. Current state that shapes the approach:

- Runtime cascade is fully wired: `CascadeSubMenuStrategy` (id `cascade`, `CascadeSubMenuStrategy.cs:21`) renders children from `CascadeSubMenuDescriptor.SubSlots` using `PluginActionStrategy` + `SubMenuLayoutEngine`; coordinator routes by `StrategyId`; `MenuSession.HitTest` dispatches cascade hit-tests. Change B archived, tests green (37 new).
- **No entry path**: `MenuSession.HandleGlobalMouseClickAsync` (`MenuSession.cs:889`) only branches on `ProcessGroupStrategy` + `List<ProcessWindowInfo>`; `CascadeSubMenuDescriptor` is never constructed outside tests.
- **No editor**: `SlotEditorViewModel` (`SlotEditorViewModel.cs:221`) has no `SubActions` surface; `SlotConfigurationDialogContent.xaml` (418 lines) has Behavior/Appearance/Advanced sections but no sub-action block. `PluginSlot.SubActions` (`ProfilesConfig.cs:635`) exists and persists; `CommandPageProvider` already maps `SubActions`→`SlotViewModel.SubSlots` (`CommandPageProvider.cs:78`).
- `SubSlotDescriptor` is a **positional record** (`Models/SubSlotDescriptor.cs:11`) — immutable, so in-place property binding is impossible; edits must go through a wrapper or `with`-replacement.
- `PluginSlot` has **no layout-style field** — `CascadeSubMenuDescriptor` takes `LayoutStyle` at construction (default `Fan`); the user's choice needs persistence on the slot.
- Editor commands use the `ItemsControl.Tag` bridge pattern (see `ParameterItemsControl_Loaded` in `SlotConfigurationDialogContent.xaml.cs:19`) because UserControls break `RelativeSource` bindings; new sub-action rows must follow it.
- Slot creation funnels through `SlotEditorWorkspace.CreateSlotDraft` (`SlotEditorWorkspace.cs:381`) — the single injection point for smart defaults; Edit mode reuses `existingSlot` and must NOT inject.

## Goals / Non-Goals

**Goals:**
- Make a configured cascade reachable: left-click on a slot with `SubSlots` opens the cascade; modifier-release and window-group drill-in semantics preserved.
- Author sub-actions in the existing unified editor (Behavior section), persisted via `PluginSlot.SubActions`, with per-sub-action plugin/action/args/label/icon/color and Fan/Ring layout picker.
- Inject sensible default sub-actions at creation time for known types, overridable in the editor.

**Non-Goals:**
- Cascade-of-cascade nesting (sub-actions with their own sub-actions) — future.
- Sub-ring theming/visual polish — future.
- Changing window-group or modifier-release behavior.
- Changing `IMenuSession`/`ISubMenuStrategy` contracts (already generalized by Change A/B).

## Decisions

### D1 — Editable row wrapper over the immutable `SubSlotDescriptor`
`SubSlotDescriptor` is a positional record; bind the editor to a new `SubSlotEditorRow` (observable wrapper: `PluginId/Action/Args/Label/IconKey/ColorHex` + metadata-derived action label), and materialize `List<SubSlotDescriptor>` on save. Prevents record-replacement churn in `ObservableCollection` and keeps `SubSlotDescriptor` unchanged.

- Alternative: use `with` expressions + replace collection items — rejected, breaks focus and `ObservableCollection` diffing during typing.

### D2 — Sub-action section reuses the parameter-field surface
Each `SubSlotEditorRow` exposes `SlotParameterEditorField` collections resolved via `_metadataRegistry.GetActionMetadata(row.PluginId, row.Action)` — the same machinery as root fields — rendered with the existing `SlotParameterFieldTemplate` and the `ItemsControl.Tag` command-bridge. A plugin/action selector per row drives metadata re-resolution (mirrors `SlotEditorViewModel.SetAction`).

- Rationale: identical UX to root parameters with near-zero new field editing code; picker intents (process/file/secret) work for free.
- Risk: sub-action args live in `SubSlotDescriptor.Args` — a plain `Dictionary<string,string>`, so `SlotParameterEditorField.Value` bindings need a mapping layer (see D4).

### D3 — Persist layout style on `PluginSlot`
Add `CascadeLayoutStyle` (`SubMenuLayoutStyle?`, null-tolerant, camelCase `layoutStyle`) to `PluginSlot`. The cascade entry and `CommandPageProvider` read it to build `CascadeSubMenuDescriptor` with the user's choice; `null` → `Fan` (descriptor default). No migration (optional field).

- Alternative: store style inside each `SubSlotDescriptor` — rejected, it's a per-cascade property, not per-child.

### D4 — Args mapping via the row, not the record
`SlotParameterEditorField` writes back to a `Dictionary<string,string>`; `SubSlotEditorRow` owns a working `Dictionary<string,string> Args` that mirrors `SubSlotDescriptor.Args`, with copy-in on load and copy-out on save. Keeps the immutable record untouched during typing.

### D5 — Cascade entry is a coordinator-seam branch in the click handler
In `HandleGlobalMouseClickAsync`, before the root-slot execution fallthrough: if the slot is a window group → existing `WindowSubMenuDescriptor` branch (unchanged); else if `slot.SubSlots.Count > 0` → build `CascadeSubMenuDescriptor(slot.SubSlots, slot.CascadeLayoutStyle ?? Fan, slot.Label)` and `EnterSubMenuAsync`. Modifier-release path untouched (it never calls the click handler). Empty `SubSlots` → normal action execution.

- Rationale: keeps entry logic in the session (which owns drill-in decisions) and relies on Change A/B routing; no strategy changes needed.
- Priority: window-group branch first so grouped slots keep their existing behavior even if they ever carry `SubSlots`.

### D6 — Smart defaults injected at creation via a catalog
New `SmartSubActionDefaults` service exposing `IReadOnlyList<SubSlotDescriptor>? ForPlugin(string pluginId, string action)`; catalog keyed by canonical plugin/action pairs (e.g., clipboard/send-keys and system-tools command types). `SlotEditorWorkspace.CreateSlotDraft` calls it after `InitializeSlotMetadata` and assigns `slot.SubActions` only for new drafts (Edit mode constructs via `existingSlot` and never re-injects).

- Localization note: default labels are stored as authored display text (already localized via plugin label conventions in the runtime renderer); no new resx needed for catalog content.
- Risk: defaults are static text, may not match user's locale — acceptable for v1; the editor is fully overridable (spec: `cascade-smart-defaults`).

### D7 — DI registration + localization
Register `SmartSubActionDefaults` (and the editor's new pickers via existing delegates) in `App.xaml.cs`. Add `Dialog.AddSlot.SubActions.*` and `Dialog.AddSlot.LayoutStyle.*` keys to `Strings.resx` (EN) + `Strings.zh-CN.resx` (ZH); follow `Category.SubCategory.Description` naming.

## Risks / Trade-offs

- [Immutable `SubSlotDescriptor` complicates typing] → D1/D4 wrapper owns working state; materialize once on save.
- [Cascade entry could conflict with grouped slots carrying sub-actions] → window-group branch takes priority (D5); documented, no mixed case today.
- [Sub-action arg fields need metadata; unknown plugin/action] → row degrades to `not-enabled` (existing `CascadeSubMenuStrategy` behavior) + editor shows inline validation instead of crashing.
- [Defaults inject English-ish labels] → overridable; catalog is the single place to localize later.
- [Editor growth in an already-dense dialog] → D2 reuses existing field template; new section is compact-empty by default (spec `unified-slot-editor-layout`).

## Migration Plan

1. Add `PluginSlot.CascadeLayoutStyle` (optional) + row/model plumbing (`SubSlotEditorRow`); no data migration.
2. Add sub-action editor section to `SlotConfigurationDialogContent.xaml` + `SlotEditorViewModel` (CRUD, layout picker) behind the Behavior section; reuse field template + Tag bridge.
3. Wire cascade entry branch in `MenuSession.HandleGlobalMouseClickAsync`.
4. Add `SmartSubActionDefaults` + inject in `SlotEditorWorkspace.CreateSlotDraft`.
5. Localization keys (EN/ZH).
6. Tests (editor CRUD, persistence, entry, defaults, window/group regression) → full suite green.
7. Rollback: removing the entry branch and editor section reverts to Change A/B runtime behavior (window path never changes).

## Open Questions

- None blocking. Default catalog contents (which plugin/action pairs get which children) can be tuned without touching specs — it is authored data behind `SmartSubActionDefaults.ForPlugin`.
