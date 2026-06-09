## Context

Pulsar uses a low-level Windows keyboard hook (`WH_KEYBOARD_LL`) via `GlobalKeyboardHook` to intercept global key events for hotkey detection. The hook tracks the state of modifier keys (Ctrl, Shift, Alt, Win) in two modes:

- **Hybrid mode** (default): Uses internal `_trackedCtrlDown` / `_trackedShiftDown` / etc. booleans, updated by `UpdateModifierTracker()` on every hook event. Immune to RDP state sync issues.
- **Legacy mode**: Uses `GetKeyState()` Win32 API directly.

The `FocusManager.ActivateWindowAsync()` method wraps window activation with `OnSyntheticEventBegin()` / `OnSyntheticEventEnd()` calls on the hook (via `IModifierStateTracker`). This sets `_syntheticEventSuppression = true`, causing `UpdateModifierTracker()` to return early for ALL events—both synthetic (from `SendInput`/`keybd_event`) and real physical keystrokes. The intent is to prevent synthetic Alt key events (from `ForceActivate`) from corrupting `_trackedAltDown`.

**The problem**: The suppression window (50-200ms) is long enough that if the user releases Shift during it, the physical Shift KeyUp event is silently dropped. `_trackedShiftDown` stays true permanently, causing all subsequent hotkey checks to see Shift as held.

**Additional issue**: `FocusManager.ForceActivate()` still calls `keybd_event(VK_MENU, ...)` to simulate an Alt key press for foreground stealing. This violates the existing `keyboard-hook-focus-sync` spec (which mandates no `keybd_event` usage) and is the sole reason `_syntheticEventSuppression` exists.

## Goals / Non-Goals

**Goals:**
1. Prevent real physical modifier KeyUp events from being lost during synthetic event suppression
2. Remove `keybd_event` usage from the focus activation path (enforce existing spec)
3. Add a safety net reset of modifier state on radial menu show/hide
4. Ensure modifier tracking runs before event listener pre-filter

**Non-Goals:**
- Changing the Hybrid/Legacy mode selection logic
- Modifying `HotkeyService`'s `_pressedKeys` tracking (separate from modifier tracking)
- Restructuring the entire keyboard input pipeline
- Adding test coverage for existing untested code (out of scope; the hook is hard to integration-test)

## Decisions

### Decision 1: Read `LLKHF_INJECTED` flag from `KBDLLHOOKSTRUCT.flags`

**Choice**: Extend the hook callback to read the `flags` field at offset 8 in `KBDLLHOOKSTRUCT`, extract bit 4 (`LLKHF_INJECTED = 0x10`), and pass `isInjected` to `UpdateModifierTracker`. During suppression, only skip injected events.

```
KBDLLHOOKSTRUCT:
  offset 0:  vkCode      ← already read via Marshal.ReadInt32(lParam)
  offset 4:  scanCode
  offset 8:  flags       ← NEW: read to check LLKHF_INJECTED (bit 4)
  offset 12: time
  offset 16: dwExtraInfo
```

**Alternatives considered**:
- *Reset modifier state after suppression ends*: Simpler but doesn't prevent corruption during the window—only cleans up after. A physical Shift press during suppression could still cause misbehavior.
- *Don't suppress KeyUp at all*: Simple but incomplete. An injected Shift KeyUp could cause `_trackedShiftDown = false` when Shift is actually held. Less harmful than the opposite, but still a bug.
- *Use `dwExtraInfo` to tag synthetic events*: Requires modifying all `SendInput`/`keybd_event` calls to set a known `dwExtraInfo` value. More invasive and error-prone.

**Rationale**: Using the OS-provided `LLKHF_INJECTED` flag is the standard Windows approach, requires no changes to existing `SendInput` calls, and correctly distinguishes injected events regardless of source.

### Decision 2: Remove `keybd_event` from `ForceActivate`

**Choice**: Eliminate the `keybd_event(VK_MENU, 0, 0, 0)` / `keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0)` calls and `Thread.Sleep(50)` from `ForceActivate()`. Replace with only `AllowSetForegroundWindow(ASFW_ANY)` + `SetForegroundWindowNative` + `BringWindowToTop`.

**Rationale**:
- The existing `keyboard-hook-focus-sync` spec already mandates this (Requirement: "FocusManager SHALL NOT use keybd_event for foreground activation")
- The Alt key simulation hack is unreliable and was always a last resort
- With `LLKHF_INJECTED` filtering in place, even if we kept `keybd_event`, the synthetic Alt events would be correctly filtered—but removing them entirely eliminates the need for suppression in the first place

**After removal**: The `OnSyntheticEventBegin/End` wrapper in `ActivateWindowAsync` still serves a purpose (future-proofing for any remaining synthetic event scenarios), but the most likely corruption vector is eliminated.

### Decision 3: Safety net—reset modifier state on menu show/hide

**Choice**: Add calls to `IModifierStateTracker.ResetAllModifiers()` when the radial menu is shown and when it's hidden. This is a belt-and-suspenders measure that guarantees clean modifier state at key lifecycle boundaries.

**Rationale**: Even with the LLKHF_INJECTED fix, edge cases could still cause stale state (e.g., if Windows itself misreports the injected flag in certain scenarios). A reset on menu boundaries is cheap and provides defense-in-depth.

### Decision 4: Reorder pre-filter and tracker update

**Choice**: Move the `UpdateModifierTracker` call before the pre-filter listener check in `HookCallback`.

**Current order** (buggy):
```
1. Check if listeners exist → if not, early return (BEFORE tracker update)
2. UpdateModifierTracker (never reached if no listeners)
3. Process event
```

**New order** (correct):
```
1. UpdateModifierTracker (always runs, regardless of listeners)
2. If no listeners, early return (event still passed to OS, tracker already updated)
3. Process event
```

**Rationale**: The tracker is a system-level concern that should always reflect physical state. The listener pre-filter is an optimization that should not affect correctness.

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| `LLKHF_INJECTED` might not be set for all synthetic events from all sources (e.g., some virtual keyboard drivers) | Physical events are always non-injected. If injected flag is missing on a synthetic event, it will update the tracker—worst case is the same as before the fix. |
| Removing `keybd_event` from `ForceActivate` could reduce activation reliability for stubborn windows | `AttachThreadInput` is already the primary path and handles most cases. `ForceActivate` was the third fallback after `AttachThreadInput` failure AND fallback failure. The `AllowSetForegroundWindow(ASFW_ANY)` call is retained, which is the effective part of the hack. |
| `ResetAllModifiers` on menu show could clear a legitimate modifier hold (e.g., user holds Shift while invoking menu) | Show is triggered by hotkey, which requires specific modifiers. After trigger, the hook's tracker already reflects the correct state from the KeyDown events. Resetting at show time is safe because the hotkey already matched correctly before the reset. |
