## Context

The Pulsar radial menu currently supports a flat root→sub-menu navigation in Task/Switcher mode. When a process group with multiple windows is clicked, `RadialMenuViewModel.EnterSubMenuAsync()` clears all slot visuals and repopulates them with individual window items. There is no spatial transition connecting the clicked parent slot to the child windows, and all same-process windows display identical app icons.

This design covers three enhancements:
1. **Expansion animation** — child slots animate from parent slot position outward
2. **Window thumbnails** — per-hWnd screenshot cache replaces generic icons
3. **Color coding** — cycle-assigned border colors differentiate same-process windows

### Current architecture (relevant)
```
RadialMenuViewModel
├── _menuState: {Root, SubMenu}
├── EnterSubMenuAsync(windows, processName)
│   ├── AnimateToLayoutAsync()       ← radius/center/slot size expansion
│   ├── ClearVisuals()               ← resets all slots
│   ├── _subMenuCoordinator.ConfigureSubMenu()
│   └── _visualStateCoordinator.PrimeSubMenuPreview()
├── RestoreRootMenu()
│   ├── AnimateToLayoutAsync()       ← contraction
│   └── _subMenuCoordinator.RestoreRootMenu()
│
RadialMenuSubMenuCoordinator.ConfigureSubMenu()
├── centerSlot → BackActionStrategy
├── foreach slot: IconImage = win.AppIcon   ← ALL SAME ICON
└── foreach slot: ActionStrategy = WindowSwitchStrategy

SlotViewModel (per slot)
├── X, Y, Size          ← layout position
├── IconKey, IconImage   ← icon resolution
├── OffsetX, OffsetY     ← magnetic/parallax offsets
├── CurrentScale, CurrentOpacity
└── CustomFillBrush, CustomStrokeBrush, CustomForegroundBrush
```

## Goals / Non-Goals

**Goals:**
- Establish spatial continuity between parent slot click and sub-menu display via animation
- Replace generic app icons in sub-menu with window-specific thumbnails (with app icon fallback)
- Differentiate same-process windows via color-coded borders
- Reuse existing animation, capture, and caching infrastructure
- Zero breaking changes to root menu behavior

**Non-Goals:**
- Full hierarchical nested ring rendering (dual-ring with separate slot pools)
- DWM live thumbnail streaming (too expensive for 12 slots)
- Thumbnails for root-level process group slots (only sub-menu)
- Changing the data model (Profiles.json, PluginSlot)
- Extending sub-menu beyond Task/Switcher mode

## Decisions

### D1: Per-slot position animation via dedicated animation properties

**Decision**: Add `AnimTargetX`/`AnimTargetY` properties to `SlotViewModel` with smooth interpolation driven by `AnimateToLayoutAsync`-compatible update loop.

**Rationale**: The existing `OffsetX`/`OffsetY` properties are used for magnetic cursor attraction and reset on every frame. Adding a second offset layer would conflate concerns. Separate animation properties keep the magnetic effect independent.

**Alternatives considered**:
- Reuse `OffsetX`/`OffsetY` with a "mode" switch → conflates magnetic and animation state, hard to debug
- Directly animate `X`/`Y` layout properties → these are the canonical slot positions; mutating them for animation would break layout restore
- WPF Storyboard per-slot → requires code-behind complexity, breaks the MVVM animation abstraction already in place

### D2: Separate `SubMenuThumbnailCache` service (not extending PreviewService)

**Decision**: Create a new `ISubMenuThumbnailCache` service with its own `ConcurrentDictionary<IntPtr, CachedThumbnail>`.

**Rationale**: `PreviewService` manages a single center-slot preview with a live-preview pipeline (DWM thumbnail host). Its cache (`_cache`) is a `Dictionary<IntPtr, BitmapSource>` with no LRU eviction, no title-change invalidation, and no concurrency guarantee. Extending it for multi-slot sub-menu thumbnails would complicate an already focused service. A separate service keeps concerns clean: PreviewService = center preview (live/snapshot/icon), SubMenuThumbnailCache = sub-menu slot icons (thumbnail/icon fallback).

**Alternatives considered**:
- Extend `PreviewService` → conflates single-preview pipeline with bulk thumbnail cache
- Store thumbnails on `ProcessWindowInfo` → makes model mutable post-construction, violates immutability pattern used elsewhere
- No cache (capture every time) → repeated `CaptureWindowAsync` per sub-menu invocation is wasteful (200ms+ per window)

### D3: 8-color fixed palette, embedded as constants

**Decision**: Use a hardcoded array of 8 hex color strings chosen for visual distinction in both themes. No user configuration.

**Rationale**: The palette is a utilitarian differentiator, not a design customization. An 8-color cycle covers the practical maximum of same-process windows (rarely more than 5-6). Configuration would add UI complexity for negligible gain.

**Palette** (tuned for both dark #2D2D2D and light #FFFFFF backgrounds):
```
#FF6B6B (coral red)      #4ECDC4 (teal)
#FFD93D (gold)           #6C5CE7 (purple)
#A8E6CF (mint green)     #FF8A5C (orange)
#45B7D1 (sky blue)       #F78FB3 (pink)
```

### D4: Eager capture on sub-menu entry (with async fallback)

**Decision**: When `ConfigureSubMenu` runs, synchronously check cache and immediately display cached thumbnails. For uncached windows, initiate async capture tasks that update `SlotViewModel.ThumbnailImage` on completion. The slot initially shows the app icon (fallback) and transitions to the thumbnail when ready.

**Rationale**: `CaptureWindowAsync` involves `PrintWindow` Win32 calls and bitmap processing (~50-150ms per window). Eagerly initiating all captures in parallel maximizes cache population speed. Displaying fallback icons immediately prevents the sub-menu from appearing blank or with placeholder loading spinners.

**Alternatives considered**:
- Capture-before-display (block sub-menu open until all captures complete) → unacceptable latency, 3 windows × 100ms = 300ms delay
- Lazy capture on hover → misses the opportunity to pre-warm cache for all visible slots

### D5: Thumbnail sizing at 48×48 source, rendered via existing 24×24 Viewbox

**Decision**: Capture thumbnails at 48×48 pixels source resolution. The existing `SlotOrb` Viewbox (24×24 design system, `Uniform` stretch) will scale them naturally to the rendered orb size (typically 42-60px).

**Rationale**: 48×48 provides enough detail to distinguish window content (text layout, color regions) while keeping memory footprint low (~9KB per thumbnail uncompressed). The Viewbox already handles scale-agnostic rendering. No XAML changes needed to the image element.

**Alternatives considered**:
- 24×24 source → too small to show any identifiable content at rendered size
- 96×96 source → diminishing returns on detail, 4× memory cost (36KB each, 50 thumbnails = 1.8MB)
- Variable size per slot → adds complexity without proportional benefit

### D6: Animation origin tracked via `RadialMenuViewModel._parentSlotPosition`

**Decision**: Store the parent slot's canvas (X, Y) position at the moment of sub-menu entry in a `(double X, double Y)` tuple on `RadialMenuViewModel`. Use this as the animation origin for all child slots.

**Rationale**: The parent slot's canvas position is known at click time (it's the `SlotViewModel.X`/`Y` values). Storing it avoids recomputing and is immune to layout changes during the animation. One origin for all children (not per-child) keeps the animation simple: all slots burst from the parent's location.

**Alternatives considered**:
- Per-child origin = each child's final position → no visual benefit, same effect as no animation
- Center-relative origin → same as current behavior, defeats the purpose
- Parent slot position projected to ring radius → adds complexity without meaningful UX improvement

## Risks / Trade-offs

- **[Risk] Thumbnail capture blocks for minimized windows** → CaptureAsync returns null quickly for iconic windows; fallback to app icon + color coding handles this gracefully. No user-visible error.
- **[Risk] 8-color palette may not be enough for edge case** → Cycling at index 8 means nth window's color matches index 0. In practice, 5+ visible windows from the same process is extremely rare. If it becomes an issue, the palette can be extended without API changes.
- **[Risk] Animation may feel sluggish on low-end hardware** → The 250ms duration with elastic easing is standard for WPF; if profiling shows jank, the easing can be simplified to `CubicEase` without spec changes.
- **[Trade-off] Eager capture increases CPU/GPU work on sub-menu entry** → Acceptable because sub-menu entry is infrequent (user explicitly clicks to enter) and captures run in parallel on thread pool.
- **[Trade-off] No thumbnail disk persistence** → Cache is purely in-memory. App restart clears all thumbnails. Disk caching would add complexity (file I/O, disk space management) for marginal benefit given capture speed (~100ms).
