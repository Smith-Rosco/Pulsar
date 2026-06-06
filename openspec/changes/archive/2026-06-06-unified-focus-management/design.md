## Context

Pulsar's focus management currently spans 6 independent components with no central coordination:

```
PulsarNative (static)          WindowService.FocusRestore()    WindowActivator
├─ SetForegroundWindow()       ├─ ForceForegroundWindow()       └─ ActivateWindow()
├─ _fgLockCount (static)       ├─ RestoreFocus() state machine
├─ keybd_event(VK_MENU) hack   └─ WindowActivationMonitor
└─ EmergencyRestore()
                               
PKI Focus Chain                Plugins (direct calls)          GlobalKeyboardHook
├─ IFocusRestorer              ├─ VbaRunnerPlugin (2 sites)    └─ _trackedAltDown
├─ IWindowFocusSimulator       ├─ ScriptEngine (1 site)           ← corrupted by
└─ SendKeysInjectionExecutor   └─ BookmarkletRunner (1 site)      keybd_event
```

All 7 call sites converge on `PulsarNative.SetForegroundWindow()` — a static method with static state (`_fgLockCount`, `_originalTimeout`) that is impossible to unit-test. The `keybd_event(VK_MENU)` workaround in `SetForegroundWindowInternal` injects a fake Alt-release into the system input stream, which the low-level keyboard hook interprets as a real event and uses to update its internal `_trackedAltDown` flag — corrupting modifier state tracking.

Meanwhile, the PKI subsystem has its own parallel focus infrastructure (`IFocusRestorer` → `WindowsFocusRestorer` → `IWindowFocusSimulator` → `WindowsFocusSimulator`), duplicating the same `PulsarNative.SetForegroundWindow` call but without post-activation verification — credentials can be injected into whichever window happens to have focus if the activation silently fails.

The radial menu window (`RadialMenuWindow`) calls `RestoreFocus()` immediately when `Dismiss()` begins, before the fade-out animation completes. Since the window is `Topmost=True`, the target window receives foreground activation but remains visually occluded by the still-visible menu for ~100ms.

## Goals / Non-Goals

**Goals:**
- Create a single, injectable `IFocusManager` service that is the sole authority for all foreground window operations
- Abstract all Win32 focus P/Invoke behind `IFocusNativeAdapter` for testability
- Replace the `keybd_event(VK_MENU)` hack with `AttachThreadInput` + modifier tracker coordination
- Add post-activation focus verification for PKI credential injection safety
- Fix the radial menu animation/focus-release timing race
- Migrate all 7 `SetForegroundWindow` call sites to use `IFocusManager`
- Remove the obsolete `WindowHelper` class and the redundant PKI focus interfaces
- Maintain full backward compatibility for end-user behavior

**Non-Goals:**
- Changing the `WH_KEYBOARD_LL` hook to `RegisterHotKey` (separate change)
- Refactoring `GlobalKeyboardHook` beyond adding `IModifierStateTracker`
- Changing the WindowService window enumeration or icon extraction logic
- Modifying the PulsarContext capture mechanism (stays as-is)
- Adding focus management for the Settings window or dialog system
- Changing the radial menu's Topmost strategy or ghost mode (resident mode)

## Decisions

### Decision 1: `IFocusManager` as sole SetForegroundWindow authority

**Choice**: All code paths that need to bring a window to foreground MUST go through `IFocusManager`. Direct `PulsarNative.SetForegroundWindow` calls in plugins are disallowed.

**Rationale**: The Ports & Adapters pattern has been successfully applied to input simulation (`IInputSimulator`), secret storage (`IPkiSecretStore`), and dialog display (`IDialogService`). Focus management is the last remaining area where side-effecting OS calls are scattered and untestable. Applying the same pattern creates consistency and testability.

**Alternatives considered**:
- **Keep SetForegroundWindow in PulsarNative but make it non-static**: Still doesn't solve the coordination problem (hook sync, verification, centralized policy)
- **Extend WindowService with more focus methods**: WindowService already has 951 lines and mixed responsibilities (enumeration, icons, capture, focus). Adding more would violate Single Responsibility

### Decision 2: `AttachThreadInput` replaces `keybd_event(VK_MENU)` as the primary focus-stealing mechanism

**Choice**: `FocusManager.ActivateWindowAsync()` uses `AttachThreadInput(currentThread, targetThread, true)` → `AllowSetForegroundWindow` → `SetForegroundWindow` → `AttachThreadInput(currentThread, targetThread, false)`. The foreground lock timeout bypass (`SPI_SETFOREGROUNDLOCKTIMEOUT=0`) is retained as a fallback in `IFocusNativeAdapter`.

**Rationale**: `AttachThreadInput` is the documented Windows approach for cross-thread foreground activation. It temporarily merges the input queues of two threads so Windows treats Pulsar's thread as authorized to activate the target. This eliminates the need for the `keybd_event` hack entirely, which:
1. Silently corrupts `GlobalKeyboardHook._trackedAltDown` via the `WH_KEYBOARD_LL` hook
2. Is an undocumented behavioral hack that could break in future Windows versions
3. Has no way to signal intent ("this is synthetic, not a real key event")

**Alternatives considered**:
- **Keep keybd_event but add hook notification**: More complex, still relies on undocumented behavior
- **Use only SPI_SETFOREGROUNDLOCKTIMEOUT bypass**: Works in most cases but fails when target thread has an active menu/modal loop

### Decision 3: Dedicated `IFocusNativeAdapter` instead of extending `PulsarNative`

**Choice**: Create a new `IFocusNativeAdapter` interface with an instance implementation `WindowsFocusNativeAdapter` that wraps the Win32 calls. Map `SetForegroundWindow`, `AttachThreadInput`, `AllowSetForegroundWindow`, etc. through it. Mark the static `PulsarNative.SetForegroundWindow` as `[Obsolete]`.

**Rationale**: `PulsarNative` is a static utility class with ~60 P/Invoke declarations covering window management, GDI, DWM, and shell operations. Making it injectable would require wrapping ALL of them. `IFocusNativeAdapter` scopes to only the 12 focus-related P/Invokes, keeping the extraction minimal.

**Alternatives considered**:
- **Make entire PulsarNative injectable via IPulsarNative**: Too broad; 60+ methods, most never need mocking
- **Keep static and use [assembly: InternalsVisibleTo] for testing**: Doesn't work — static state (`_fgLockCount`) still can't be isolated per test

### Decision 4: Focus verification as a first-class concern in PKI

**Choice**: After `IFocusManager.ActivateWindowAsync()` completes for PKI focus restore, the `SendKeysInjectionExecutor` checks that `GetForegroundWindow()` matches the expected target handle. If mismatch, retry once with 50ms delay. If still mismatched, fail the injection with `FocusRestore` stage failure.

**Rationale**: Pulsar currently has a documented bug (PKI_REFACTORING_AND_BUGS.md) where UIA `SetValue` was abandoned because of focus timing issues. The same principle applies: if focus didn't land on the target window, injecting credentials into whichever window currently has focus is a security risk. Verification makes this a hard failure.

**Alternatives considered**:
- **No verification, rely on SetForegroundWindow return value**: The return value is unreliable — it returns true even when focus fails to transfer due to UIPI, hung threads, or virtual desktop mismatch
- **Always wait 500ms and hope**: Timing-based "fixes" are brittle; explicit verification is deterministic

### Decision 5: Defer `ReleaseAsync()` until after menu fade-out animation

**Choice**: Move `IFocusManager.ReleaseAsync()` from the start of `Dismiss()` to the `fadeOut.Completed` handler. The 100ms fade-out animation completes before focus is restored to the target window, preventing the visual overlap of a Topmost menu over a newly-focused target.

**Rationale**: The current code restores focus immediately while the Topmost window is still fading out. This causes 100ms of visual glitch where the target window has focus but appears behind the menu. Deferring until animation completion fixes this without changing any other behavior.

**Trade-off**: Quick Switch timing increases by ~100ms (menu takes 100ms to fade before target window appears). This is imperceptible — the target window activation itself takes similar time. Actually: Quick Switch hides the menu via `context.IsVisible = false` BEFORE focus restore, so the quick switch path is unaffected. Only the normal dismiss (user releases modifier, no action taken) sees the 100ms added delay.

### Decision 6: `IModifierStateTracker` interface for hook coordination

**Choice**: `GlobalKeyboardHook` implements `IModifierStateTracker`, which exposes `OnSyntheticEventBegin()` and `OnSyntheticEventEnd()`. Before `FocusManager` calls `SetForegroundWindow` (which no longer uses `keybd_event`), it calls `OnSyntheticEventBegin()`. If in the future any FocusManager operation needs to inject synthetic keys, the tracker can suppress modifier updates during that window.

**Rationale**: Even though `AttachThreadInput` replaces `keybd_event`, the interface provides a safety net for future synthetic input needs (e.g., if PKI injection ever needs `SendInput` from within FocusManager). It also cleanly separates the hook's modifier tracking concern from FocusManager's activation concern.

**Alternatives considered**:
- **No interface, just document "don't call keybd_event"**: Fragile; future developers won't know about the hook interaction
- **FocusManager directly references GlobalKeyboardHook**: Tight coupling; breaks testability

### Decision 7: Migration strategy — delegation, not rewrite

**Choice**: Each existing call site is migrated one phase at a time, using delegation: the old code is changed to call `IFocusManager` instead of `PulsarNative.SetForegroundWindow`, but the old `PulsarNative.SetForegroundWindow` is kept (marked `[Obsolete]`) until all callers are migrated. This allows incremental verification at each step.

**Rationale**: A "big bang" replacement of all 7 call sites simultaneously carries high risk of regressions. Phase-by-phase migration allows running the existing test suite after each change.

## Risks / Trade-offs

| Risk | Mitigation |
|---|---|
| `AttachThreadInput` might not work across integrity levels (UIPI) | Retain `AllowSetForegroundWindow` + foreground lock timeout bypass in `IFocusNativeAdapter` as fallback path in `ActivateWindowAsync` |
| `AttachThreadInput` might fail if target thread is hung (not pumping messages) | Set a timeout on `SetForegroundWindow` retries; classify as `FocusActivationFailureReason.TargetThreadHung` |
| Replacing PKI's `IFocusRestorer` breaks external PKI plugins | PKI is a Core plugin (managed entirely in-repo). Mark interfaces as `[Obsolete]` for one release before removal |
| GlobalKeyboardHook implementing `IModifierStateTracker` adds coupling | The interface is minimal (2 methods), lives in `Core/Interfaces/`, and FocusManager holds an optional reference (`IModifierStateTracker?`) |
| FocusManager becomes a God Object | Scope is strictly limited to foreground window operations. Window enumeration, icon extraction, DWM thumbnails stay in WindowService |
| 100ms animation delay might cause perceptible lag on dismiss-only scenarios | The delay only affects the path where user releases modifier without selecting a slot. Quick Switch and plugin execution hide the menu before focus restore (unaffected) |

## Migration Plan

### Phase 1: Extract native adapter (no behavior change)
1. Create `IFocusNativeAdapter` + `WindowsFocusNativeAdapter`
2. Register in DI
3. Verify: existing code unchanged, new adapter can be resolved

### Phase 2: Build FocusManager core
1. Create `IFocusManager` + `FocusManager` implementation
2. Implement `Capture()`, `ActivateWindowAsync()`, `ReleaseAsync()`
3. Refactor `WindowService.ForceForegroundWindow()` → delegate to `IFocusManager`
4. Refactor `WindowActivator.ActivateWindow()` → delegate to `IFocusManager.ActivateWindowAsync()`
5. Verify: existing window switching behavior unchanged

### Phase 3: Hook coordination + kill keybd_event
1. `GlobalKeyboardHook` implements `IModifierStateTracker`
2. `FocusManager` uses `AttachThreadInput` path (no more `keybd_event`)
3. Verify: RDP modifier fix still works, no stuck modifier keys

### Phase 4: Integrate PKI
1. `SendKeysInjectionExecutor` uses `IFocusManager` directly
2. Add post-activation verification step
3. Remove `IFocusRestorer`/`IWindowFocusSimulator` registrations
4. Verify: PKI injection tests pass, verification catches misdirected focus

### Phase 5: Fix RadialMenuWindow timing
1. Move `ReleaseAsync()` into `fadeOut.Completed` handler
2. Verify: no visual glitch on dismiss, target window not occluded

### Phase 6: Migrate plugins
1. Migrate `VbaRunnerPlugin`, `BookmarkletRunnerPlugin`, `ScriptEngine`
2. Mark `PulsarNative.SetForegroundWindow` as `[Obsolete]`
3. Verify: plugin functionality unchanged

### Rollback
Each phase is independently deployable and revertible. If any phase causes issues, revert that phase's changes and keep the previous phases intact.

## Open Questions

1. **Should `IFocusNativeAdapter` include `LockSetForegroundWindow`?** Currently used in `SetForegroundWindowInternal` to lock/unlock around the activation. With `AttachThreadInput`, locking may be unnecessary. Decision: exclude from initial adapter; add if needed.

2. **Should FocusManager own the `WindowActivationMonitor`, or should it remain in WindowService?** The monitor feeds the Quick Switch history stack. If FocusManager owns history, it should own the monitor. Decision: keep monitor in WindowService for now; FocusManager receives history events via `IFocusHistory`.

3. **What is the exact retry policy for ActivateWindowAsync verification failures?** Current proposal: 1 retry at 50ms. Could be configurable via `FocusActivationOptions`. Decision: implement as configurable; default to 1 retry at 50ms.

4. **Should `IFocusManager` expose an event for external focus change notifications?** Currently `WindowService` subscribes to `WindowActivationMonitor.WindowActivated`. If plugins need this, they could go through `IFocusManager`. Decision: not in initial scope; can be added later.
