## 1. Shared Selection Core

- [x] 1.1 Define the shared window-selection abstraction and context model inside the existing window switching layer
- [x] 1.2 Refactor candidate ranking to use real activation recency as the canonical signal for user-facing switching decisions
- [x] 1.3 Implement deterministic fallback handling for candidate windows that do not yet have tracked activation history
- [x] 1.4 Encode explicit skip rules in the shared selection path for current-foreground and pre-invocation window cases

## 2. Wire High-Value Switching Paths

- [x] 2.1 Update `WindowService.SwitchToProcessAsync` to delegate target selection to the shared selection core
- [x] 2.2 Update grouped radial-menu process switching to delegate target selection to the same shared selection core
- [x] 2.3 Remove or reduce duplicated per-caller selection logic once both paths use the shared selection core
- [x] 2.4 Verify WinSwitcher `activate`, `launch`, and `switch` actions preserve their documented semantics after the selection refactor

## 3. Shared Activation Path

- [x] 3.1 Introduce or expose a single service-level activation path for concrete window targets
- [x] 3.2 Route direct window-slot activation through the shared activation path instead of UI strategy-specific native calls
- [x] 3.3 Ensure minimized targets are restored before foreground activation and invalid handles fail predictably
- [x] 3.4 Align logging and success/failure reporting with the shared activation path

## 4. Regression Coverage And Validation

- [x] 4.1 Add focused tests for multi-window target selection using real activation recency
- [x] 4.2 Add focused tests for skip-rule behavior across plugin-driven and menu-driven switching contexts
- [ ] 4.3 Add focused tests for shared activation behavior with minimized and invalid targets
- [ ] 4.4 Run targeted manual validation against common multi-window apps and document any follow-up work that should remain out of scope for this change
