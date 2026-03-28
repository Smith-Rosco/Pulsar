## Context

Pulsar uses a multi-headed UI architecture: `App.xaml` intentionally avoids loading full theme dictionaries globally, while `ThemeService` injects theme resources into windows and pages at runtime. That model keeps the radial menu isolated from standard Wpf.Ui windows, but it also means resource lookup must respect host and element boundaries.

Today, slot type and health tones are declared in `Styles/SlotStyles.xaml`, which is usually merged locally by pages and user controls. At the same time, `SlotBrushConverter` resolves tone keys through `Application.Current.TryFindResource(...)`, which skips the active element resource tree and can miss those locally merged brushes. The result is a failure mode where slot badges keep their container border but render their text with a transparent foreground.

The project has already encountered the same architectural smell in button hover rendering. Pulsar solved that class of bug by replacing Wpf.Ui `Appearance`-driven state colors with explicit Pulsar button templates and theme tokens. This change extends the same principle to slot tones so badge text color becomes a stable, host-level contract rather than an incidental local resource.

## Goals / Non-Goals

**Goals:**
- Make slot type and health tone colors available through the same theme-injection path used by standard Pulsar surfaces.
- Ensure slot badges render readable text consistently in settings pages, dialog content, and the radial menu.
- Remove the need for slot tone color resolution to depend on application-level global lookup assumptions.
- Continue the project-wide move away from Wpf.Ui default visual state color behavior in surfaces affected by this change.

**Non-Goals:**
- Redesign slot layout, copy, spacing, or information hierarchy beyond readability requirements already covered by existing specs.
- Replace every local style dictionary with global resources; component-scoped layout and border styles can remain local.
- Introduce new user-facing theme customization or config options.
- Rewrite unrelated converters or theme resources that are not part of slot tone visibility or the known button contrast regression class.

## Decisions

### Decision: Promote slot tones into theme-level semantic tokens
Slot type and health brushes will move out of local-only slot style dictionaries and become semantic theme tokens available through theme injection. In practice, standard themes and the radial theme surface must expose the slot tone brush keys that slot-related views depend on.

Rationale:
- Theme tokens match the existing Pulsar direction used for button state colors.
- Slot tones are semantic UI colors used across multiple surfaces, so they should live with other semantic theme resources rather than only inside one component dictionary.
- Theme injection already defines the supported resource boundary for windows and pages.

Alternatives considered:
- Keep slot tones in `SlotStyles.xaml` and make every host manually merge them at higher scope. Rejected because it preserves fragile coupling between individual views and shared semantic colors.
- Leave slot tones local and only patch the converter. Rejected as the end-state because it fixes the immediate symptom but preserves a split resource model.

### Decision: Treat `SlotStyles.xaml` as component styling, not the source of shared color semantics
`SlotStyles.xaml` should continue to own chip padding, corner radii, border styles, and slot-specific view composition, but not the only authoritative definition of semantic tone brushes that multiple hosts must resolve.

Rationale:
- It preserves component encapsulation for visual structure without making shared color semantics dependent on local merges.
- It keeps the future mental model simple: layout and reusable styles can be local, semantic colors belong to the theme layer.

Alternatives considered:
- Keep both local and theme copies of the same tone brushes. Rejected because duplicated keys create drift risk and ambiguous ownership.

### Decision: Replace application-global slot brush lookup with host-safe resolution
Slot brush resolution should no longer rely on `Application.Current.TryFindResource(...)` for slot tone keys. The implementation should resolve from the active themed scope or use a contract that is guaranteed to be backed by theme-injected resources.

Rationale:
- Global application lookup conflicts with Pulsar's architecture, where the real theme boundary is the active window/page/control tree.
- The failure mode is silent because missing brushes fall back to `Transparent`, making bugs visually subtle but severe.

Alternatives considered:
- Continue global lookup and register slot tones in `App.xaml`. Rejected because it breaks the project's UI isolation rule and increases the chance of cross-surface contamination.

### Decision: Finish the migration away from Wpf.Ui `Appearance` in affected dialogs
Any affected surface still using `Appearance="Primary"` or similar for critical actions should adopt explicit Pulsar button styles so hover and pressed foreground behavior remains deterministic under the multi-headed theme model.

Rationale:
- The button issue and slot issue share the same root pattern: visual semantics should not depend on third-party default resource behavior crossing unstable boundaries.
- Removing known outliers now reduces regression noise while this change is touching the same architectural seam.

Alternatives considered:
- Leave residual `Appearance` usage in place because it is not directly related to slot badges. Rejected because it perpetuates a known class of contrast regressions in the same subsystem.

## Risks / Trade-offs

- [Theme token ownership becomes broader] -> Mitigation: keep only semantic slot tone brushes in the theme layer and leave structural slot styles in `SlotStyles.xaml`.
- [Radial and standard themes could drift] -> Mitigation: define the same slot tone token set for every supported host theme and validate all slot surfaces against the same key contract.
- [Converter or binding refactor may ripple across multiple views] -> Mitigation: keep the public tone key contract stable (`TypeToneKey`, `HealthToneKey`) while only changing where those keys are resolved.
- [Residual Wpf.Ui `Appearance` usage could reintroduce contrast bugs later] -> Mitigation: treat remaining occurrences as part of this cleanup and document explicit Pulsar styles as the only allowed pattern.

## Migration Plan

1. Define the authoritative slot tone token set in theme resources used by standard windows/pages and the radial menu.
2. Remove duplicate or conflicting slot tone brush definitions from component-local slot style resources.
3. Update slot tone resolution so slot badge text uses host-safe themed resources instead of application-global lookup.
4. Apply the updated contract to slot-related views and replace any remaining affected `Appearance`-based buttons with Pulsar styles.
5. Validate dialogs, settings pages, and the radial menu in both supported theme modes to confirm text remains readable.

Rollback strategy:
- Revert the change set as a unit if cross-host token injection causes regressions; no data migration is involved.

## Open Questions

- Should slot tone tokens live directly in `Theme.Dark.xaml` / `Theme.Light.xaml`, or in a dedicated shared token dictionary injected alongside those themes?
- Do we want slot tone keys to keep their existing names for compatibility, or adopt a stricter `Theme.Slot.*` naming scheme during this change?
- Are there any non-slot converters following the same `Application.Current.TryFindResource(...)` pattern that should be captured as follow-up work rather than included here?
