## Why

Pulsar's focus (foreground window) management is scattered across 6 independent components with 7 distinct `SetForegroundWindow` call sites, a static `keybd_event` hack that silently corrupts the keyboard hook's modifier tracker, no post-activation verification in the PKI injection chain, and an animation/focus-release race condition in the radial menu dismiss. This fragmentation makes focus behavior unpredictable, untestable, and impossible to debug holistically. Applying the same Ports & Adapters pattern used elsewhere in Pulsar to focus management eliminates these issues and creates a single, testable source of truth.

## What Changes

- **Introduce `IFocusManager`** — a new DI-registered service that is the sole authority for all foreground window operations (acquire, release, verify, track history)
- **Introduce `IFocusNativeAdapter`** — abstracts all Win32 focus-related P/Invoke calls behind an injectable, mockable interface, replacing the static `PulsarNative.SetForegroundWindow` and its static `_fgLockCount`/`_originalTimeout` state
- **Introduce `IModifierStateTracker`** — allows FocusManager to notify the keyboard hook before synthetic key events, replacing the `keybd_event(VK_MENU)` hack with `AttachThreadInput` and preventing modifier state corruption
- **Replace PKI's redundant focus chain** (`IFocusRestorer` → `WindowsFocusRestorer` → `IWindowFocusSimulator` → `WindowsFocusSimulator`) with direct `IFocusManager` usage, adding post-activation verification to credential injection
- **Fix radial menu dismiss timing** — defer `ReleaseAsync()` until after the fade-out animation completes, eliminating the visual glitch where the target window appears behind a still-visible Topmost menu
- **Migrate all direct `SetForegroundWindow` calls** in plugins (VbaRunner, BookmarkletRunner, ScriptEngine) to `IFocusManager.ActivateWindowAsync()`
- **Remove `WindowHelper`** (already `[Obsolete]`) and mark `PulsarNative.SetForegroundWindow` as deprecated
- **BREAKING**: `IFocusRestorer` and `IWindowFocusSimulator` interfaces are removed; any external plugin depending on them must migrate to `IFocusManager`

## Capabilities

### New Capabilities

- `focus-manager`: Centralized focus orchestration — capture context, activate Pulsar menu, release focus to target, direct window activation with AttachThreadInput, and Quick Switch coordination. All SetForegroundWindow operations flow through this single service.
- `focus-verification`: Post-activation verification that focus actually landed on the expected window, with configurable retry/backoff. Required for PKI credential injection safety.
- `keyboard-hook-focus-sync`: Coordination protocol between FocusManager and GlobalKeyboardHook so synthetic keyboard events (formerly `keybd_event`) do not corrupt modifier state tracking.

### Modified Capabilities

- `window-switch-activation-path`: The shared activation path (currently `WindowActivator`) SHALL delegate to `IFocusManager.ActivateWindowAsync()` rather than calling `PulsarNative.SetForegroundWindow` directly. Activation behavior and flash confirmation contract remain unchanged; only the internal dispatch layer changes.
- `pki-runtime-architecture`: PKI injection SHALL use `IFocusManager` for focus restoration and verification instead of the separate `IFocusRestorer`/`IWindowFocusSimulator` chain. The injection plan structure (HideLauncher → RestoreFocus → Delay → Inject) is preserved; focus verification is added after the Delay step.

## Impact

- **Affected code**: `PulsarNative.cs` (~100 lines removed), `WindowHelper.cs` (entire file removed), `WindowService.cs` (~80 lines refactored to delegation), `WindowActivator.cs` (refactored), `SendKeysInjectionExecutor.cs` (adds verification), `WindowsFocusRestorer.cs` + `WindowsFocusSimulator.cs` (removed), `IFocusRestorer.cs` + `IWindowFocusSimulator.cs` (removed), `GlobalKeyboardHook.cs` (implements IModifierStateTracker), `RadialMenuWindow.xaml.cs` (fixes dismiss timing), `VbaRunnerPlugin.cs`, `BookmarkletRunnerPlugin.cs`, `ScriptEngine.cs` (migrate to IFocusManager)
- **New files**: `Core/Interfaces/IFocusManager.cs`, `Core/Interfaces/IFocusNativeAdapter.cs`, `Core/Interfaces/IModifierStateTracker.cs`, `Services/FocusManager.cs`, `Services/WindowsFocusNativeAdapter.cs`, `Core/Focus/Focus*.cs` (support types)
- **DI changes**: 4 new registrations, 2 registrations removed
- **Breaking**: External plugins referencing `IFocusRestorer` or `IWindowFocusSimulator` need migration
