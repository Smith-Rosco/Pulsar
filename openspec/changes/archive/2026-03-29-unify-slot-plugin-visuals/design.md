## Context

Pulsar currently models built-in plugin identity in more than one place. `SettingsPluginsPage` renders plugin cards from plugin/runtime metadata, while the Create Slot flow builds its own `PluginTypeOption` list in `SettingsViewModel.BuildAddSlotOptions()` and also applies separate icon/color suggestion logic in `AddSlotViewModel`. That duplication has already drifted: some built-in plugins use different icon keys in Create Slot than they do in plugin metadata or slot suggestion helpers.

The Create Slot dialog also assigns strong plugin-specific default colors both in draft construction and in suggestion logic. That makes plugin branding visually dominate a workflow that is supposed to help the user choose behavior first and optional appearance second. In practice, slot color behaves more like user-authored organization than canonical plugin identity.

This change is cross-cutting because it touches plugin metadata consumption, slot draft defaults, slot suggestion logic, and two separate settings surfaces. It also overlaps with existing OpenSpec capabilities around plugin display identity, Create Slot information hierarchy, and shared icon rendering.

## Goals / Non-Goals

**Goals:**
- Establish one canonical source for built-in plugin identity fields used by both the Plugins page and the Create Slot picker.
- Remove plugin-branded default colors from new slot creation so color remains optional appearance polish.
- Keep Create Slot visually aligned with existing plugin-management surfaces without collapsing both screens into the exact same layout.
- Reduce hard-coded plugin icon/color switch statements where the values are really metadata concerns.

**Non-Goals:**
- Renaming built-in plugin IDs or redesigning the plugin runtime contract.
- Reworking the entire slot preview system or changing validation behavior unrelated to plugin visuals.
- Removing the user's ability to choose a custom slot color or icon.
- Forcing the Plugins page and Create Slot picker to share a single XAML component if a lighter shared-data approach is sufficient.

## Decisions

### 1. Canonical plugin identity comes from plugin metadata, not page-local lists

Decision:
- Create Slot SHALL build plugin-picker entries from canonical built-in plugin metadata rather than maintaining its own hand-authored icon/name/description/color table in `SettingsViewModel`.

Rationale:
- The current duplication is the direct cause of icon drift between the Plugins page and Create Slot.
- Built-in plugin metadata already represents the product's intended display identity and is the correct layer to reuse.

Alternatives considered:
- Keep `BuildAddSlotOptions()` and manually synchronize it with plugin metadata: rejected because the repo already shows drift and the maintenance cost scales with every plugin tweak.
- Make Create Slot derive identity from plugin runtime instances instead of metadata: rejected because the product already has a metadata model intended for user-facing display concerns.

### 2. Plugin accent color is not the default slot color

Decision:
- Built-in plugin metadata MAY continue to expose an accent or tone hint for plugin-management surfaces, but newly created slot drafts SHALL NOT automatically inherit strong plugin-specific colors as their default `Slot.Color`.

Rationale:
- A slot is a user-authored object. Its color is closer to workflow organization than plugin identity.
- Defaulting every new slot to a saturated plugin color makes optional appearance choices feel mandatory and crowds the authoring hierarchy.

Alternatives considered:
- Keep current plugin-specific default colors and simply soften them: rejected because the core problem is ownership of the color decision, not just saturation.
- Remove color support from slot creation entirely: rejected because optional appearance polish is still useful when the user chooses it.

### 3. Create Slot uses restrained plugin identity rendering

Decision:
- The Create Slot picker SHALL render plugin identity with the same canonical icon and descriptive copy as the Plugins page, but with a more neutral visual treatment that emphasizes selection state over plugin-branded color.

Rationale:
- The two surfaces have different jobs, so they do not need pixel-for-pixel parity.
- They do need clear family resemblance so users recognize the same built-in plugin immediately across settings surfaces.

Alternatives considered:
- Reuse the full `ExpandableCard` component inside Create Slot: rejected because the management-focused affordances and expanded analytics content do not fit the slot-selection workflow.
- Leave Create Slot visually distinct as long as text/icon values match: rejected because inconsistent icon container treatment still communicates different product semantics for the same plugin.

### 4. Slot suggestion helpers should only suggest identity fields that belong to the slot

Decision:
- Slot suggestion logic SHALL continue to suggest labels and context-sensitive icons where those values help complete a slot draft, but plugin identity values that are already canonical metadata SHALL not be re-declared in parallel switch statements unless they represent slot-specific behavior.

Rationale:
- Some suggestions are genuinely slot-specific, such as changing the command plugin icon for `sendkeys`.
- Base plugin identity, however, should not live in `BuildAddSlotOptions()`, `BuildSlotTemplate()`, and `AddSlotViewModel.BuildSuggestedIcon/Color()` simultaneously.

Alternatives considered:
- Remove all slot suggestion logic: rejected because dynamic labels and action-specific icon suggestions still improve authoring.
- Keep metadata and suggestion switch tables side by side: rejected because it preserves the current ambiguity around which layer owns a value.

## Risks / Trade-offs

- [Metadata for some built-in plugins is incomplete or inconsistent with runtime `Icon` values] -> Normalize built-in metadata first and treat it as the canonical source before rewiring Create Slot.
- [Removing default slot colors makes some slots feel visually flatter at first] -> Preserve optional color picking and use selection state, typography, and spacing to keep the picker legible without relying on saturated accents.
- [Action-specific icon suggestions could regress when base icon logic moves to metadata] -> Keep a clear split between canonical plugin identity and action-specific slot overrides, with tests for known cases such as `sendkeys`.
- [Plugins page and Create Slot could still drift stylistically even with shared data] -> Define a small shared display model or helper for plugin identity so both surfaces consume the same fields and icon rendering path.

## Migration Plan

1. Update OpenSpec requirements for plugin display identity, Create Slot hierarchy, and shared icon rendering.
2. Introduce or refine the canonical built-in plugin display model consumed by settings surfaces.
3. Refactor Create Slot option building to read canonical metadata rather than a hand-authored table.
4. Remove strong plugin-color defaults from slot draft creation and suggestion logic while preserving manual color selection.
5. Update Create Slot card styling so selection emphasis comes from neutral structure and selected-state treatment rather than plugin accent fill.
6. Validate that Plugins and Create Slot present the same icon/name/description identity for built-in plugins, and that new slots start without a forced plugin color.

Rollback strategy:
- Revert the Create Slot metadata wiring and styling changes while leaving any metadata cleanups that are independently correct and non-breaking.

## Open Questions

- Should built-in plugin metadata keep `AccentColor` as a management-surface hint, or should plugin-facing surfaces converge on neutral styling there too?
- Should a new slot with no chosen color persist `Slot.Color` as empty/null-equivalent, or should Pulsar assign a neutral theme token at render time?
- Are there any built-in plugins besides `com.pulsar.command` with action-specific icon overrides that should remain in suggestion logic after base identity is centralized?
