## Context

The window switching system in Pulsar has evolved through multiple iterations, resulting in scattered Win32 API definitions and mixed responsibilities. Currently:

- **Win32 API duplication**: Native methods defined in 4 locations (PulsarContext.cs L200-204, WindowService.cs L180-204 and L1294-1333, WindowActivationMonitor.cs, WindowHelper.cs)
- **WindowService bloat**: 1335-line class handling 8 distinct responsibilities (window enumeration, QuickSwitch state, focus restore, icon extraction, window registry, window capture, blacklist management, native P/Invoke)
- **ProcessPageProvider mixing**: Data loading logic (window enumeration, grouping, config matching) conflated with UI binding (ObservableCollection manipulation)
- **Maintenance risk**: Duplicate APIs may behave inconsistently across Windows versions

Stakeholders: Core development team maintaining window switching functionality.

## Goals / Non-Goals

**Goals:**
- Create single source of truth for Win32 API definitions (`PulsarNative.cs`)
- Eliminate duplicate Win32 API declarations across 4 locations
- Improve code organization in WindowService with region blocks
- Separate data loading from UI binding in ProcessPageProvider
- Preserve all existing behaviors (no functional changes)

**Non-Goals:**
- No new window switching features
- No changes to public API contracts (IWindowService interface unchanged)
- No database or configuration schema changes
- No dependency additions or removals

## Decisions

### D1: New PulsarNative class as unified entry point

**Decision**: Create `Pulsar/Pulsar/Native/PulsarNative.cs` containing all Win32 API definitions consolidated into one location.

**Rationale**: Eliminates the "silent bomb" of duplicate definitions. Provides single point for API updates and security fixes. The reference-counting logic for foreground lock (originally in WindowHelper) belongs here as it's a system-level resource.

**Alternatives considered**:
- Keep existing distribution: Rejected due to maintenance burden and inconsistency risk
- Move all to existing WindowHelper: Rejected - WindowHelper has unrelated methods, better to create clean slate
- Use wrapping facade pattern: Over-engineered for this use case

### D2: SetForegroundWindow reference counting in PulsarNative

**Decision**: Include the foreground lock reference counting logic within PulsarNative.SetForegroundWindow rather than exposing raw API.

**Rationale**: The original implementation in WindowHelper had static state management that must be preserved for correct behavior. Encapsulating it ensures all callers get correct behavior without managing state externally.

**Alternatives considered**:
- Keep reference counting in WindowHelper: Creates dependency that defeats consolidation goal
- Move to WindowService: Violates single responsibility - WindowService shouldn't manage system-level locks

### D3: ProcessWindowMatcher as separate class

**Decision**: Extract data matching logic into `ViewModels/Strategies/ProcessWindowMatcher.cs`.

**Rationale**: Enables unit testing of matching logic in isolation. Follows single responsibility principle - ProcessPageProvider handles UI binding, matcher handles data transformation.

**Alternatives considered**:
- Keep in ProcessPageProvider: Maintains status quo of mixed responsibilities
- Use static utility class: Less testable, harder to inject dependencies if needed later

### D4: Region organization in WindowService

**Decision**: Add #region blocks around existing code without restructuring into separate classes.

**Rationale**: External callers don't need to mock internal modules. Regions provide visual organization without API changes. Pure reorganization - zero risk of behavioral changes.

## Risks / Trade-offs

### R1: Behavioral regression in foreground window switching

**Risk**: Phase 2 changes to `ForceForegroundWindow` → `PulsarNative.SetForegroundWindow` may alter timing/behavior.

**Mitigation**: Compare implementation thoroughly. The SetForegroundWindow in PulsarNative includes all the original logic (AllowSetForegroundWindow, LockSetForegroundWindow, keybd_event, fallback to BringWindowToTop). Run comprehensive functional tests after change.

### R2: Accumulative large diff in WindowService

**Risk**: Phase 2 (API replacement) + Phase 4 (region reorganization) create massive git diff making review difficult.

**Mitigation**: Execute Phase 2 and verify with build + functional tests. Then Phase 4 as separate git commit for clean code review.

### R3: Icon extraction breakage

**Risk**: SHGetFileInfo and related APIs are used in icon extraction. If incorrectly migrated, icons won't display.

**Mitigation**: Keep SHGetFileInfo in WindowService as local code (not in PulsarNative) - it's icon-specific, not window core. Document this decision clearly.

## Migration Plan

1. Phase 0: Create PulsarNative.cs, verify build passes
2. Phase 1: Refactor WindowActivationMonitor, verify build + test
3. Phase 2: Refactor WindowService (2A, 2B, 2C), verify build + test all window switching scenarios
4. Phase 3: Refactor PulsarContext, verify build
5. Phase 4: Add regions to WindowService (separate commit)
6. Phase 5: Create ProcessWindowMatcher, update ProcessPageProvider, verify build + test

**Rollback**: Each phase can be reverted by reverting git commit. No database migrations needed.

## Open Questions

- None at this time - the refactoring document provides sufficient detail for all phases.