# radial-renderer-contract — Design

## Context

Pulsar's radial menu is a circular-orb layout (`SlotOrb`), not a sector pie — so StarPie's `IRadialStyleRenderer` (sector brushes, `Math.Atan2` geometry) cannot be ported verbatim; a Pulsar-shaped contract must be built. Current rendering is locked into XAML:

- `SlotOrb.xaml` hard-codes the active-slot glow inside `Style.Triggers` (Blur/DropShadow effects), and `SlotOrb.xaml.cs` already owns an `OnIsActiveChanged` hook.
- Theme brushes live as resource keys in `Theme.Dark.xaml` / `Theme.Light.xaml` (`Theme.Orb.*`, `Theme.Accent.*`, `Theme.Radial.*`), injected via `IThemeService.ApplyTheme()`.
- Performance discipline is already codified: `RadialMenuWindow.xaml` deliberately avoids per-element `DropShadowEffect`.

See proposal.md — Why for motivation; specs/ define the behavioral contract this design satisfies.

## Goals / Non-Goals

**Goals:**
- A renderer seam that owns slot-highlight decisions, keeping Pulsar's no-per-slot-drop-shadow discipline.
- A typed token model over the existing `Theme.*` resource keys so renderers and future presets consume a stable contract.
- A preset resolution layer (`System` / `Dark` / `Light` / named preset → token set, with safe fallback) that preserves today's visuals by default.
- Everything that makes a highlight/visual decision is pure and unit-testable without touching the WPF tree.

**Non-Goals:**
- Building new visual styles (Glassmorphism / CleanSectors / CatPaw) — this change only proves the seam with the default (current-look) renderer.
- Color-tune / preset-management UI, eyedropper, custom-icon import — future changes on top of this seam.
- Plugin-hosted renderers via `IPluginRegistry` — future change; the contract is designed so a plugin can register a renderer later.
- Changing layout, interaction, or the `RadialMenuMode` lifecycle.

## Decisions

### D1: `IRadialRenderer` — a decision-producing contract, not a paint-into-tree contract

```csharp
public interface IRadialRenderer
{
    string Id { get; }
    void Initialize(IRadialThemeTokens tokens);
    IRadialSlotHighlight ResolveHighlight(bool isActive);          // pure: fill/stroke/effect/opacity/blur
    void RenderDecorations(Canvas canvas, double cx, double cy,
                           double wheelRadius, double coreRadius); // thin WPF adapter, default no-op
}
```

- `ResolveHighlight(bool)` is a pure function of state → a data record (`IRadialSlotHighlight`: glow brush, effect kind, blur radius, opacity). The renderer **never** walks the element tree, so it is Moq-able and unit-testable headlessly (Pulsar's mock discipline).
- `RenderDecorations` is the only WPF-coupled member and is a no-op in the default renderer, so the default path stays purely data-driven.
- **Alternative considered**: StarPie's `IRadialStyleRenderer` (sector brushes + `ApplySectorHighlight(Path)`). Rejected: Pulsar has no sector `Path` elements; binding the contract to `Path` would leak the template shape into the seam.

### D2: `SlotOrb` implements a narrow surface, applied from its existing `OnIsActiveChanged`

`SlotOrb` gains an internal method `ApplyHighlight(IRadialSlotHighlight highlight)` that writes the glow brush/effect/opacity onto its `ActiveShape`/`OrbFill` elements (replacing the trigger-embedded effect). `OnIsActiveChanged` (already present) calls the injected renderer and forwards the result. The hard-coded highlight effect is removed from `SlotOrb.xaml`'s active-state trigger; triggers keep only layout/opacity animation.

- **Alternative considered**: renderer writing directly into `SlotOrb`'s `x:Name`ed internals. Rejected: those are private template elements; the narrow method keeps the seam and the control's encapsulation.

### D3: `IRadialThemeTokens` — typed projection of existing resource keys

```csharp
public interface IRadialThemeTokens
{
    Brush OrbFill { get; }  Brush OrbStroke { get; }  Brush OrbText { get; }
    Brush ActiveGlow { get; }  Brush LabelBackground { get; }  Brush LabelForeground { get; }
    Brush Accent { get; }  Brush AccentHover { get; }  Brush AccentForeground { get; }
    Brush RadialTitleForeground { get; }  Brush RadialTitleShadow { get; }  Brush RadialTitleScrim { get; }
}
```

A single resolver (`RadialThemeTokenSet`) reads these from the active theme resource dictionaries so tokens are always consistent with what other surfaces see; renderers depend on the interface, never on resource-key strings.

### D4: `RadialThemePresetResolver` — layered resolution with fallback

Mirrors StarPie's `BaseStyleRenderer.Initialize` layering but adapted to Pulsar's two built-in dictionaries:

- `System` → follow Windows dark/light (`AppThemeManager.IsWindowsInDarkTheme` equivalent) → resolve to `Dark` or `Light`.
- `Dark` / `Light` → the existing `Theme.Dark.xaml` / `Theme.Light.xaml` token sets (source of truth unchanged).
- Named preset (first batch: `MatchaForest`, `GlacialIce`, `MorandiMuted`, hex token sets ported from the StarPie reference) → resolved from a static preset catalog.
- Unknown value → fall back to `Light`/`Dark` (theme default), log a warning, never throw.

Resolution happens at menu open / theme change; the resulting token set is handed to the renderer via `Initialize`.

### D5: Mode tone (visual-identity) flows through tokens, not the renderer

Task-vs-Action cool/warm differentiation is a token concern: the preset resolver (or a mode-aware token decorator) selects the accent/glow token by `RadialMenuMode` (Task → cool, Action → warm), and the renderer merely consumes the already-resolved tokens. Because the renderer and preset are downstream of mode selection, changing renderer/preset cannot drop the mode tone — satisfying the modified `visual-identity` spec.

### D6: Wiring & settings

- Register in `App.xaml.cs` `ConfigureServices` (DI): `IRadialRenderer` (default impl), `RadialThemePresetResolver`, token set factory — `AddSingleton`, consistent with existing registrations.
- Additive `ProfileSettings`: `string RadialRenderer = "Default"` and `string RadialThemePreset = "System"` (camelCase persistence, no schema break). `RadialMenuViewModel` / `MenuSession` apply on open; `ConfigUpdated` re-applies.
- Defaults (`Default` + `System`) reproduce current visuals exactly; existing tests must stay green without change.

## Risks / Trade-offs

- **[Risk] Moving highlight out of XAML triggers changes visuals subtly** → Mitigation: default renderer reproduces the exact current glow (same blur radius/opacity); spec pins "visually equivalent"; visual QA gate in tasks.
- **[Trade-off] Named presets are hex ported from StarPie** → they are opt-in only; default remains `System` → current look. Color-tune UI is explicitly deferred.
- **[Risk] Token resolver duplicating resource reads drifts from XAML** → Mitigation: single resolver reads from the same merged dictionaries `ThemeService` injects; a test asserts token values equal the dictionary values for both themes.
- **[Trade-off] `RenderDecorations` WPF-coupled member** → kept thin and default no-op; decorative styles are a future change and already isolated behind the seam.

## Migration Plan

- Backward-compatible: existing `Theme.*` keys remain valid for all non-renderer surfaces; new settings are additive.
- Rollback: set settings to defaults and (if needed) revert the `SlotOrb.xaml` trigger change — the seam can be left in place harmlessly since default renderer is behavior-neutral.

## Open Questions

- Exact first-batch preset catalog (which named presets ship) — does not change specs/approach; can be finalized during implementation with the user.
- Whether `RadialRenderer` setting should be exposed in settings UI now or only the preset — deferred; both default to safe values and UI is a follow-up.
