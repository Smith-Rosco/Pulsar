## Context

Pulsar renders plugin icons in two primary surfaces:

1. **Plugins settings page** (`SettingsPluginsPage.xaml`) — uses `ExpandableCard` with `OrbBackground` bound to each plugin's hardcoded `AccentColor` hex string. Each plugin has a unique color (blue, green, orange, red, etc.), making the list visually fragmented.
2. **Add Slot picker** (`AddSlotContent.xaml`) — wraps a `SlotOrb` inside a `Border` styled by `PickerIconContainerStyle`. `SlotOrb` renders its own circular `OrbFill` ellipse; the outer `Border` adds a separate rounded-rectangle container. This produces a double-border artifact: a circle inside a square.

The `SlotOrb` control is the canonical icon renderer for slot surfaces (radial menu). It already handles icon key interpretation (emoji, Fluent/MDL2 glyphs, file paths) and renders a circle with a neutral theme background when no accent color is forced.

The fix is straightforward in both cases — no new abstractions required.

## Goals / Non-Goals

**Goals:**
- Eliminate the double-border artifact in the Add Slot picker by removing the outer `Border` wrapper around `SlotOrb`
- Eliminate accent-color fragmentation in the Plugins settings page by removing the `OrbBackground` accent-color binding from `ExpandableCard`
- Both surfaces converge on the same neutral `SlotOrb` circle rendering
- No changes to `SlotOrb` internals

**Non-Goals:**
- Redesigning `SlotOrb` itself
- Removing `AccentColor` from the `UIHints` data model (retained for potential future use)
- Changing icon rendering behavior for the radial menu orbs
- Touching any plugin beyond what is needed to stop consuming `AccentColor` visually

## Decisions

### Decision 1: Remove outer Border in picker, do not add IsTransparent

**Choice**: Remove the `<Border Style="{StaticResource PickerIconContainerStyle}">` wrapper entirely from `AddSlotContent.xaml`. Let `SlotOrb` render at the target size directly.

**Alternative considered**: Keep the `Border` but set `SlotOrb IsTransparent="True"` so the orb doesn't draw its own circle — the container provides the shape. This is how `ExpandableCard` currently works.

**Rationale**: The picker goal is consistency with the radial menu's visual language (circle). The `PickerIconContainerStyle` is a square-rounded-rect shape. Keeping it and making SlotOrb transparent would produce a square icon in the picker — inconsistent with the radial menu. Removing the wrapper entirely and letting SlotOrb render its circle is simpler and matches the intended visual.

### Decision 2: Neutral background in ExpandableCard, not per-plugin color

**Choice**: In `ExpandableCard.xaml`, replace the `OrbBackground`-driven `Background` binding with `{DynamicResource ControlFillColorSecondaryBrush}` (the standard WPF UI neutral fill). Remove `IsTransparent="True"` from the inner `SlotOrb` so the orb renders its own circle.

**Alternative considered**: Keep the accent color system but make colors less saturated / more harmonious. Requires per-plugin color curation and ongoing maintenance; doesn't solve the fundamental inconsistency with the picker surface.

**Rationale**: Neutral background is zero-maintenance, matches WPF UI design language, and is consistent with the Add Slot picker post-fix. The icon glyph itself provides sufficient identity — color is redundant.

### Decision 3: Retain OrbBackground dependency property on ExpandableCard

**Choice**: Keep the `OrbBackground` dependency property on `ExpandableCard` but stop using it internally for the icon container background.

**Rationale**: Removing the property would be a breaking change for any other consumers. Keeping it as a no-op for icon background is safe and non-breaking.

## Risks / Trade-offs

- **[Risk] ExpandableCard used elsewhere with OrbBackground** → The property is retained; existing bindings won't break. The visual change (no accent color) will apply to all ExpandableCard usages — check that no other page intentionally relies on the accent color for semantic meaning.
- **[Risk] SlotOrb circle size in picker feels too large/small without container** → Mitigated by setting `Size`, `Width`, and `Height` explicitly on the `SlotOrb` element to match the previous `PickerIconContainerStyle` dimensions (36×36).
- **[Risk] PickerIconContainerStyle becomes unused dead style** → Mark it for removal or leave as unused style — low risk either way.

## Migration Plan

Pure UI change. No data migration, no API changes, no configuration changes. Rollback is a git revert.

1. Update `ExpandableCard.xaml` — icon container background + SlotOrb transparency
2. Update `SettingsPluginsPage.xaml` — remove OrbBackground binding
3. Update `AddSlotContent.xaml` — remove Border wrapper, resize SlotOrb directly
4. Build and visually verify both surfaces

## Open Questions

- Should `PickerIconContainerStyle` in `AddSlotContent.xaml` be deleted outright, or left as dead style? (Low priority — either is acceptable)
- Are there other pages/controls that pass `OrbBackground` to `ExpandableCard` that need the binding removed?
