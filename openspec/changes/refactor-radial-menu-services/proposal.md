## Why

The `RadialMenuViewModel` has grown to 1287 lines with over 10 distinct responsibilities including mouse tracking, animation, layout calculation, paging, mode switching, plugin execution, preview capture, and quick switch logic. This violates Single Responsibility Principle and makes the code difficult to test, maintain, and extend. Additionally, the animation system uses three separate mechanisms (DispatcherTimer, async/await Task.Delay, and XAML Storyboards) that are difficult to coordinate and cannot be paused/resumed or composed.

## What Changes

- **Extract services from RadialMenuViewModel**: Split monolithic ViewModel into focused services (IMouseTrackingService, IAnimationController, ISlotLayoutEngine, IPagingController, IPreviewService)
- **Unified animation system**: Replace three animation mechanisms with a single AnimationController using task-based composition
- **Improved testability**: All extracted services are mockable, enabling unit testing without UI dependencies
- **Reduced ViewModel complexity**: RadialMenuViewModel reduced from ~1287 lines to ~400 lines

## Capabilities

### New Capabilities
- `mouse-tracking-service`: Decoupled mouse position tracking with dead zone detection and hit testing
- `animation-controller`: Unified animation system replacing DispatcherTimer, async animations, and XAML Storyboards
- `slot-layout-engine`: Separated layout calculation from ViewModel state management

### Modified Capabilities
- (none - this is a refactoring with no change to external behavior or requirements)

## Impact

**Affected Code:**
- `ViewModels/RadialMenuViewModel.cs` - Will be significantly simplified
- `ViewModels/SlotViewModel.cs` - Will be simplified to only hold state, not animation logic
- `Helpers/RadialLayoutHelper.cs` - Logic moved to ISlotLayoutEngine

**New Files:**
- `Services/Interfaces/IAnimationController.cs`
- `Services/Interfaces/IMouseTrackingService.cs`
- `Services/Interfaces/ISlotLayoutEngine.cs`
- `Services/Interfaces/IPagingController.cs`
- `Services/Interfaces/IPreviewService.cs`
- `Services/AnimationController.cs`
- `Services/MouseTrackingService.cs`
- `Services/SlotLayoutEngine.cs`
- `Services/PagingController.cs`
- `Services/PreviewService.cs`

**No breaking changes** - All public APIs remain the same; this is purely internal refactoring.
