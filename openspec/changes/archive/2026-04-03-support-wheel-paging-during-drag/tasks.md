## 1. Define Global Wheel Input Path

- [x] 1.1 Create a native or service-level global mouse-wheel input component aligned with the existing global keyboard hook architecture
- [x] 1.2 Register the new wheel input component in dependency injection and application startup
- [x] 1.3 Expose wheel delta notifications in a form the radial menu runtime can subscribe to without embedding menu logic in the native hook layer

## 2. Integrate Wheel Paging With Radial Menu Session State

- [x] 2.1 Route global wheel deltas to the radial menu only while Pulsar is visible
- [x] 2.2 Keep paging eligibility checks in the application layer so root-only paging, page-provider availability, and mode rules remain centralized
- [x] 2.3 Decide and implement whether single-page and boundary-feedback wheel gestures should mark the wheel event handled
- [x] 2.4 Preserve existing page-direction semantics and ensure handled wheel gestures do not trigger duplicate page changes

## 3. Reconcile Global And Window-Local Wheel Handling

- [x] 3.1 Verify whether `RadialMenuWindow.PreviewMouseWheel` duplicates paging once the global wheel path is active
- [x] 3.2 Remove or suppress the window-local wheel path if duplicate paging occurs in normal non-drag sessions
- [x] 3.3 Keep ordinary non-drag wheel paging behavior unchanged from the user's perspective

## 4. Validate Drag And Non-Drag Interaction Scenarios

- [x] 4.1 Build the application and verify the new input component does not break startup or shutdown
- [ ] 4.2 Manually verify `Ctrl+Q` invocation during active file drag now supports wheel paging in switcher mode
- [ ] 4.3 Manually verify multi-page action mode also pages correctly during an active drag session
- [ ] 4.4 Manually verify hidden, submenu, single-page, and boundary cases behave according to spec without stealing unrelated system scrolling
- [ ] 4.5 Manually verify ordinary non-drag invocation still pages exactly once per wheel gesture
