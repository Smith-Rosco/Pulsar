## 1. GlobalKeyboardHook — Injected Event Detection

- [x] 1.1 Read `KBDLLHOOKSTRUCT.flags` field (offset 8) in `HookCallback` to extract `LLKHF_INJECTED` (bit 4, value 0x10) flag
- [x] 1.2 Add `bool isInjected` parameter to `UpdateModifierTracker` method signature
- [x] 1.3 Modify `UpdateModifierTracker` to skip only injected events during suppression: `if (_syntheticEventSuppression && isInjected) return;`
- [x] 1.4 Move `UpdateModifierTracker` call BEFORE the listener pre-filter check in `HookCallback` so tracker always updates regardless of subscribers
- [x] 1.5 Pass `isInjected` from `HookCallback` to `UpdateModifierTracker` for each event

## 2. FocusManager — Remove keybd_event Hack

- [x] 2.1 Remove `_native.KeybdEvent(VK_MENU, 0, 0, 0)` (Alt press) from `ForceActivate` in `FocusManager.cs`
- [x] 2.2 Remove `_native.KeybdEvent(VK_MENU, 0, KEYEVENTF_KEYUP, 0)` (Alt release) from `ForceActivate`
- [x] 2.3 Remove `Thread.Sleep(50)` delay from `ForceActivate` (was waiting for synthetic event propagation, now unnecessary)
- [x] 2.4 Retain `_native.AllowSetForegroundWindow(-1)` (ASFW_ANY) + `SetForegroundWindowNative` + `BringWindowToTop` in `ForceActivate`

## 3. Radial Menu — Safety Net Modifier Reset

- [x] 3.1 Inject `IModifierStateTracker` or expose `ResetModifierState()` through `IHotkeyService` for access by `RadialMenuViewModel`
- [x] 3.2 Call `ResetAllModifiers()` when the radial menu becomes visible (in the `Show` method or `IsVisible` setter)
- [x] 3.3 Call `ResetAllModifiers()` when the radial menu is hidden (in the menu dismiss flow)

## 4. HotkeyService — Interface Update (Optional)

- [x] 4.1 Add `void ResetModifierState()` method to `IHotkeyService` interface if needed for RadialMenuViewModel access
- [x] 4.2 Implement `ResetModifierState()` in `HotkeyService` delegating to `_hook.ResetModifierState()`

## 5. Verification

- [x] 5.1 Run `dotnet build` and fix any compilation errors
- [ ] 5.2 Manually verify: press Ctrl+Shift+Q (ShowGrid), release Shift during menu operation, then press Ctrl+Q — should open ShowSwitcher, NOT ShowGrid
- [ ] 5.3 Manually verify: use PKI credential injection or Command plugin key-sending with Shift combinations — modifier state should remain correct after injection
- [ ] 5.4 Manually verify: window switching via radial menu does not corrupt modifier state
