# radial-renderer-contract — Proposal

## Why

Pulsar's radial menu rendering is locked into a single visual style: a hard-coded circular `SlotOrb` whose highlight (`DropShadowEffect`/`BlurEffect`) and theme brushes are embedded in XAML `Style.Triggers` and two hand-maintained `Theme.Dark/Light.xaml` dictionaries. The roadmap (direction 2, borrowed from StarPie's `IRadialStyleRenderer` + preset system) identifies this as Pulsar's largest product gap versus StarPie: no pluggable renderer, no theme presets, no per-item color tuning. Today, adding a new visual style or color scheme means copying and hand-editing XAML across multiple files.

## What Changes

- **Renderer contract (`IRadialRenderer`)**: introduce a pluggable rendering seam that owns slot highlight application and decorative layer painting, replacing the hard-coded effects buried in `SlotOrb.xaml`.
- **Theme token layer (`IRadialThemeTokens`)**: abstract the existing `Theme.Orb.*` / `Theme.Accent.*` / `Theme.Radial.*` resource keys behind a typed token model, so renderers and future theme presets consume a stable contract instead of raw resource keys.
- **Preset resolution layer**: a resolver that maps a configured theme value (`System` → follow OS / `Dark` / `Light` / named preset) to a concrete token set with a safe fallback, mirroring StarPie's `BaseStyleRenderer.Initialize` layering.
- **Highlight strategy**: extract the active-slot glow out of `SlotOrb.xaml` triggers into an injectable `IRadialRenderer.ApplySlotHighlight`, keeping Pulsar's existing performance discipline (no `DropShadowEffect` where it can be avoided).
- **Defaults preserve current behavior**: default renderer + default preset reproduce today's Dark/Light look exactly; no breaking visual change.

This is the foundation phase of direction 2 — deliberately scoped to *contract + tokens + presets*. Multi-style renderer forms (Glassmorphism / CleanSectors / CatPaw), color-tune UI, and plugin-hosted themes are future changes built on this seam.

## Capabilities

### New Capabilities
- `radial-renderer-contract`: the `IRadialRenderer` seam — injectable slot-highlight application and decoration rendering, with a default implementation preserving current visuals.
- `radial-theme-presets`: the `IRadialThemeTokens` model plus a preset resolution layer (`System`/`Dark`/`Light`/named preset → token set with fallback).

### Modified Capabilities
- `visual-identity`: Pulsar SHALL continue to differentiate Task vs Action modes visually; renderer/token resolution must preserve that contract (cool vs warm tone differentiation) — the delta pins that mode-based differentiation survives the new token seam.

## Impact

- **Affected code**:
  - `Core/Rendering/` (new) — `IRadialRenderer`, `IRadialThemeTokens`, `RadialThemePresetResolver`, default renderer implementation
  - `ViewModels/SlotViewModel.cs` / `Views/Controls/SlotOrb.xaml` — highlight applied via renderer instead of inline `Style.Triggers`
  - `Views/RadialMenuWindow.xaml` — decoration layer hook point; theme resources resolve through tokens
  - `Themes/Theme.Dark.xaml` / `Theme.Light.xaml` — kept as the source of the two built-in token sets; resolved through the token layer rather than only raw resource keys
  - `Services/ThemeService.cs` / `App.xaml.cs` — wire the renderer + preset resolver into DI and apply at menu open / theme change
  - `Models/ProfilesConfig.cs` — additive settings: `RadialRenderer` (style id), `RadialThemePreset` (preset id) with defaults
- **Dependencies**: reuses `IThemeService.ApplyTheme` seam and existing `Theme.*` resource keys; no plugin-API changes in this phase.
- **No breaking changes**: default renderer + default preset reproduce the current look; existing theme/resource keys remain valid for other surfaces.
