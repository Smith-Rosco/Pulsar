## Context

The recent layered slot editor work established the right product split between list-level quick edit and dialog-level deep configuration, but the resulting presentation still feels over-explained and vertically inflated. On the slots page, collapsed cards still spend space on instructional prose instead of pure state, while expanded cards still read like a stacked mini settings page with multiple headings and mixed concerns. In the full configuration dialog, long-form descriptions, examples, and hints compete with the actual editing controls, making the page feel more like documentation than a focused editor.

This change is primarily an information architecture refinement across three connected surfaces:
- the collapsed slot card on `SettingsSlotsPage.xaml`
- the expanded quick-edit content inside the slot card
- the full configuration dialog in `SlotConfigurationDialogContent.xaml`

The design must preserve the metadata-driven parameter system and the existing layered editing model while making each layer visually and cognitively distinct. It also needs to respect current WPF constraints around shared controls, resource injection, and dialog composition already documented in the repository.

## Goals / Non-Goals

**Goals:**
- Make collapsed slot cards feel like scan rows rather than explanatory cards.
- Make expanded quick edit feel like a compact control surface for common edits rather than a small documentation page.
- Make the full configuration dialog feel like a focused inspector/editor with a clear structure for basic settings, parameters, and advanced options.
- Replace always-visible low-priority guidance with progressive disclosure patterns such as tooltips or info affordances where appropriate.
- Establish explicit rules for what information must remain always visible versus what can be summarized or disclosed on demand.

**Non-Goals:**
- Changing slot runtime behavior, plugin execution semantics, or stored slot argument schemas.
- Replacing the metadata-driven editor with plugin-specific custom UIs.
- Redesigning unrelated settings pages.
- Introducing a new side panel, multistep wizard, or detached configuration window.
- Hiding critical warnings, required-state indicators, or validation status inside tooltips.

## Decisions

### 1. Collapsed slot cards become pure scan surfaces

Decision:
- The collapsed slot card SHALL show only slot identity and compact status metadata, and SHALL NOT include instructional or explanatory prose.

Rationale:
- Users scan the slots list to answer "what is this slot" and "is it healthy" before they decide to interact.
- Explanatory phrases such as "open full configuration" consume height without adding state information.
- Removing prose creates a more stable, row-like rhythm across many slots.

Alternatives considered:
- Keep a short helper sentence in the collapsed state: rejected because even compact prose adds noise and duplicates interaction affordances already conveyed by expansion and action buttons.
- Replace prose with summary tokens in all cases: rejected because summary tokens can still overgrow the collapsed layer for parameter-heavy slots.

### 2. Expanded quick edit uses task-oriented compact rows instead of stacked sections

Decision:
- The expanded inline layer SHALL use a compact row-based field layout for high-frequency edits and SHALL minimize visible section headings.

Rationale:
- The current stacked layout creates unnecessary visual hierarchy and page-like weight inside a card.
- Common quick edits such as label, action, icon, color, and one or two key parameters fit naturally into a denser label/value arrangement.
- Reducing visible titles lets the user focus on controls rather than on deciding which heading to read first.

Alternatives considered:
- Keep the existing stacked sections but reduce margins: rejected because spacing is only part of the problem; the stronger issue is competing hierarchy.
- Flatten all controls into a single unlabeled list: rejected because users still need light structure and predictable alignment.

### 3. Summary content belongs to scan surfaces, not active edit surfaces

Decision:
- Summary-oriented content such as status tokens SHALL appear only where users are scanning state, and SHALL NOT compete with editing controls inside quick-edit layouts.

Rationale:
- Once a card is expanded, the user has already entered an editing task and no longer benefits from a separate "summary of the summary" block.
- Removing summary blocks from expanded content reduces vertical height and clarifies intent.

Alternatives considered:
- Keep summary tokens in expanded quick edit for reassurance: rejected because they duplicate information already available through the editable fields and status indicators.

### 4. Full configuration dialog adopts an inspector-style hierarchy

Decision:
- The full configuration dialog SHALL use a restrained inspector-style hierarchy with a clear top identity block, visible validation state, and a small number of purposeful groups such as Basic, Parameters, and Advanced.

Rationale:
- A deep editing surface benefits from explicit grouping, but too many headings make it feel fragmented.
- An inspector model gives users a stable mental model: identify the slot, review any issues, then edit grouped fields.
- This model aligns with existing dialog usage in the repo while improving comprehension.

Alternatives considered:
- Preserve the current required/optional/advanced grouping exactly as the primary visual structure: rejected because it reflects metadata internals more than user tasks.
- Remove all grouping in the dialog too: rejected because deep configuration needs stronger organization than quick edit.

### 5. Help content moves to progressive disclosure, but validation stays visible

Decision:
- Descriptions, examples, format hints, and similar low-priority guidance SHALL move into tooltip-style or info-affordance disclosure patterns where possible, while required state and validation state SHALL remain visible in-context.

Rationale:
- Metadata-driven fields currently render too much supportive text by default, causing vertical growth.
- Tooltips are well suited for "help me understand this field" information, but not for "this field is currently invalid" information.
- Preserving visible validation maintains confidence and prevents hidden failure states.

Alternatives considered:
- Keep all help text visible for discoverability: rejected because it overwhelms routine editing.
- Move all help and validation into tooltips: rejected because hiding status and corrective information would make the UI less safe and less learnable.

### 6. Density rules vary by layer, not by a single global layout system

Decision:
- Density and layout rules SHALL be defined separately for collapsed cards, expanded quick edit, and full configuration rather than forcing one universal field layout.

Rationale:
- Each layer answers a different user question and therefore needs a different density target.
- A uniform layout across all layers would either waste space in collapsed views or over-compress complex dialog content.

Alternatives considered:
- Apply left-label/right-control layout everywhere: rejected because some dialog fields and validation states need full-width or multi-line treatment.
- Keep all layers vertically stacked for consistency: rejected because it preserves the current height problem.

## Risks / Trade-offs

- [Quick edit becomes too dense for unfamiliar actions] → Limit row-based compaction to high-frequency fields and preserve escalation to full configuration.
- [Tooltip-heavy guidance becomes inaccessible on keyboard or touch-like interactions] → Ensure info affordances are focusable and retain visible validation for critical issues.
- [Inspector-style dialog drifts away from metadata-driven group semantics] → Map metadata groups into user-facing groups deliberately instead of exposing raw metadata categories one-to-one.
- [Removing visible prose lowers immediate discoverability for new users] → Use careful labels, placeholder text, and focused tooltips rather than restoring permanent helper copy.
- [Some third-party plugin fields may not fit compact row layouts] → Allow the renderer to fall back to full-width field rows when metadata or control type indicates high complexity.

## Migration Plan

1. Update specs to define hierarchy, density, and progressive disclosure expectations.
2. Refactor collapsed slot cards to remove helper prose and retain only scan-critical state.
3. Redesign expanded quick edit into a compact row-based layout with fewer visible headings.
4. Remove summary-only blocks from expanded edit content and keep summary semantics in scan surfaces.
5. Reorganize the full configuration dialog into a clearer inspector-style grouping model.
6. Move field descriptions, examples, and input hints into tooltip or info affordances while preserving visible validation.
7. Validate the result against multiple built-in plugin parameter sets to ensure compact layouts still handle picker fields, sensitive parameters, and long guidance safely.

Rollback strategy:
- Revert the slot card and dialog presentation changes while leaving the underlying layered editing and metadata models intact.

## Open Questions

- Should quick edit retain inline appearance editing by default, or should appearance move fully into full configuration for denser lists?
- Do we want a shared reusable "field help affordance" control for tooltip-based metadata help, or is direct per-template tooltip wiring sufficient?
- Should the dialog group labels remain "Required / Optional / Advanced" internally for traceability, even if the user-facing headings become task-based?
