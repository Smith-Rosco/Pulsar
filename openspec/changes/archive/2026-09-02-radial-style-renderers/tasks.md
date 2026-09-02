## 1. Renderer factory & wiring

- [x] 1.1 Create `Core/Rendering/StyleRendererFactory.cs` with `Create(string id)` resolving by case-insensitive id from an injected `IEnumerable<IRadialRenderer>`, unknown id → Default renderer; add `StyleRendererFactoryTests` covering registered id, unknown id fallback, default-config resolution
- [x] 1.2 Register `DefaultRadialRenderer` (as fallback) + factory in `App.xaml.cs` `ConfigureServices` and verify `IServiceProvider.GetService<StyleRendererFactory>()` resolves with the Default renderer present
- [x] 1.3 Update `RadialMenuViewModel.ApplyRadialRendering` to resolve `_renderer` via the factory from `settings.RadialRenderer` (keeping optional injection so existing tests compile) and verify `ApplyRadialRendering` picks the configured renderer on menu open
- [x] 1.4 Add renderer-registry test asserting an unknown `RadialRenderer` value falls back to Default without throwing

## 2. ClassicRing renderer

- [x] 2.1 Create `Core/Rendering/ClassicRingRadialRenderer.cs` (Id `ClassicRing`): `ResolveHighlight` = accent stroke ring + reduced blur glow; `RenderDecorations` paints an outer thin ring + quadrant ticks from tokens; all shapes `IsHitTestVisible=false`
- [x] 2.2 Add `ClassicRingRadialRendererTests`: active/inactive resolve to expected highlight, decorations render without throwing, no `DropShadowEffect`, decorations non-hit-testable
- [x] 2.3 Verify `dotnet build Pulsar/Pulsar/Pulsar.csproj` succeeds (0 new errors) after adding the renderer

## 3. Glassmorphism renderer

- [x] 3.1 Create `Core/Rendering/GlassmorphismRadialRenderer.cs` (Id `Glassmorphism`): `ResolveHighlight` = translucent fill layer + 1px accent-hover stroke + soft edge blur; `RenderDecorations` paints layered translucent disc + top highlight arc from tokens; all shapes `IsHitTestVisible=false`
- [x] 3.2 Add `GlassmorphismRadialRendererTests`: active/inactive resolve, decorations render without throwing, no `DropShadowEffect`, decorations non-hit-testable
- [x] 3.3 Verify `dotnet build Pulsar/Pulsar/Pulsar.csproj` succeeds (0 new errors) after adding the renderer

## 4. Renderer resource pack

- [x] 4.1 Create `Core/Rendering/RendererResourcePack.cs` holding per-renderer numeric/alpha constants (stroke thickness, alpha, radii) and have both new renderers consume it; add `RendererResourcePackTests` asserting pack values are shared and default-preserving
- [x] 4.2 Verify the Default renderer is untouched (no resource pack coupling) and existing `DefaultRadialRendererTests` still pass unchanged

## 5. Settings UI: renderer + preset selectors

- [x] 5.1 Add `Settings.Appearance.RendererStyle` and `Settings.Appearance.ThemePreset` (+ option labels) to `Strings.resx` (EN) and `Strings.zh-CN.resx` (ZH) and verify both compile
- [x] 5.2 Add a renderer-style `ComboBox` (Default/ClassicRing/Glassmorphism) and a theme-preset `ComboBox` (System/Dark/Light/MatchaForest/GlacialIce/MorandiMuted) section to `SettingsGeneralPage.xaml`
- [x] 5.3 Wire both selectors through `SettingsViewModel` observable properties + `ConfigEditSession`/`RebuildCache` pattern (never `UpdateHotkey()`); add round-trip tests asserting save applies without reverting `Profiles.json`
- [x] 5.4 Verify saving the selectors triggers `ConfigUpdated` → `ApplyRadialRendering` re-render (assert renderer/preset change observed on next open)

## 6. Tests & verification

- [x] 6.1 Run `dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj` — full suite green (no regressions in renderer/settings tests)
- [x] 6.2 Build `Pulsar/Pulsar/Pulsar.csproj` — 0 errors
- [x] 6.3 Manual QA (requires human): switch renderer Classic/Glassmorphism and preset in settings, verify menu re-renders both themes; mode tone holds under every renderer

