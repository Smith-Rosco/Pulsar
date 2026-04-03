## Context

Pulsar currently pages the radial menu by subscribing to `RadialMenuWindow.PreviewMouseWheel` and forwarding the wheel delta to `RadialMenuViewModel.HandleMouseWheel`. That works when the overlay window participates in the normal WPF input route, but it fails during Windows file drag-and-drop because the drag is owned by the system OLE drag loop rather than by Pulsar's window message pipeline.

At the same time, `Ctrl+Q` still summons Pulsar during the drag because hotkey activation is implemented with a global low-level keyboard hook. The mismatch is architectural: menu activation is global, but paging is local to the overlay window. The result is an inconsistent session in which the menu appears but cannot be paged with the wheel while the left mouse button remains held on a dragged file.

This change is cross-cutting because it touches native input capture, runtime interaction policy, and radial-menu behavior. It benefits from a design document so implementation can preserve existing semantics while avoiding accidental input theft from the host application.

## Goals / Non-Goals

**Goals:**
- Keep wheel paging available when Pulsar is visible during an active drag session.
- Unify activation and paging around a global-input model instead of relying on window-local wheel delivery.
- Preserve existing paging rules: only page at the root menu, show single-page feedback, and keep boundary behavior intact.
- Prevent Pulsar from consuming global wheel input when the menu is hidden or otherwise not eligible to page.
- Keep the implementation small and aligned with the existing `GlobalKeyboardHook` pattern.

**Non-Goals:**
- No change to slot layout, page ordering, or switcher content.
- No attempt to make the radial menu a drag-and-drop target for external files.
- No redesign of quick-switch, left-click execution, or hover tracking.
- No broad input abstraction refactor beyond what is required to route wheel input correctly.

## Decisions

### 1. Add a global mouse-wheel input path

**Decision:** Introduce a global mouse input component that listens for `WM_MOUSEWHEEL` independently of `RadialMenuWindow` focus and forwards wheel deltas to the radial-menu session only while Pulsar is visible.

**Rationale:**
- The current failure occurs because the overlay window does not reliably receive WPF mouse-wheel events during OLE drag-and-drop.
- A low-level global path matches the existing hotkey architecture, which already succeeds during drag sessions.
- This fixes the problem at the correct layer instead of trying to coerce WPF focus, activation, or mouse capture to behave differently during system-managed drag loops.

**Alternatives Considered:**
- Keep relying on `PreviewMouseWheel` and try to force focus or topmost behavior: rejected because the issue is input routing, not z-order.
- Add an `HwndSourceHook` to `RadialMenuWindow`: rejected because this is still window-local and does not solve missing wheel delivery during drag sessions.
- Add keyboard-only page navigation as the primary fix: rejected because it papers over the regression instead of restoring the wheel workflow users already expect.

### 2. Consume global wheel input only during an active Pulsar session

**Decision:** The new global wheel path SHALL be gated by radial-menu visibility and eligibility checks before it triggers paging or marks the wheel event handled.

**Eligibility rules:**
- Pulsar is visible.
- The menu is at the root level where paging is valid.
- A page provider exists.
- The wheel delta results in a valid paging action or an intentional boundary/single-page feedback action.

**Rationale:**
- Prevents unintended interference with normal application scrolling when Pulsar is not actively in use.
- Preserves the principle that Pulsar only owns transient global input during an active overlay session.

**Alternatives Considered:**
- Consume all wheel input while Pulsar is visible: rejected because it would be too invasive, especially on single-page menus or submenus.
- Never consume the wheel and only mirror paging opportunistically: rejected because host applications could scroll underneath the overlay, producing confusing double behavior.

### 3. Keep paging policy in the ViewModel, not in the hook

**Decision:** The native/global input layer only detects and forwards wheel deltas. Paging eligibility, single-page hint behavior, and boundary feedback remain in `RadialMenuViewModel` or a closely related application-layer service.

**Rationale:**
- Keeps policy close to existing menu state and page-provider logic.
- Makes the input layer simpler and less stateful.
- Reduces the risk of duplicating paging rules across native and managed layers.

**Alternatives Considered:**
- Move all paging rules into the hook service: rejected because native input code should not own menu business logic.

### 4. Preserve the existing window-local wheel path as a compatibility fallback unless it becomes redundant

**Decision:** During implementation, keep the current `PreviewMouseWheel` path unless testing shows it causes duplicate paging once the global wheel path is active.

**Rationale:**
- It minimizes change for the normal non-drag case.
- It allows implementation to add the missing drag-session path first, then simplify only if duplication is observed.

**Alternatives Considered:**
- Remove the WPF wheel path immediately: rejected because it expands change scope without first validating whether the global path fully covers normal operation.

## Risks / Trade-offs

- [Global wheel hook consumes input too aggressively] -> Mitigate with strict visibility and eligibility gating, plus tests for hidden, submenu, and single-page states.
- [Duplicate paging in normal sessions because both WPF and global paths fire] -> Mitigate by instrumenting wheel handling and disabling the window-local path if duplicate deltas are observed.
- [High-resolution wheels emit many deltas and skip pages too quickly] -> Mitigate by preserving or adding delta accumulation / throttling behavior near the paging entry point.
- [Hook lifecycle bugs leave the hook active after the menu is dismissed] -> Mitigate by tying subscription lifetime to application services and ensuring dismiss paths clear session-owned handling state.
- [Global input code increases native complexity] -> Mitigate by mirroring the small, focused structure already used by `GlobalKeyboardHook` rather than introducing a broad input framework.

## Migration Plan

1. Introduce the global mouse-wheel input component and register it in DI without changing paging behavior yet.
2. Route wheel deltas into the radial-menu paging path behind visibility and eligibility checks.
3. Validate three interaction classes:
   - normal invocation with no drag
   - invocation while dragging a file with left mouse button held
   - hidden / submenu / single-page sessions where Pulsar must not over-consume wheel input
4. If duplicate paging occurs in normal sessions, remove or suppress the `PreviewMouseWheel` path.
5. Ship with focused regression coverage around drag-session paging and ordinary wheel behavior.

## Open Questions

1. Should single-page wheel gestures be consumed when Pulsar is visible so the hint can appear without underlying window scrolling?
   - Tentative direction: yes, if Pulsar intentionally displays single-page feedback for that gesture.

2. Should boundary bounce gestures also consume the wheel event?
   - Tentative direction: yes, because the gesture is intentionally handled by Pulsar even when no page changes.

3. Is `WH_MOUSE_LL` the best fit, or is there an existing lower-risk abstraction in the codebase worth extending?
   - Tentative direction: use a dedicated low-level mouse hook patterned after `GlobalKeyboardHook`, because no existing global mouse infrastructure was found.
