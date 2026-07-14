## Why

The sub-radial menu (Task/Switcher mode, multi-window process groups) suffers from two UX deficiencies: (1) entering a sub-menu feels disorienting because the entire ring is abruptly replaced with no spatial connection to the parent slot that was clicked, and (2) slots for multiple windows of the same process all display identical app icons, making individual windows visually indistinguishable. This change addresses both issues by adding parent-to-child transition animations and introducing window-specific visual identity (thumbnails + color coding).

## What Changes

- **Sub-menu expansion animation**: When entering a sub-menu, child slots animate radially outward from the parent slot's position to their final ring positions, establishing a clear spatial relationship between parent and children. The center slot transitions to show the parent process name as a breadcrumb. The reverse animation plays when returning to the root menu.
- **Window thumbnail capture & caching**: A per-hWnd thumbnail cache captures window screenshots at sub-menu entry time and stores them at 48×48 resolution. Cached thumbnails replace the generic app icon on sub-menu slots. Thumbnails are invalidated when the window closes or its title changes.
- **Color-coded window differentiation**: Same-process windows in the sub-menu receive stable, cycle-assigned color tokens applied to the slot border/stroke, providing immediate visual differentiation even when thumbnails are unavailable or loading.

## Capabilities

### New Capabilities
- `submenu-expansion-animation`: Spatial transition animation when entering/exiting sub-menus, animating slots from/to the parent slot's position.
- `submenu-slot-thumbnail`: Per-window thumbnail capture, in-memory caching (ConcurrentDictionary keyed by hWnd), and display on sub-menu slots with graceful fallback to app icons.
- `submenu-window-color-coding`: Deterministic color assignment for same-process windows based on stable sort order, applied as slot border/stroke tokens.

### Modified Capabilities
- `sub-radial-window-title`: Slot label behavior unchanged, but slot visual identity is now augmented by thumbnail and color coding alongside the existing title label.

## Impact

- **Affected code**:
  - `RadialMenuViewModel.EnterSubMenuAsync()` / `RestoreRootMenu()` — add animation origin/destination parameter (parent slot position)
  - `RadialMenuSubMenuCoordinator.ConfigureSubMenu()` — wire thumbnail loading + color assignment
  - `SlotViewModel` — add `ThumbnailImage` property, merge into icon display priority
  - `SlotOrb.xaml` / `SlotOrb.xaml.cs` — integrate color stroke binding from sub-menu context
  - New service: `ISubMenuThumbnailCache` + implementation — thumbnail capture, caching, invalidation
  - `ProcessWindowInfo` — no changes (hWnd already available, capture happens on-demand)
- **Dependencies**: Reuses existing `IWindowService.CaptureWindowAsync()` for thumbnail capture, existing `PreviewService` cache strategy as reference pattern
- **No breaking changes**: All existing ring behavior, slot layout, and interaction remain intact
