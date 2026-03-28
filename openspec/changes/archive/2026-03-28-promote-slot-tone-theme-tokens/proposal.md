## Why

Slot type and health badge text currently depends on color resources that are declared locally in `SlotStyles.xaml` but resolved globally through `Application.Current`, which makes the badges disappear when the lookup leaves the active element resource tree. Pulsar already solved a similar class of hover-state regressions for buttons by promoting visual state colors into explicit theme-driven styles, so now is the right time to bring slot tone colors into the same semantic token model.

## What Changes

- Promote slot type and slot health tone brushes from local `SlotStyles.xaml` resources into theme-injected semantic tokens that are available to standard hosts and the radial window.
- Replace slot tone resolution paths that rely on application-level resource lookup with a host-safe contract that resolves brushes from the active themed resource scope.
- Standardize slot badge rendering across dialogs, settings pages, and the radial menu so tone text remains visible in all supported surfaces and themes.
- Remove remaining reliance on Wpf.Ui button `Appearance` values in affected surfaces where explicit Pulsar styles are required to keep text/background contrast predictable.

## Capabilities

### New Capabilities
- `slot-tone-theme-tokens`: Defines semantic theme tokens and rendering rules for slot type/health tones across Pulsar surfaces.

### Modified Capabilities
- `slot-editor-information-hierarchy`: Slot identity and health indicators must remain readable in full configuration and creation surfaces while preserving the established editing hierarchy.

## Impact

- Affected code includes `ThemeService`, theme dictionaries, slot style resources, slot brush resolution, slot-related dialogs/pages, the radial menu, and any remaining dialog surfaces that still depend on Wpf.Ui `Appearance` state colors.
- This change reduces a recurring class of UI regressions caused by mismatched resource scope assumptions in the multi-headed UI architecture.
- No external API or config schema changes are expected, but UI rendering contracts and OpenSpec coverage for slot tone visibility will expand.
