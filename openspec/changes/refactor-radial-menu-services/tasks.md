## 1. Create Service Interfaces

- [x] 1.1 Create `Services/Interfaces/IAnimationController.cs` with AnimateLayoutAsync, BounceAsync, UpdateMagnetism, Pause, Resume, QueueAsync methods
- [x] 1.2 Create `Services/Interfaces/IMouseTrackingService.cs` with MousePosition observable, RelativePosition, IsInDeadZone, HoveredSlotIndex, StartTracking, StopTracking
- [x] 1.3 Create `Services/Interfaces/ISlotLayoutEngine.cs` with CalculateOptimalLayout, GetSlotPosition, HitTest
- [x] 1.4 Create `Services/Interfaces/IPagingController.cs` with NextPageAsync, PrevPageAsync, GoToPageAsync, CurrentPage, TotalPages, OnBoundaryReached event
- [x] 1.5 Create `Services/Interfaces/IPreviewService.cs` with CaptureAsync, InvalidateCache, ClearCache

## 2. Implement AnimationController

- [x] 2.1 Create `Services/AnimationController.cs` implementing IAnimationController
- [x] 2.2 Implement LayoutTarget and AnimationOptions DTOs
- [x] 2.3 Implement AnimateLayoutAsync with CompositionTarget.Rendering and easing functions
- [x] 2.4 Implement BounceAsync with compress/elastic phases
- [x] 2.5 Implement UpdateMagnetism with lerp smoothing
- [x] 2.6 Implement Pause/Resume lifecycle methods
- [x] 2.7 Implement QueueAsync for sequential animations
- [x] 2.8 Verify build succeeds

## 3. Implement MouseTrackingService

- [x] 3.1 Create `Services/MouseTrackingService.cs` implementing IMouseTrackingService
- [x] 3.2 Implement StartTracking/StopTracking with CompositionTarget.Rendering subscription
- [x] 3.3 Implement relative position calculation from screen coordinates
- [x] 3.4 Implement dead zone detection using ISlotLayoutEngine
- [x] 3.5 Implement hit testing using ISlotLayoutEngine.HitTest
- [x] 3.6 Integrate with IWindowService for coordinate conversion
- [x] 3.7 Verify build succeeds

## 4. Implement SlotLayoutEngine

- [x] 4.1 Create `Services/SlotLayoutEngine.cs` implementing ISlotLayoutEngine
- [x] 4.2 Port CalculateOptimalRadius from RadialLayoutHelper
- [x] 4.3 Port CalculateOptimalSlotSize from RadialLayoutHelper
- [x] 4.4 Port CalculateOptimalCenterSize from RadialLayoutHelper
- [x] 4.5 Port GetSlotPosition from RadialLayoutHelper
- [x] 4.6 Port HitTest from RadialLayoutHelper
- [x] 4.7 Port CalculateDeadZoneRatio from RadialLayoutHelper
- [x] 4.8 Port CalculateVisualDensity from RadialLayoutHelper
- [x] 4.9 Verify build succeeds

## 5. Implement PagingController

- [x] 5.1 Create `Services/PagingController.cs` implementing IPagingController
- [x] 5.2 Implement page navigation methods (Next, Prev, GoTo)
- [x] 5.3 Implement boundary detection and OnBoundaryReached event
- [x] 5.4 Wire up IAnimationController for bounce animations on boundary
- [x] 5.5 Verify build succeeds

## 6. Implement PreviewService

- [ ] 6.1 Create `Services/PreviewService.cs` implementing IPreviewService
- [ ] 6.2 Implement CaptureAsync with IWindowService.CaptureWindowAsync
- [ ] 6.3 Implement cache dictionary with IntPtr keys
- [ ] 6.4 Implement InvalidateCache and ClearCache
- [ ] 6.5 Verify build succeeds

## 7. Register Services in DI

- [ ] 7.1 Register all new services in App.xaml.cs ConfigureServices
- [ ] 7.2 Update RadialMenuViewModel constructor to accept new services
- [ ] 7.3 Verify build succeeds

## 8. Refactor RadialMenuViewModel

- [ ] 8.1 Remove animation timer and UpdateLayoutAnimation method
- [ ] 8.2 Remove mouse tracking fields and HandleMouseMove logic
- [ ] 8.3 Remove magnetism calculation logic
- [ ] 8.4 Remove bounce animation methods (AnimateBounce, ShowTemporaryHint, ShowSinglePageHint)
- [ ] 8.5 Remove layout calculation fields (CanvasSize, CenterX, CenterY, RadiusNormal, etc.)
- [ ] 8.6 Remove layout helper calls - use ISlotLayoutEngine instead
- [ ] 8.7 Wire up IAnimationController for all animations
- [ ] 8.8 Wire up IMouseTrackingService for mouse input
- [ ] 8.9 Wire up IPagingController for page navigation
- [ ] 8.10 Wire up IPreviewService for window captures
- [ ] 8.11 Verify RadialMenuViewModel is reduced to ~400 lines
- [ ] 8.12 Verify build succeeds

## 9. Simplify SlotViewModel

- [ ] 9.1 Remove magnetism-related fields (_currentMagneticOffsetX, _currentMagneticOffsetY)
- [ ] 9.2 Keep UpdateMagneticOffset method but have it called by AnimationController
- [ ] 9.3 Keep ResetAnimation method
- [ ] 9.4 Verify build succeeds

## 10. Testing

- [ ] 10.1 Run full build: `dotnet build Pulsar/Pulsar/Pulsar.csproj`
- [ ] 10.2 Run application and verify radial menu displays correctly
- [ ] 10.3 Test mouse hover - slots should activate with magnetism effect
- [ ] 10.4 Test mouse wheel - pagination should work with bounce at boundaries
- [ ] 10.5 Test Ctrl+Space to dismiss - verify animation is smooth
- [ ] 10.6 Test SubMenu navigation - verify layout expansion animation
