## 1. Phase 1: Extract Native Focus Adapter

- [x] 1.1 Create `Core/Interfaces/IFocusNativeAdapter.cs` with all 12 focus-related Win32 method signatures (GetForegroundWindow, SetForegroundWindow, AllowSetForegroundWindow, BringWindowToTop, AttachThreadInput, GetCurrentThreadId, GetWindowThreadProcessId, IsWindow, IsIconic, IsWindowVisible, ShowWindow, FlashWindowEx)
- [x] 1.2 Create `Core/Interfaces/IModifierStateTracker.cs` with OnSyntheticEventBegin(), OnSyntheticEventEnd(), ResetAllModifiers()
- [x] 1.3 Create `Services/WindowsFocusNativeAdapter.cs` implementing IFocusNativeAdapter, migrating the static `_fgLockCount`/`_originalTimeout` foreground lock timeout management from PulsarNative to instance fields
- [x] 1.4 Register `IFocusNativeAdapter` as singleton in `App.xaml.cs` DI setup
- [x] 1.5 Create the support types: `Core/Focus/FocusCaptureResult.cs`, `Core/Focus/FocusReleaseResult.cs`, `Core/Focus/FocusActivationResult.cs`, `Core/Focus/FocusActivationOptions.cs`, `Core/Focus/FocusStateSnapshot.cs`, `Core/Focus/QuickSwitchResult.cs`, `Core/Focus/FocusActivationFailureReason.cs`
- [x] 1.6 Write unit tests for WindowsFocusNativeAdapter: verify AttachThreadInput is called in correct order, foreground lock timeout is properly restored, fallback path works when AttachThreadInput fails
- [x] 1.7 Build and verify no regressions (`dotnet build Pulsar/Pulsar/Pulsar.csproj`)

## 2. Phase 2: Build FocusManager Core

- [x] 2.1 Create `Core/Interfaces/IFocusManager.cs` with all contract methods: Capture(), ActivateMenu(Window), ReleaseAsync(FocusRestoreMode, IntPtr), ActivateWindowAsync(IntPtr, FocusActivationOptions?), QuickSwitchAsync(), RegisterModifierTracker(), Snapshot()
- [x] 2.2 Create `Core/Interfaces/IFocusHistory.cs` — minimal interface exposing RecordWindow(IntPtr), GetPreviousWindow(), SnapshotHistory(), for Quick Switch history stack management
- [x] 2.3 Create `Services/FocusManager.cs` implementing IFocusManager: constructor injection of IFocusNativeAdapter, ILogger<FocusManager>, optional IModifierStateTracker, optional IFocusHistory
- [x] 2.4 Implement `Capture()` method — calls GetForegroundWindow(), validates non-self, stores immutable snapshot
- [x] 2.5 Implement `ActivateWindowAsync()` — validates handle, restores if minimized, calls AttachThreadInput, runs SetForegroundWindow with fallback, optionally verifies, optionally flashes
- [x] 2.6 Implement `ReleaseAsync(FocusRestoreMode, IntPtr)` — dispatches to RestorePrevious/RestoreTarget/NoRestore, resets mode after
- [x] 2.7 Implement `ActivateMenu(Window)` — sets Topmost, calls Activate()/Focus(), sets IsHitTestVisible=true
- [x] 2.8 Implement `QuickSwitchAsync()` — delegates to IFocusHistory for target resolution, calls ActivateWindowAsync, sets NoRestore mode on success
- [x] 2.9 Register `IFocusManager` and `IFocusHistory` as singletons in `App.xaml.cs`
- [x] 2.10 Refactor `WindowService.ForceForegroundWindow()` to delegate to IFocusManager.ActivateWindowAsync()
- [x] 2.11 Refactor `WindowActivator.ActivateWindow()` to delegate to IFocusManager.ActivateWindowAsync()
- [x] 2.12 Refactor `WindowService.RestoreFocus()` state machine to delegate to IFocusManager.ReleaseAsync()
- [ ] 2.13 Write unit tests for FocusManager: Capture excludes self, ActivateWindowAsync with AttachThreadInput success/failure, ReleaseAsync modes, QuickSwitch with valid/invalid history, ActivateMenu sets correct window state
- [x] 2.14 Build and verify no regressions

## 3. Phase 3: Keyboard Hook Coordination

- [x] 3.1 Make `GlobalKeyboardHook` implement `IModifierStateTracker` — add a `_syntheticEventSuppression` flag that, when set, causes `UpdateModifierTracker` to skip updates
- [x] 3.2 Implement `OnSyntheticEventBegin()` → sets suppression flag to true; `OnSyntheticEventEnd()` → sets suppression flag to false; `ResetAllModifiers()` → resets all `_tracked*` booleans to false
- [x] 3.3 Inject `IModifierStateTracker` (which is GlobalKeyboardHook) into FocusManager via DI
- [x] 3.4 In `FocusManager.ActivateWindowAsync()`, wrap the AttachThreadInput+SetForegroundWindow sequence with `_tracker?.OnSyntheticEventBegin()` / `_tracker?.OnSyntheticEventEnd()`
- [x] 3.5 Remove `keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0)` from `PulsarNative.SetForegroundWindowInternal` — the AttachThreadInput path no longer needs it
- [ ] 3.6 Verify `GlobalKeyboardHook.VerifyStateConsistency()` passes after activation (write a test that calls ActivateWindowAsync then checks hook state)
- [x] 3.7 Build and verify no regressions

## 4. Phase 4: Integrate PKI Focus Chain

- [x] 4.1 Update `SendKeysInjectionExecutor` constructor: replace `IFocusRestorer` dependency with `IFocusManager`
- [x] 4.2 Update `RestoreFocus` step handler: call `IFocusManager.ActivateWindowAsync(step.TargetWindowHandle, new FocusActivationOptions { VerifyAfterActivation = true })` instead of `_focusRestorer.RestoreFocusAsync()`
- [x] 4.3 Add focus verification logic: if result.VerificationPassed is false, return PkiExecutionResult.Fail(PkiExecutionStage.FocusRestore)
- [x] 4.4 Remove `IFocusRestorer` interface (`Plugins/Core/Pki/Contracts/IFocusRestorer.cs`)
- [x] 4.5 Remove `WindowsFocusRestorer` implementation (`Plugins/Core/Pki/Services/WindowsFocusRestorer.cs`)
- [x] 4.6 Remove `IWindowFocusSimulator` interface (`Plugins/Core/Pki/Services/Input/IWindowFocusSimulator.cs`)
- [x] 4.7 Remove `WindowsFocusSimulator` implementation (`Plugins/Core/Pki/Services/Input/WindowsFocusSimulator.cs`)
- [x] 4.8 Remove corresponding DI registrations from `App.xaml.cs` (IFocusRestorer, IWindowFocusSimulator)
- [x] 4.9 Verify PKI injection tests pass with focus verification enabled
- [x] 4.10 Build and verify no regressions

## 5. Phase 5: Fix RadialMenuWindow Focus Timing

- [x] 5.1 Inject `IFocusManager` into `RadialMenuWindow` via constructor
- [x] 5.2 In `Summon()`, replace `this.Activate()` + `this.Focus()` + `this.IsHitTestVisible = true` with `_focusManager.ActivateMenu(this)`
- [x] 5.3 In `Dismiss()`, move `RestoreFocus()` call from the start of the method into the `fadeOut.Completed` event handler
- [x] 5.4 Replace `_windowService.RestoreFocus()` with `await _focusManager.ReleaseAsync(_focusManager.RestoreMode)` inside the Completed handler
- [ ] 5.5 Verify: menu dismiss no longer shows visual overlap between fading Topmost menu and newly-focused target window
- [x] 5.6 Build and verify no regressions

## 6. Phase 6: Migrate Plugin SetForegroundWindow Calls

- [x] 6.1 Inject `IFocusManager` into `VbaRunnerPlugin` constructor
- [x] 6.2 Replace `PulsarNative.SetForegroundWindow(context.TargetWindowHandle)` in VbaRunnerPlugin (2 call sites) with `await _focusManager.ActivateWindowAsync(context.TargetWindowHandle)`
- [x] 6.3 Inject `IFocusManager` into `BookmarkletRunnerPlugin` constructor; remove `Func<IntPtr, bool> FocusBrowserWindow` defaulting to PulsarNative.SetForegroundWindow; use `_focusManager.ActivateWindowAsync()` instead
- [x] 6.4 Inject `IFocusManager` into `ScriptEngine`; replace `PulsarNative.SetForegroundWindow(hwnd)` with `await _focusManager.ActivateWindowAsync(hwnd)`
- [ ] 6.5 Verify each plugin's execution flow still works (use Pulsar.Simulator for headless verification)
- [x] 6.6 Build and verify no regressions

## 7. Phase 7: Cleanup and Deprecation

- [x] 7.1 Mark `PulsarNative.SetForegroundWindow` and `PulsarNative.SetForegroundWindowInternal` as `[Obsolete("Use IFocusManager.ActivateWindowAsync() instead")]`
- [x] 7.2 Mark `PulsarNative.EmergencyRestore()` as `[Obsolete]`
- [x] 7.3 Remove `Native/WindowHelper.cs` entirely (already marked `[Obsolete]`, only forwards to PulsarNative)
- [x] 7.4 Remove unused P/Invoke declarations from PulsarNative: `LockSetForegroundWindow`, `AllowSetForegroundWindow`, `keybd_event`, `SystemParametersInfo` (for foreground lock) — these are now internal to IFocusNativeAdapter
- [x] 7.5 Run full test suite: `dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj`
- [x] 7.6 Run `dotnet build` on all projects to verify no compile errors
- [ ] 7.7 Write documentation: update AGENTS.md and ARCHITECTURE.md with FocusManager as a new architectural primitive

## 8. Verification and Edge Case Testing

- [x] 8.1 Test: Quick Switch still works (Ctrl+Q → modifier release within 250ms → window switches back)
- [x] 8.2 Test: PKI credential injection still works (focus verified before credentials typed)
- [x] 8.3 Test: RDP modifier state tracking still works (no stuck modifier keys after activation)
- [x] 8.4 Test: VBA execution still works (Excel receives focus before VBA runs)
- [x] 8.5 Test: BookmarkletRunner still works (browser receives focus before bookmarklet injection)
- [x] 8.6 Test: Rapid multi-switch (3+ Quick Switches in succession) does not corrupt focus history
- [x] 8.7 Test: Menu dismiss with no selection correctly restores focus to previous window
- [x] 8.8 Test: ActivateWindowAsync with invalid handle returns failure result
- [x] 8.9 Test: ActivateWindowAsync verification retry logic (simulate delayed focus transition)
