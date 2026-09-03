## Why

M0 already re-narrated Pulsar as a "heavy-duty office automation workbench" with three pillars (one-click Excel/WPS macros, legacy web-page scripts, secure form-fill sign-in), but the in-app entry points still present features flatly: settings navigation and the plugin list mix the three pillars with system utility plugins in no particular order. M2's "功能顺序 = 叙事顺序" requires the in-app surface to make the workbench mainline visible at a glance, so users (especially new ones) find the headline capabilities first and system plumbing recedes to the background.

## What Changes

- **Settings navigation reorder**: reorganize the settings navigation order (currently General → Slots → Plugins → Analytics → About in `SettingsPageCatalog`) so workbench-relevant entries are grouped and surface first; system/plumbing pages move later.
- **Plugin list grouping & ordering**: group/order the plugin management list so the three pillars (VBA runner, web scripts, PKI/form-fill) appear front and center, with system plugins grouped separately and visually demoted.
- **First-launch wizard ordering**: align the onboarding/usage-selection presentation order with the three-pillar narrative.
- **Localization**: refresh entry titles/descriptions to match the three-pillar framing (no hardcoded strings).

## Capabilities

### New Capabilities
- `home-screen-entry-organization`: Defines the stable entry-organization contract — the grouping and ordering rules applied to settings navigation, the plugin management list, and first-launch presentation, so the office-automation pillars are surfaced first and system utilities are demoted to a background group.

### Modified Capabilities
- None. `settings-shell-navigation` (the navigation mechanism) and `plugin-display-identity` (canonical names) are unchanged; this change only orders and groups existing entries.

## Impact

- **Settings shell**: `SettingsPageCatalog` page registration order and grouping.
- **Plugin management**: `PluginManagerViewModel` list ordering/grouping (three-pillar front, system behind).
- **Onboarding**: first-launch wizard step/selection ordering.
- **Localization**: `Strings.resx` + `Strings.zh-CN.resx` entry titles/descriptions.
- No configuration-format, plugin-contract, or data-model changes; no **BREAKING** changes.
