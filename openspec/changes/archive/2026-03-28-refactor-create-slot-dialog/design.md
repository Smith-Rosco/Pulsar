## Context

The current Create Slot dialog in `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml` is organized as a full-width top preview card followed by a two-column body. The lower body splits plugin-type selection on the left from slot configuration on the right. Within that structure, the UI currently exposes multiple always-visible explanatory text blocks, a prominent preview surface before the user has committed to a type, descriptive text for every plugin type option, and repeated bordered field treatments for parameter selection rows.

This structure is functional, but it asks the user to parse several equal-weight regions before they can make the first meaningful decision. That creates avoidable cognitive load in a flow that is inherently sequential: choose type, choose action, complete required setup, then optionally polish label/icon/color. The codebase already has slot-model support for action metadata, summary tokens, validation state, required/optional/advanced parameter groupings, and preview badges, so the main opportunity is not new behavior but re-prioritizing how existing information is presented.

The project already defines relevant OpenSpec requirements for dialog hierarchy, progressive disclosure, and layout density. This change should therefore reshape the Create Slot surface to better satisfy those requirements rather than introduce a new product capability.

## Goals / Non-Goals

**Goals:**
- Reduce cognitive load in Create Slot by making the primary authoring path visually dominant.
- Preserve the current underlying slot-authoring behavior, metadata contracts, picker flows, validation model, and preview data, while reordering and de-emphasizing how they appear.
- Make plugin-type selection remain scannable as the number of available plugin types grows.
- Move low-priority helper copy and appearance guidance into secondary disclosure without hiding critical validation or required-state information.
- Replace heavy repeated card treatments with typography, spacing, lightweight separators, and selective emphasis.

**Non-Goals:**
- Redesign the existing full slot configuration dialog (`SlotConfigurationDialogContent.xaml`) as part of this change.
- Change plugin metadata contracts, parameter metadata semantics, or picker behavior.
- Add search, categorization, or plugin marketplace mechanics unless the existing plugin count makes a minimal structural accommodation necessary during implementation.
- Introduce new theming systems or change the existing slot tone/token model.

## Decisions

### Decision: Keep the two-column body, but reduce the top preview to a lightweight workflow header

The user explicitly prefers the future-friendly two-column pattern because plugin count may continue to grow. The design will therefore retain the lower left/right division, but the full-width top region will stop behaving like a large equal-weight preview card.

Instead, the top area should become a lighter workflow header that primarily does three things: identify the task, surface any active validation summary, and provide a compact slot preview anchor. The preview remains useful because it reinforces the emerging slot identity, but it should not visually dominate the dialog before the user has selected a type.

Why this over removing the top region entirely:
- The current dialog already benefits from a stable identity/validation anchor at the top.
- Removing the top region completely would push validation and preview back into the body, creating more reflow and less consistency.
- A compact header can preserve structure while materially reducing visual competition.

Alternatives considered:
- **Remove preview entirely until a type is selected**: simpler, but loses useful live feedback and would introduce a larger layout jump after selection.
- **Keep the existing large preview card and only trim copy**: lower implementation risk, but does not adequately address the current over-weighted hierarchy.

### Decision: Plugin type selection becomes compact and scan-first, with descriptions demoted to tooltip-level help

The current left column renders each plugin type as a fairly tall bordered item with icon, title, and always-visible description. As plugin count grows, this consumes vertical space quickly and forces the user to read more prose than they need to choose a category.

The left column should instead optimize for fast scanning. The visible content for each plugin type should focus on icon, display name, and selected state. Descriptions should move to tooltip-level or comparable secondary disclosure.

Why this over preserving descriptions inline:
- Plugin choice is a recognition task first, and a reading task second.
- Inline descriptions repeat the same instructional burden on every row.
- The right column already becomes the place where the chosen type's deeper context can be explained if necessary.

Alternatives considered:
- **Icon-only grid**: scales well, but increases ambiguity and harms discoverability for less familiar plugin types.
- **Current list with smaller text**: low effort, but still leaves too much prose in the primary scan surface.

### Decision: The right column follows the creation flow strictly: action -> required details -> optional details -> appearance polish

The right column should reflect the actual slot-creation order. The current dialog already approximates this, but it still presents extra section prose and a label/appearance area that can compete too early with behavior setup.

The reworked order should be explicit in the visual rhythm:
1. Compact selected-type context and/or status anchor
2. Action selection
3. Required fields
4. Optional and advanced disclosure
5. Label and appearance polish as secondary finishing work

Label editing should remain accessible, but it should not steal priority from behavior completion. Appearance customization should remain disclosed by default.

Why this over moving label earlier:
- The ViewModel already suggests labels automatically based on plugin/action/parameters.
- For most users, label customization is a refinement step, not a prerequisite for correctness.

Alternatives considered:
- **Put label directly under type selection**: may feel friendly, but encourages premature polish before the slot works.
- **Hide label inside appearance disclosure**: cleaner, but too aggressive because users often do rename slots.

### Decision: Simple parameter rows should lose equivalent card treatment and use lighter structure

`CreateSlotParameterFieldTemplate` currently wraps each field value/picker row in a bordered, padded container. When several required fields appear together, the right column becomes a stack of visually equal mini-cards. That weakens hierarchy and adds visual noise.

The design should shift simple fields toward lighter row/group presentation using alignment, spacing, and separators, while still allowing complex picker states or long validation to expand naturally when necessary.

Why this over keeping boxed rows:
- The existing model already distinguishes simple vs complex fields and supports compact presentation goals elsewhere in the product.
- Removing repeated boxes makes the most important distinctions come from order, headings, and required state rather than container chrome.

Alternatives considered:
- **Keep the bordered treatment but reduce corner radius/background contrast**: an incremental improvement, but still leaves every field visually over-framed.
- **Make all fields fully row-based**: may be too aggressive for secret selectors or richer picker summaries.

### Decision: Only critical correctness signals remain persistently visible

The dialog should keep visible:
- validation summary when present
- required indicators
- missing/invalid state close to the active editing surface

The dialog should demote or hide by default:
- plugin descriptions in the picker list
- general instructional prose once the user has selected a type
- appearance guidance paragraphs
- low-priority helper/example text that already exists in parameter metadata

This follows the existing progressive disclosure requirement and aligns with the user's explicit goal of preserving a clean surface.

Alternatives considered:
- **Use tooltips for everything, including validation**: rejected because critical correctness information must remain visible.
- **Keep helper prose but shrink the text**: rejected because density alone does not solve attention competition.

## Risks / Trade-offs

- **[Risk] Over-compressing the dialog could make unfamiliar plugin types harder to understand** -> Mitigation: keep plugin names visible, preserve tooltips, and allow selected-type context to appear in the right column.
- **[Risk] Reducing field containers could make some complex pickers feel under-structured** -> Mitigation: keep the template flexible so complex fields can still use taller or richer layouts where needed.
- **[Risk] A lighter preview may reduce perceived confidence that the slot is taking shape** -> Mitigation: keep a compact persistent preview anchor with orb, title, type badge, and health state.
- **[Risk] Moving helper text into secondary disclosure may impact discoverability for first-time users** -> Mitigation: preserve clear help affordances and keep required-state plus corrective validation always visible.
- **[Risk] Future plugin growth may still strain the left column** -> Mitigation: adopt a denser scan-first item layout now and leave room for later search/grouping without rewriting the creation flow again.

## Migration Plan

This is a UI-only authoring-surface refactor with no data migration. Implementation should be done in-place in the Create Slot dialog by updating the XAML structure and only making ViewModel adjustments needed to support the new hierarchy. If the refactor proves visually problematic, rollback can be achieved by restoring the previous `AddSlotContent.xaml` and any minimal ViewModel property changes supporting the new presentation.

## Open Questions

- Whether the left column should remain a vertical compact list or evolve into a denser wrapped tile grid once implementation begins and real plugin counts are tested.
- Whether the compact preview anchor belongs in the header region or at the top of the right column after visual QA.
- Whether selected plugin context in the right column needs one persistent line of prose after type selection, or whether title/action hierarchy alone is sufficient.
