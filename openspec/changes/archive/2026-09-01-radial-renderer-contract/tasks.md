# radial-renderer-contract — Tasks

## 1. Rendering seam: contract & default renderer (test-first)

- [x] 1.1 Create `Core/Rendering/IRadialRenderer.cs` and `Core/Rendering/IRadialSlotHighlight.cs` (glow brush, effect kind, blur radius, opacity) and verify the project compiles
- [x] 1.2 Create `Core/Rendering/DefaultRadialRenderer.cs` implementing `ResolveHighlight(bool)` to reproduce the current active-slot glow (blur + opacity equivalent to today's trigger), with `RenderDecorations` as a no-op, and verify `Pulsar.Tests/Core/Rendering/DefaultRadialRendererTests` pass (active vs inactive resolve to expected highlight data; no `DropShadowEffect`)
- [x] 1.3 Add `IRadialRenderer.ResolveHighlight` purity test: same input state → same output record, no UI dependency (Moq-friendly)

## 2. Token layer

- [x] 2.1 Create `Core/Rendering/IRadialThemeTokens.cs` with the 11 typed brush properties (OrbFill/OrbStroke/OrbText/ActiveGlow/LabelBackground/LabelForeground/Accent/AccentHover/AccentForeground/RadialTitleForeground/RadialTitleShadow/RadialTitleScrim)
- [x] 2.2 Implement `RadialThemeTokenSet` reading from the active theme merged dictionaries, and verify a test asserts token values equal the corresponding `Theme.Dark.xaml` / `Theme.Light.xaml` dictionary values for both themes
- [x] 2.3 Create `Core/Rendering/RadialThemePresetResolver.cs` with layered resolution (`System`→OS, `Dark`, `Light`, named preset, unknown→fallback with warning) and verify resolver tests cover System resolution, named preset, and unknown-value fallback (spec `radial-theme-presets`)

## 3. Preset catalog

- [x] 3.1 Add static preset catalog for `MatchaForest` / `GlacialIce` / `MorandiMuted` (hex token sets ported from StarPie reference) and verify resolver returns the expected token set for each named preset
- [x] 3.2 Verify default (`System`) and unknown-value paths keep resolving to the existing Dark/Light token sets without visual change

## 4. SlotOrb integration

- [x] 4.1 Remove the hard-coded highlight effect from the active-state trigger in `Views/Controls/SlotOrb.xaml`, keeping only layout/opacity animation
- [x] 4.2 Add `SlotOrb.ApplyHighlight(IRadialSlotHighlight)` writing glow brush/effect/opacity onto `ActiveShape`/`OrbFill`, and invoke it from the existing `OnIsActiveChanged` via the injected renderer
- [ ] 4.3 Verify `RadialMenuWindow` opens with the default renderer and the active slot is highlighted visually equivalent to before (manual QA: both Dark and Light themes)

## 5. DI & settings wiring

- [x] 5.1 Register `IRadialRenderer`, `RadialThemePresetResolver`, and token set factory in `App.xaml.cs` `ConfigureServices` as singletons and verify resolution succeeds
- [x] 5.2 Add `ProfileSettings.RadialRenderer = "Default"` and `ProfileSettings.RadialThemePreset = "System"` (camelCase persistence, additive) and verify `ConfigService` round-trips them without validation errors
- [x] 5.3 Apply renderer + preset on menu open and on `ConfigUpdated` (re-resolve tokens → `Initialize` renderer), and verify changing preset re-renders the menu (manual QA)
- [x] 5.4 Keep mode tone flowing through tokens (Task→cool, Action→warm) so the modified `visual-identity` spec holds under the new seam, and verify a test asserts the token decorator selects accent/glow by `RadialMenuMode`

## 6. Tests & verification

- [x] 6.1 Unit tests green: `DefaultRadialRendererTests`, token-set equivalence, preset resolver, mode-tone decorator
- [x] 6.2 Run `dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj` — all pass (existing 380+ baseline unchanged)
- [x] 6.3 Build `Pulsar/Pulsar/Pulsar.csproj` — 0 errors
- [ ] 6.4 Manual QA (requires human): default look identical for Dark/Light; preset switch changes visuals; unknown preset falls back without error; both modes keep distinct tones
