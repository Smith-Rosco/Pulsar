## Context

The `RadialMenuViewModel` (1287 lines) handles multiple distinct concerns: mouse tracking, layout animation, paging, mode switching, plugin execution, preview capture, and quick switch. The animation system uses three different mechanisms that cannot be composed: DispatcherTimer (layout), async/await Task.Delay (bounce), and XAML Storyboards (UI triggers).

This design addresses extracting these responsibilities into focused services while maintaining identical external behavior.

## Goals / Non-Goals

**Goals:**
- Extract services with clear interfaces enabling dependency injection and mocking
- Create unified animation system replacing three separate mechanisms
- Reduce RadialMenuViewModel from ~1287 lines to ~400 lines
- Enable unit testing of animation and layout logic without UI dependencies
- Maintain 60 FPS animation performance

**Non-Goals:**
- No change to external API or user-facing behavior
- No changes to XAML views (SlotOrb, RadialMenuWindow)
- No new features or capability changes

## Decisions

### 1. Service Extraction Strategy

**Decision:** Extract 5 services from RadialMenuViewModel

| Service | Responsibility | Interface |
|---------|---------------|-----------|
| `IMouseTrackingService` | Global mouse position, dead zone, hit testing | Publish position stream |
| `IAnimationController` | Unified lerp/bounce/magnetism animations | Task-based composition |
| `ISlotLayoutEngine` | Layout calculations (position, radius, density) | Pure calculation |
| `IPagingController` | Page navigation and boundary handling | Next/Prev/GoTo |
| `IPreviewService` | Window capture and caching | Capture with cache |

**Rationale:** Each service has a single, well-defined responsibility. Interfaces enable mocking for tests.

**Alternatives Considered:**
- Extract fewer, larger services (e.g., "InputService" combining mouse + paging) → Defeats testability goal
- Use events instead of interfaces → Loses type safety and IntelliSense

### 2. Animation Controller Architecture

**Decision:** Use task-based animation with `IAnimationController` instead of DispatcherTimer

```csharp
public interface IAnimationController
{
    Task AnimateLayoutAsync(LayoutTarget target, AnimationOptions? options = null);
    Task BounceAsync(BounceDirection direction, CancellationToken ct = default);
    void UpdateMagnetism(Vector2 cursorPosition);
    void Pause();
    void Resume();
    Task QueueAsync(Func<CancellationToken, Task> animation);
}
```

**Rationale:** 
- Task-based allows `await` for composition and cancellation
- Queue enables sequential animation composition (bounce then expand)
- Pause/Resume supported (Timer-based approach had no pause)

**Alternatives Considered:**
- Keep DispatcherTimer → Cannot compose animations, no pause/resume
- Use Reactive Extensions (Rx) → Adds dependency, steeper learning curve

### 3. Animation Loop Strategy

**Decision:** Use `CompositionTarget.Rendering` event for animation timing

```csharp
CompositionTarget.Rendering += OnRender;

private void OnRender(object? sender, EventArgs e)
{
    if (_isPaused) return;
    var elapsed = DateTime.Now - _startTime;
    var t = _easing(elapsed.TotalMilliseconds / _duration.TotalMilliseconds);
    ApplyInterpolation(t);
}
```

**Rationale:**
- Synchronized with WPF render cycle (~60 FPS)
- Pauses automatically when window not visible
- More efficient than DispatcherTimer

**Alternatives Considered:**
- `DispatcherTimer` at 16ms intervals → Less precise, runs even when hidden
- `requestAnimationFrame` → Not available in WPF, requires interop

### 3a. Animation Controller Integration Contract

**Decision:** `IAnimationController` must expose the configuration surface needed to bind animation output back to the radial menu state, rather than relying on concrete `AnimationController` methods only available on the implementation type.

The current implementation already contains callback-based integration points such as layout updates, bounce updates, magnetism updates, and slot target registration, but they are not part of the interface contract. That makes the service difficult to consume through DI during the `RadialMenuViewModel` migration because the ViewModel can inject `IAnimationController` but cannot legally configure it.

**Required contract additions:**

```csharp
void SetLayoutUpdateCallback(Action<LayoutTarget> callback);
void SetBounceUpdateCallback(Action<double> callback);
void SetMagnetismUpdateCallback(Action<Vector, IList<SlotAnimationTarget>> callback);
void SetSlotTargets(IList<SlotAnimationTarget> targets);
```

**Rationale:**
- Preserves DI-friendly usage through the interface
- Avoids leaking implementation-specific casts into `RadialMenuViewModel`
- Makes the service extraction complete rather than nominal
- Creates an explicit seam for tests to verify animation wiring

**Alternatives Considered:**
- Cast `IAnimationController` to `AnimationController` in the ViewModel → Couples ViewModel to concrete type and undermines extraction
- Keep animation state updates in the ViewModel while only delegating timing → Leaves the hardest responsibility in the ViewModel and weakens the refactor
- Push callbacks into a separate adapter service → Extra indirection without solving the missing contract problem

### 4. Mouse Tracking Architecture

**Decision:** MouseTrackingService subscribes to global cursor position via composition target

```csharp
public interface IMouseTrackingService
{
    IObservable<Vector2> MousePosition { get; }
    Vector2 RelativePosition { get; }
    bool IsInDeadZone { get; }
    int HoveredSlotIndex { get; }
    void StartTracking();
    void StopTracking();
}
```

Uses `IWindowService` for screen-to-local coordinate conversion.

**Rationale:** Decouples input handling from ViewModel, enables replay/logging of mouse paths.

### 5. SlotLayoutEngine as Stateless Helper

**Decision:** ISlotLayoutEngine is a stateless interface wrapping RadialLayoutHelper

```csharp
public interface ISlotLayoutEngine
{
    LayoutParameters CalculateOptimalLayout(int slotCount);
    Vector2 GetSlotPosition(int index, int totalSlots, LayoutParameters p);
    int HitTest(Vector2 point, LayoutParameters p);
}
```

**Rationale:** Layout logic already well-tested in RadialLayoutHelper. This preserves existing calculation behavior while making it injectable.

## Risks / Trade-offs

| Risk | Impact | Mitigation |
|------|--------|------------|
| Task.Yield() animation jitter | Animation may stutter | Benchmark test, fallback to CompositionTarget.Rendering if needed |
| Breaking existing animations | Subtle UX changes | Create AnimationTests with screenshot comparison |
| Too many services | Architectural over-engineering | Only extract if service has clear boundary; evaluate after |
| Circular dependency (MouseTracking → WindowService → ...) | Build breaks | Define interfaces before implementations |

## Migration Plan

**Phase 1: Create Service Interfaces**
1. Create `Services/Interfaces/` with all 5 interfaces
2. No implementation, no behavioral change
3. Verify build succeeds

**Phase 2: Implement AnimationController**
1. Implement `AnimationController` with lerp animations
2. Replace `UpdateLayoutAnimation()` timer with AnimationController
3. Verify 60 FPS maintained
4. Implement bounce animation

**Phase 3: Implement MouseTrackingService**
1. Implement `MouseTrackingService`
2. Route mouse events through service
3. Remove direct mouse handling from ViewModel

**Phase 4: Implement Remaining Services**
1. SlotLayoutEngine (wraps RadialLayoutHelper)
2. PagingController
3. PreviewService

**Phase 5: Simplify ViewModel**
1. Remove extracted code
2. Wire up services via DI
3. Run all integration tests

## Open Questions

1. **Preview Cancellation**: Current implementation uses `_previewCts.Cancel()`. Should AnimationController own cancellation tokens or should each service manage its own?
   - *Tentative answer*: Each service manages its own CTS, passed via `CancellationToken` parameter.

2. **Service Lifetime**: Should these services be Singleton or Transient?
   - *Tentative answer*: Singleton for AnimationController (stateful), Transient for stateless helpers.

3. **Backwards Compatibility**: SlotViewModel.UpdateMagneticOffset() is called by RadialMenuViewModel. Should SlotViewModel call IAnimationController directly?
   - *Tentative answer*: No - SlotViewModel remains passive (data only). AnimationController drives property changes.

4. **Interface Completeness**: Should the callback/target registration methods live on `IAnimationController` or a dedicated companion interface?
   - *Current direction*: Keep them on `IAnimationController` for now because the change goal is a small internal refactor, not a broader animation architecture split.
