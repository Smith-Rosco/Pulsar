## Why

Plugin icons render inconsistently across Pulsar surfaces: the Plugins settings page renders icons as colorful accent-colored squares (via `AccentColor` hardcoded per plugin), while the Add Slot picker renders icons as neutral circles through `SlotOrb` — and the picker also double-borders icons by wrapping `SlotOrb` (which renders its own circle) inside a square `Border` container. This visual fragmentation undermines a coherent design language and creates confusion when users encounter the same plugin represented differently.

## What Changes

- Remove per-plugin `AccentColor` as a visual differentiator for icon backgrounds in the Plugins settings page
- Update `ExpandableCard` to render icons using neutral theme background instead of accent-colored background
- Update the Add Slot picker to render plugin icons directly via `SlotOrb` without an outer `Border` container wrapper (eliminating the double-border / square-inside-circle visual artifact)
- Both surfaces converge on the same neutral-circle `SlotOrb` rendering: consistent with the radial menu's own slot visual language
- `AccentColor` field in `UIHints` is retained in the data model (may serve other future purposes) but is no longer used to colorize icon backgrounds

## Capabilities

### New Capabilities
- `plugin-icon-surface-consistency`: Plugin icons render through the same neutral `SlotOrb` visual treatment across the Plugins settings page and the Add Slot picker, eliminating accent-color fragmentation and double-border artifacts.

### Modified Capabilities
- `slot-editor-shared-icon-rendering`: The Add Slot picker's icon rendering path is updated — the outer `Border` (PickerIconContainerStyle) wrapper around `SlotOrb` is removed, so icon rendering goes directly through `SlotOrb` with no container double-framing.
- `plugin-display-identity`: Plugin display identity no longer includes a visual accent color as part of the icon presentation contract; canonical identity is name + description + icon key only.

## Impact

- `Views/Controls/ExpandableCard.xaml` — remove accent-color background from icon container; switch to neutral theme resource
- `Views/Pages/SettingsPluginsPage.xaml` — remove `OrbBackground` binding that passes `AccentColor`
- `Views/Dialogs/Contents/AddSlotContent.xaml` — remove outer `Border` (PickerIconContainerStyle) wrapping `SlotOrb` in plugin type picker; let `SlotOrb` render at appropriate size directly
- `Core/Plugin/Metadata/UIHints.cs` — `AccentColor` property retained but noted as not used for icon background presentation
- No breaking changes to plugin plugin APIs; `AccentColor` is not removed from the data model
- No changes to `SlotOrb` control itself
