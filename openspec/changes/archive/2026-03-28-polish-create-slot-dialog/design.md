## Context

`Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml` was recently refactored to improve the Create Slot authoring flow, but the resulting surface still places too much weight in the top region. The left side of the header currently reserves prominent space for explanatory copy and a wide validation summary, while the preview remains substantial enough to compete with the actual work of choosing a slot type and filling required details.

At the same time, the plugin type picker in the same dialog uses a local text-based glyph rendering approach that differs from the shared slot icon rendering path used elsewhere in Pulsar. `SettingsViewModel.BuildAddSlotOptions()` currently supplies values such as `"E8A7"` and `"E756"`, while the picker card renders those values directly through a `TextBlock` with a Fluent/MDL2 font family. This bypasses `IconHelper.GetGlyph()` and the `SlotOrb` icon interpretation path, so hex icon keys are treated as literal text instead of converted into glyph characters. In practice, this produces the visible "four square boxes" defect in the Create Slot picker.

The dialog therefore needs a second-pass polish that addresses both information hierarchy and rendering consistency without changing slot creation semantics, plugin metadata contracts, or picker command behavior.

## Goals / Non-Goals

**Goals:**
- Reduce the visual prominence of the Create Slot header so it behaves as a lightweight workflow anchor rather than a competing content block.
- Keep a live preview visible, but shrink and reposition it so it supports confidence without dominating attention.
- Reorganize plugin type selection into a more uniform, scan-first layout that remains readable as plugin count grows.
- Route plugin type icon rendering through the same icon-key interpretation pipeline used by slot surfaces elsewhere in the application.
- Keep validation and required-state feedback visible, while demoting normal draft-state copy and non-critical helper prose.
- Preserve the existing behavioral flow of slot creation: select type, choose action, fill required details, optionally polish label/icon/color.

**Non-Goals:**
- Redefine plugin metadata storage or migrate existing icon keys in configuration data.
- Rework the full slot configuration dialog outside Create Slot unless a shared helper is needed for icon rendering.
- Introduce marketplace/search features for plugin types beyond lightweight grouping or category tabs needed to stabilize scanability.
- Change how pickers, parameter metadata, or action semantics function.

## Decisions

### Decision: Replace the heavy top header with a lightweight title/status bar plus compact preview anchor

The top region should stop acting like a two-column content band with explanatory prose and a substantial preview card. Instead, it should become a lightweight workflow bar that identifies the task and slot number, with compact status context and a smaller preview anchor.

The preview should remain visible because it helps users validate that the slot is taking shape, but it should be visually subordinate to the main editing surface. The preview content should focus on orb/icon, title, type badge, action summary, and one compact status line instead of a large multi-part information card.

Why this over removing the header preview entirely:
- A persistent preview anchor still helps orient the user while editing.
- Keeping it compact avoids the current problem of preview competing with the authoring flow.
- A stable compact anchor avoids abrupt reflow between pre-selection and post-selection states.

Alternatives considered:
- **Remove preview until a type is selected**: simpler, but creates a larger layout jump and loses early orientation.
- **Keep the current header and only trim text**: lower effort, but does not solve the hierarchy problem strongly enough.

### Decision: Move Create Slot into a scan-first selection-plus-configuration layout with categorized plugin type browsing

The plugin picker should stop behaving like a variable-height prose list. Instead, the left side of the dialog should become a scan-first selection surface with lightweight category tabs or segmented filters and consistent-size cards/tiles for plugin types.

Visible card content should be limited to icon, name, and selected state. Descriptions remain available through tooltip or selected-type context in the configuration column. The goal is not to introduce a full marketplace-style browser, but to normalize width, reduce vertical jitter, and make future plugin growth less disruptive.

Why this over the current vertical list:
- Consistent card sizing solves the current uneven picker widths and improves rhythm.
- Category tabs help the picker scale without requiring search as part of this change.
- Selection becomes a recognition task first, not a prose-reading task.

Alternatives considered:
- **Keep a vertical list and force fixed width rows**: helps width consistency, but still reads like a long form rather than a type browser.
- **Use icon-only tiles**: maximizes density, but weakens recognition for less familiar plugin types.

### Decision: Reuse the shared icon-key rendering path rather than maintaining a picker-specific glyph implementation

Create Slot plugin type icons should be rendered through the same interpretation rules as `SlotOrb` and `IconHelper.GetGlyph()`. That means the picker must treat icon values as `IconKey`-style semantic values that may represent a Fluent/MDL2 hex code, emoji, or file path, instead of assuming the value is already a renderable glyph character.

This can be achieved by either reusing `SlotOrb` directly for the picker icon surface or by introducing a lightweight shared presenter/converter that delegates to the same underlying icon interpretation logic. The important constraint is that Create Slot must no longer directly render raw hex strings in a `TextBlock`.

Why this over patching the current `TextBlock` locally:
- The codebase already has a correct icon interpretation path.
- Local conversion in one XAML surface would duplicate logic and invite future divergence.
- Shared rendering ensures consistent behavior for Fluent glyphs, emoji, and file-backed icons.

Alternatives considered:
- **Convert the known six icon strings to literal glyph characters in `SettingsViewModel`**: expedient, but leaks rendering concerns into view-model setup and does not solve the broader inconsistency.
- **Keep direct `TextBlock` rendering and add a local converter in AddSlotContent only**: better than current behavior, but still creates a second icon-rendering path.

### Decision: Demote validation summary from a broad banner to compact status integrated with the active workflow

The current wide validation block in the header consumes prime attention even during normal draft states. Validation should remain visible, but its presentation should reflect severity and editing context.

Critical error states may still deserve strong treatment, but routine incomplete-state guidance should move closer to the editing path, such as a compact status line near the preview anchor or a slim summary near required details. Field-level validation remains in place where correction happens.

Why this over hiding validation entirely until save:
- Users still need visible correctness signals while building a slot.
- The problem is prominence and placement, not the existence of validation.

Alternatives considered:
- **Keep the current banner and shrink its text**: still reserves too much space in the most visible area.
- **Hide all summary validation and rely only on field-level errors**: cleaner visually, but weaker for multi-field incomplete states.

### Decision: Preserve the current configuration order, but make the right column more explicit about primary vs secondary work

The right side already contains the core authoring flow, so the next iteration should clarify rather than reinvent it. The visible order should remain:
1. Selected type context
2. Action selection
3. Required details
4. Optional/advanced disclosure
5. Label and appearance polish

The main change is visual emphasis: selected-type context becomes compact, required details remain prominent, and appearance stays secondary by default.

Why this over a strict multi-step wizard:
- The dialog already has a productive single-surface editing model.
- A wizard would reduce simultaneous visibility and slow experienced users.
- The hierarchy problem can be solved without introducing hard step transitions.

Alternatives considered:
- **Convert the dialog to a two-step wizard**: clearer for first-time users, but more disruptive and heavier than needed.
- **Move label editing earlier**: encourages premature polish before behavior works.

## Risks / Trade-offs

- **[Risk] Category tabs could feel unnecessary with only a handful of plugin types** -> Mitigation: keep the category model lightweight and allow an `All` view so the UI remains simple even at small scale.
- **[Risk] Shrinking the preview too far may reduce user confidence** -> Mitigation: preserve the key identity signals in the compact preview and keep it persistently visible.
- **[Risk] Reusing `SlotOrb` directly in picker cards may visually overstate the icon area or introduce control-specific styling side effects** -> Mitigation: allow a lightweight shared presenter if direct reuse feels too heavy, but keep one icon interpretation path.
- **[Risk] Moving validation away from the header could make errors easier to miss** -> Mitigation: keep severe states visually distinct and ensure field-level validation remains immediate near the affected controls.
- **[Risk] A second-pass refactor may overlap conceptually with the previous `refactor-create-slot-dialog` change** -> Mitigation: frame this change as corrective polish and rendering consistency, not a replacement of the prior flow model.

## Migration Plan

This is a UI and presentation-layer refinement with no data migration. Implementation should update the Create Slot XAML structure, add any minimal ViewModel/category support required for the new picker organization, and consolidate icon rendering through shared helper logic. Rollback remains straightforward: restore the prior Create Slot XAML layout and any presentation-only ViewModel changes if visual QA or usability feedback rejects the new hierarchy.

## Open Questions

- Whether the compact preview belongs in the top-right of the main layout or at the top of the configuration column after visual testing.
- Whether plugin type grouping should be hard-coded for the current six plugin types or inferred from plugin metadata in a later follow-up.
- Whether the shared icon rendering should be expressed as direct `SlotOrb` reuse or as a smaller shared presenter built on the same icon helper logic.
