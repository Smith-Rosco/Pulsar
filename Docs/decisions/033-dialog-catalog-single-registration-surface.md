# ADR-033: Dialog Catalog — One Registration Surface for Every Dialog

**Status**: Accepted
**Date**: 2026-09-11
**Deciders**: milo (owner), WorkBuddy agent (implementation)
**Related**: [029-settings-transient-pages.md](./029-settings-transient-pages.md), [023-page-provider-factory-seam.md](./023-page-provider-factory-seam.md), [027-documentation-structure-v6-single-authoritative-sources.md](./027-documentation-structure-v6-single-authoritative-sources.md)

---

## Context

A modal dialog in Pulsar was assembled from five independent choices, each made
at the call site:

1. the content view model (`ViewModels/Dialogs/*`),
2. its content control (`Views/Dialogs/Contents/*.xaml`),
3. the implicit `DataTemplate` in `Themes/DialogTemplates.xaml`,
4. the `DialogSizeConstraints` preset,
5. the title resource key in `Strings.resx` / `Strings.zh-CN.resx`.

Only leg 1↔3 was guarded (`DialogTemplateRegistrationTests`). Legs 4 and 5 had
**no guard at all**, and no artifact tied a title key to a content type. The
architecture review of 2026-09-11 measured the result:

| Finding | Evidence |
|---|---|
| `ShowCustomAsync` call sites | **20** — 18 direct across 9 files plus 2 inside `SettingsDialogFlows` |
| Distinct content types | 15 (plus `sys:String` for message bodies) |
| Call sites choosing a size preset inline, not by name | **4** — `WindowInspector` 780×560, `ExampleLibrary` 620×480, `ScriptEditor` 720×560, `PluginSettings` 550×500 |
| Title keys reaching the caller through `string.Format` | **4**, each wrapped in a hand-written `?? "English fallback"` |
| Duplicated registration of `DialogTemplates.xaml` | 2 (`App.xaml:22`, `DialogHostWindow.xaml:28`) |
| A single dialog fix that touched 6 files / 3 layers | `d85b17c` |

Two structural facts killed the obvious design ("key the catalog by view-model
type"):

- **A view-model type does not determine a dialog.** `InputDialogViewModel` was
  shown under three different titles with two different sizes
  (`SecretPicker.AddSecret` / `SecretPicker.EditSecret`, both Medium;
  `About.BackupPasswordTitle`, Small). Conversely `IconPickerViewModel` was shown
  by three call sites that *did* agree — and `ProcessPickerViewModel` by two that
  did **not** (one Medium, one `LargeResizable`). Type → (title, size) is not a
  function in either direction.
- **A dialog id must be a distinct type, not a `string`.** Adding
  `ShowCustomAsync(string dialogId, …)` next to the existing
  `ShowCustomAsync(string title, TViewModel content, DialogButtons buttons = …)`
  would make a two-argument call bind to the *old* overload (normal form beats
  expanded form), silently treating the id as a literal title. A distinct
  `DialogId` type removes the ambiguity and makes the swap a compile error.

The first recon pass also mis-read the process-picker size as Medium, because the
grep output truncated a multi-line call whose tail
(`}, DialogButtons.OkCancel, DialogSizeConstraints.LargeResizable);`) sat three
lines below the anchor. The compiler caught it: the two process-picker rows are
identical and collapsed into **one** id. Logged here because it is the failure
mode a "key by type" catalog would have hidden permanently.

## Decision

**Introduce a dialog catalog: one row per dialog owns its title key, content
type, size preset, button set and theme override. Call sites reference a
`DialogId` constant instead of choosing any of them.**

Five parts:

1. **`DialogId` is a distinct value type**, not a `string`
   (`Services/DialogCatalog.cs`). The `string`-keyed overloads stay for dynamic
   titles and one-off sizes; the `DialogId`-keyed overload is a pure addition, so
   no existing call changes meaning and no overload is ambiguous.

2. **`DialogCatalog` is a static registry of 19 rows** covering 15 content types.
   It is static by design — the set is compile-time data with no runtime
   registration, in contrast with `SettingsPageCatalog`, which must accept
   transient pages. The 4 inline size presets moved into the rows **verbatim**;
   nothing was forced onto a named preset.

3. **The catalog also records the content type**, which is what makes leg 3
   guardable in both directions (see 5). It is not consulted at runtime — the
   existing `HasTemplate` fail-fast already checks the concrete instance.

4. **`IDialogService` gains one overload**:
   `ShowCustomAsync<TViewModel>(DialogId, TViewModel, params object[] titleArgs)`.
   Title resolution is driven by the row's explicit `TitleIsFormat` flag rather
   than by "were arguments supplied", so a row that expects arguments but
   receives none fails loudly instead of rendering a literal `{0}`.
   `SettingsDialogFlows.RunAsync` now takes a `DialogId` and no longer threads
   `buttons` / `sizeConstraints` through its five callers.

5. **The guard covers all three legs.** `DialogCatalogTests` pins, for every row:
   the title key exists in **both** resx; the `TitleIsFormat` flag matches the
   placeholder actually present in the resx **value** (both cultures); the
   content type has a `DataTemplate`; and — the reverse direction — every dialog
   `DataTemplate` either has a row or is on a documented exemption list
   (`sys:String` for message bodies, `ColorPickerViewModel` /
   `InputDialogViewModel`, which are shown by dedicated title-owning APIs).

### What did not change

- **The message / confirmation / input / colour APIs.** They own their titles by
  design (a message body is not a registered view model) and carry no row.
- **`DialogTemplates.xaml` and its double merge.** `App.xaml:22` feeds
  `DialogService.HasTemplate`'s fail-fast; `DialogHostWindow.xaml:28` feeds the
  visual tree. Same file, so there is no divergence to fix — a comment records
  the division of labour instead.
- **Every dialog's runtime appearance.** Title text, size, buttons and theme are
  byte-for-byte what they were; the 4 inline presets moved unchanged.
- **`SlotEditorViewModel`'s retired modal template** (ADR-029) stays retired.

### Deliberate behaviour preservation (discovered defect, not fixed)

`Dialog.PluginAnalyticsDetail.Title` resolves to `"Plugin Details"` /
`"插件详情"` — **no `{0}` placeholder** — while the call site ran
`string.Format(…, item.DisplayName)`. The plugin name was therefore discarded by
`string.Format` on every invocation. This ADR **preserves** that behaviour: the
row carries `TitleIsFormat = false` and the dead argument was removed with a
comment. Fixing it changes user-visible text and belongs to its own change; the
new guard is what makes the drift visible (a translator adding `{0}` now fails
the test until the flag is flipped and an argument supplied).

## Consequences

**Positive**
- **The reference-discipline problem is fixed permanently.** Twenty call sites
  stop choosing titles and sizes independently; a new dialog cannot be shown
  without a row, and the reverse-direction guard fails if a template is added
  without one.
- **Two previously unguarded legs are now guarded**, and the guarded leg became
  bidirectional. The failure modes this removes were all silent: a missing resx
  key rendered the key, a missing template rendered the type name, and a
  placeholder/flag mismatch dropped an argument from the title.
- The process-picker duplication was found and collapsed during the migration —
  a defect the "key by type" design would have preserved.
- `SettingsDialogFlows` shrank: it no longer threads `DialogButtons` and
  `DialogSizeConstraints` through every caller.

**Negative / accepted**
- **A second way to show a dialog now exists.** The `string`-keyed overloads are
  kept, so a future call site could bypass the catalog and re-introduce the drift
  one dialog at a time. The mitigation is the reverse-direction guard plus the
  documented rule ("every dialog with a row must be shown through it"); a
  code-review rule cannot be machine-enforced without deleting the dynamic
  overloads, which are still needed.
- **One more file to touch when adding a dialog** is offset by two fewer
  decisions per call site, but the total surface is not obviously smaller for the
  first dialog after this change.
- `DialogRegistration.SizeConstraints` hands out a shared instance rather than
  the fresh preset each call site used to get from the `static` property.
  `DialogService.ApplySizeConstraints` only reads it, so there is no behavioural
  difference; the record documents the reference as a read-only template.

## Verification

All evidence below is **【已验证】**.

| Check | Result |
|---|---|
| `dev.ps1 build` | **0 errors, 0 warnings** |
| `dev.ps1 test` | **1613/1613 passed, 0 failed, 0 skipped** (baseline 1606 + 8 new catalog tests − 1 net in `SettingsDialogFlowsTests`) |
| Live call sites still choosing a title key | **20 → 0** |
| Live call sites still choosing a size preset / button set | **20 → 0** |
| `DialogSizeConstraints` presets inlined at call sites | **4 → 0** (moved verbatim into rows) |
| Duplicate-id / missing-resx / missing-template / flag-mismatch | all four fail the new guards if reintroduced (guards exercised by construction on every run) |

Test changes:

- **New** `Pulsar.Tests/Dialogs/DialogCatalogTests.cs` (8 cases): catalog
  non-empty; ids unique and non-blank; every row well-formed (title key, size
  preset, content type implements `IDialogViewModel`); lookup resolves every row
  and rejects an unknown id with the id in the message; title keys exist in both
  resx; `TitleIsFormat` matches the resx value in both cultures; every content
  type has a template; every template has a row or an exemption.
- **Rewritten** `SettingsDialogFlowsTests` — the two "which overload was used"
  cases were replaced by one that asserts the id is handed through unchanged and
  that the recipe never chooses a title, size or button set of its own.
- **Re-pointed** `SettingsAnalyticsPageViewModelTests`,
  `SecretPickerSeamTests`, `SettingsViewModelDirtyStateTests` to the new
  overload; the analytics verifications now assert the concrete dialog id instead
  of `It.IsAny<string>()` for the title.

**Test blind spot (acknowledged)**: the catalog is compile-time data, so its rows
are covered by static guards rather than by exercising each dialog. Two
consequences follow. First, a row whose size preset is *wrong for its content*
(e.g. too small for the body) passes every test — only real-hardware inspection
shows that, and this change is behaviour-preserving, so no dialog was re-sized.
Second, the runtime path of the new overload (`ResolveTitle` → `ShowCustomAsync`)
is not directly unit-tested: it needs a WPF `Application` and a dispatcher, so
only the halves are guarded — the resx values statically, and `HasTemplate` by
`DialogTemplateRegistrationTests`.

**Strongest counter-case**: the guards pin the *catalog*, not the *callers*. A
call site that keeps using the `string`-keyed overload still compiles, still
passes, and still picks its own title and size — the reverse-direction guard only
notices when a *new dialog template* appears without a row. If a developer adds a
row and then shows the dialog through the old overload, nothing fails. Closing
this needs either a "no `ShowCustomAsync(string, …)` outside `DialogService`"
analyzer rule or deletion of the dynamic overloads — both out of scope here,
since dynamic titles are a legitimate need.

## Change History

- `v1.0.0` (2026-09-11): dialog catalog introduced — `DialogId` /
  `DialogIds` / `DialogRegistration` / `DialogCatalog` added; 20 call sites
  migrated to the `DialogId` overload of `ShowCustomAsync`; `SettingsDialogFlows`
  takes a `DialogId`; three-leg guard added and the template leg made
  bidirectional; the process-picker duplicate id found and collapsed; the dead
  title argument of `PluginAnalyticsDetails` preserved and documented.
