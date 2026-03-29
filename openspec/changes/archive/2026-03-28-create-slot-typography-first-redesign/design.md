## Context

Pulsar already has a fairly capable slot creation and slot editing system, but the current dialog surfaces over-rely on stacked `Border` containers, repeated section cards, and wizard framing. In `AddSlotContent.xaml`, the Create Slot flow is split into two steps even though the user task is conceptually one continuous editing pass: choose a slot type, satisfy the required behavior fields, optionally refine appearance, and save. The result is a dialog that feels heavier than the underlying task.

The requested direction is explicitly Apple-inspired, but within the existing WPF / Wpf.Ui design system and Pulsar theme constraints. That means the redesign should not imitate macOS chrome literally; instead it should adopt the more durable principles behind that style: typography-led hierarchy, strong alignment, consistent spacing rhythm, fewer surfaces, calmer information density, and progressive disclosure for low-priority controls.

This change is presentation-focused. It must preserve all current slot creation semantics, parameter metadata behavior, plugin contracts, and dialog infrastructure.

## Goals / Non-Goals

**Goals:**
- Simplify Create Slot into a calmer editing surface that reads as one workflow instead of a stack of panels
- Shift hierarchy from borders and cards to text scale, spacing, and alignment
- Make required behavioral setup the clear primary task, with appearance customization as secondary disclosure
- Align Create Slot and Slot Configuration so both dialogs communicate the same information architecture principles
- Reuse existing ViewModel and parameter metadata patterns where possible, only simplifying presentation state when needed

**Non-Goals:**
- Do not change plugin execution, slot persistence, validation rules, or metadata generation
- Do not introduce a new UI framework, external dependency, or custom rendering system
- Do not redesign unrelated settings pages or slot list card interactions in this change
- Do not require exact macOS visual mimicry; the goal is Apple-like information design, not platform cloning

## Decisions

### D1: Replace step-based framing with a single progressive surface

**Decision:** Remove the conceptual prominence of the current two-step wizard in `AddSlotViewModel` and `AddSlotContent.xaml`, replacing it with one scrollable editing surface that reveals content progressively based on slot selection and available parameters.

**Rationale:** The existing step split does not represent two distinct user jobs. The first step already contains the essential creation work, while the second mostly exposes label, icon, and color polish. Forcing a step transition adds navigation overhead and duplicates hierarchy. A single surface better matches the actual mental model: configure the slot, then optionally refine it.

**Alternatives considered:**
- Keep the wizard and restyle it: reduces some visual noise, but still preserves unnecessary task fragmentation
- Expand to a three-step flow: increases ceremony and moves further away from the desired lightweight editor feel

### D2: Use one strong header anchor and reduce repeated section surfaces

**Decision:** Consolidate the dialog header and preview into one primary anchor area near the top, then render most subsequent sections with minimal or no full-card framing unless a background surface is needed for emphasis or state.

**Rationale:** The current layout repeatedly wraps content in bordered blocks, making every region compete for attention. A single strong anchor gives users orientation, while downstream sections can rely on typography, whitespace, and consistent left edges to express order.

**Alternatives considered:**
- Preserve separate header and preview cards: easier incremental change, but keeps duplicated emphasis
- Remove preview entirely: reduces redundancy, but loses a useful live artifact showing the slot outcome

### D3: Convert parameter items from self-contained cards into editor rows

**Decision:** Rework the parameter field template so each field behaves like a form row or compact editing block, not a fully boxed mini-card. Help affordances and validation hints remain, but borders become optional and exceptional rather than the default for every field.

**Rationale:** Parameter rows are currently one of the largest contributors to noise because each field creates its own surface. For typography-first design, rows should share one reading plane and be separated primarily by spacing, labels, and secondary text. This keeps the editor visually calm while preserving clarity.

**Alternatives considered:**
- Keep field cards but make them thinner: still reads as repeated component stacking
- Remove all visual grouping: risks ambiguity for complex picker-based fields, so selective highlighting remains allowed

### D4: Demote appearance and summary to secondary disclosure

**Decision:** Keep label editing visible, but move lower-priority appearance controls such as icon browsing and color customization into a lighter disclosure treatment. Summary tokens should become supporting context integrated near preview or footer helper text instead of occupying a full standalone section.

**Rationale:** The main task is making the slot work. Appearance customization is valuable but secondary. Treating it with equal structural weight pulls attention away from required actions and fields. Progressive disclosure preserves capability without forcing it into the primary reading path.

**Alternatives considered:**
- Keep Appearance as a full section at parity with behavior: reinforces the current over-weighted layout
- Hide all appearance controls until after creation: too restrictive for users who intentionally customize before saving

### D5: Define a stable typography and spacing system for slot dialogs

**Decision:** Establish a narrow type scale and spacing rhythm for Create Slot and Slot Configuration, with section titles, primary body text, secondary body text, and compact metadata each having consistent roles.

**Rationale:** The current dialog mixes many nearby font sizes, which creates fragmentation instead of hierarchy. A tighter scale combined with consistent vertical spacing gives the interface the calmer, Apple-like editorial quality the user wants.

**Alternatives considered:**
- Continue using ad hoc local font sizing: fast for isolated edits, but perpetuates inconsistency
- Move immediately to app-wide design tokens: worthwhile later, but broader than this change

### D6: Keep validation visible while hiding low-priority guidance

**Decision:** Preserve visible validation summaries and required-state markers in the active editing path, while moving descriptions, examples, and advanced helper copy into lighter or collapsible disclosure patterns where possible.

**Rationale:** The redesign should reduce noise without sacrificing correctness. Users must still see incomplete required fields and corrective feedback without extra interaction.

**Alternatives considered:**
- Collapse all help and validation together: lowers noise but risks hidden failure states
- Keep all instructional text permanently visible: preserves clarity but undermines density goals

## Risks / Trade-offs

- [Risk] A less boxed layout may initially feel less explicit to users accustomed to card-based grouping -> Mitigation: preserve strong section titles, alignment, and one clear top preview anchor
- [Risk] Removing wizard framing may require modest ViewModel simplification and dialog copy changes -> Mitigation: keep the underlying slot creation lifecycle unchanged and only reduce presentation-specific step state where safe
- [Risk] Parameter rows without default borders may become visually ambiguous for complex picker fields -> Mitigation: allow selective background treatment for high-complexity rows while keeping simple rows border-light
- [Risk] Create Slot and Edit Slot alignment could drift if only one dialog is fully refactored -> Mitigation: include corresponding hierarchy cleanup in `SlotConfigurationDialogContent.xaml` during the same implementation pass
- [Risk] Existing localized copy or hard-coded size values may still create uneven rhythm after layout changes -> Mitigation: standardize the slot-dialog typographic scale and spacing during the refactor rather than only moving controls around

## Migration Plan

1. Update the OpenSpec delta specs for information hierarchy, density/layout, and progressive disclosure
2. Refactor `AddSlotViewModel` presentation state to support a single-surface flow while preserving slot draft creation behavior
3. Rebuild `AddSlotContent.xaml` around the new hierarchy, parameter rows, and secondary appearance disclosure
4. Refactor `SlotConfigurationDialogContent.xaml` to share the same structural language where appropriate
5. Run `dotnet build Pulsar/Pulsar/Pulsar.csproj` and validate dialog behavior manually

Rollback is low risk because the change is limited to dialog XAML and presentation-oriented ViewModel properties.

## Open Questions

- Should the single-surface Create Slot dialog fully remove wizard chrome from the footer, or keep button text behavior (`Continue` vs `Save Slot`) while flattening the visible structure? Recommended default: remove wizard semantics entirely and use a direct create/save action.
- Should summary tokens appear under the preview header or in footer helper text? Recommended default: place them near the preview so they read as live slot metadata rather than a separate summary module.
