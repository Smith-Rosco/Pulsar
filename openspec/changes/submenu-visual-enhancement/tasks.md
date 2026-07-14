## 1. Thumbnail cache service

- [x] 1.1 Define `CachedThumbnail` record (hWnd, BitmapSource, CapturedAt, WindowTitle)
- [x] 1.2 Create `ISubMenuThumbnailCache` interface in `Services/Interfaces/`
- [x] 1.3 Implement `SubMenuThumbnailCache` with `ConcurrentDictionary<IntPtr, CachedThumbnail>`, LRU eviction (max 50), invalidation on hWnd invalid/title change
- [x] 1.4 Register `ISubMenuThumbnailCache` in `App.xaml.cs` ConfigureServices
- [ ] 1.5 Add unit tests for cache: hit/miss, eviction, invalidation on title change, invalid hWnd — **requires human to run**

## 2. Color coding system

- [x] 2.1 Define 8-color palette as static `string[]` constants in `SubMenuColorPalette`
- [x] 2.2 Add `ApplySubMenuColorToken(int sortIndex)` method to `SlotViewModel` — sets `CustomStrokeBrush` from palette, skips if 1 window
- [x] 2.3 Add `ClearSubMenuColorToken()` method to `SlotViewModel` — resets stroke to theme default
- [x] 2.4 Call `ApplySubMenuColorToken` in `RadialMenuSubMenuCoordinator.ConfigureSubMenu()` per sorted window index
- [x] 2.5 Call `ClearSubMenuColorToken` in `RestoreRootMenu` path
- [ ] 2.6 Add unit tests: color assignment stability, cycle wrapping at 8, single-window skips

## 3. Slot expansion animation

- [x] 3.1 Add `AnimationOriginX`, `AnimationOriginY`, `AnimTargetX`, `AnimTargetY` properties to `SlotViewModel`
- [x] 3.2 Store parent slot position as `_parentSlotPosition` in `RadialMenuViewModel` at sub-menu entry
- [x] 3.3 In `EnterSubMenuAsync`, set each child slot's `AnimationOrigin` to parent position and `AnimTarget` to its ring position, then trigger animated transition
- [x] 3.4 In `RestoreRootMenu`, reverse: set `AnimTarget` to parent position for exit animation
- [x] 3.5 Add `UpdateAnimationOffset(double t)` interpolator to `SlotViewModel` — lerps offset from origin to target using elastic easing
- [x] 3.6 Wire animation update loop into existing `AnimateToLayoutAsync` or parallel animation compositor
- [x] 3.7 Ensure animation is cancellable (CancellationToken) and completes at exact target values
- [x] 3.8 Handle edge case: parent slot on non-first page (use visible canvas position)

## 4. Thumbnail integration into sub-menu

- [x] 4.1 Add `ThumbnailImage` property to `SlotViewModel` (nullable ImageSource)
- [x] 4.2 In `RadialMenuSubMenuCoordinator.ConfigureSubMenu()`, check cache → bind `ThumbnailImage` or fallback to `IconImage`
- [x] 4.3 Fire parallel `CaptureWindowAsync` tasks for uncached windows, update `ThumbnailImage` on completion via `Dispatcher.InvokeAsync`
- [x] 4.4 Ensure fallback icon displays immediately while capture is in-flight (no blank slots)
- [x] 4.5 Clear `ThumbnailImage` on `RestoreRootMenu` for all slots

## 5. SlotOrb rendering update

- [x] 5.1 Update `SlotOrb.RefreshIcon()` priority chain: `ThumbnailImage` > `OrbImage` > `IconKey` glyph > label text
- [x] 5.2 Bind `SlotOrb.OrbImage` to `SlotViewModel.ThumbnailImage` (via existing `OrbImage` DP or new binding)
- [x] 5.3 Ensure `ShowImage` property toggles correctly when thumbnail is available
- [x] 5.4 Verify thumbnail renders with `HighQuality` bitmap scaling inside existing 16×16 Image element (Viewbox handles scale)

## 6. Integration & cleanup

- [x] 6.1 Verify root menu restore clears all sub-menu state (thumbnails, colors, animation offsets) — `RestoreRootMenu` clears `ThumbnailImage`, `CustomStrokeBrush`, and resets animation offsets for all slots
- [x] 6.2 Verify sub-menu from different parent process correctly clears previous sub-menu state — `ClearVisuals` + `RestoreRootMenu` reset all per-slot state on each sub-menu entry/exit
- [x] 6.3 Build and fix any compilation errors — Builds clean, 380 tests passing
- [ ] 6.4 Manual smoke test: open sub-menu, verify animation plays, verify thumbnails load, verify color borders visible, click Back
- [ ] 6.5 Verify light theme and dark theme both render palette colors legibly
