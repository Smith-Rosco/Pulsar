# Input Injection Architecture

**Status**: Published  
**Scope**: Architecture  
**Applies To**: PKI plugin, text injection scenarios  
**Last Updated**: 2026-03-03

---

## Overview

When injecting text into external applications (e.g., browsers, terminals), Pulsar uses a hierarchical approach prioritizing stability and performance.

---

## Injection Method Hierarchy

### 1. UI Automation (UIA) - Preferred

**Mechanism**: Uses Windows Automation API (`IUIAutomationValuePattern.SetValue`).

**Pros**:
- Instantaneous
- Invisible to user
- Does not touch clipboard
- Thread-safe (if marshaled correctly)

**Cons**:
- Requires target element to support `ValuePattern`
- Not all applications support UIA (Modern Browsers do)

**Code**: See `Pulsar.Native.UiaHelper`

---

### 2. Clipboard Paste (Ctrl+V) - Fallback

**Mechanism**: Set Clipboard text → Send `Ctrl+V`.

**Pros**:
- Fast
- Universally supported

**Cons**:
- Overwrites user's clipboard (requires save/restore)
- Prone to locking errors (`ExternalException`)
- Requires STA thread affinity

---

### 3. Simulated Typing (SendInput) - Last Resort

**Mechanism**: Sends array of `KEYBDINPUT` structures.

**Pros**:
- Works everywhere
- No clipboard issues

**Cons**:
- Slow (limited by target app's UI thread speed)
- Visible "typing" animation
- User can see the text being typed

---

## Best Practice

Always attempt UIA first. If it fails (element not found or pattern not supported), fall back to Clipboard or Typing depending on the context:
- **Typing**: Safer for small text (< 50 characters)
- **Clipboard**: Better for large blocks of text

---

## Implementation Example

```csharp
public async Task InjectTextAsync(string text, IntPtr targetWindow)
{
    // 1. Try UIA first
    if (await TryUiaInjectionAsync(text, targetWindow))
    {
        _logger.LogInformation("Text injected via UIA");
        return;
    }
    
    // 2. Fall back to clipboard
    if (text.Length > 50)
    {
        await ClipboardPasteAsync(text, targetWindow);
        _logger.LogInformation("Text injected via Clipboard");
        return;
    }
    
    // 3. Last resort: simulated typing
    await SimulatedTypingAsync(text, targetWindow);
    _logger.LogInformation("Text injected via SendInput");
}
```

---

## Focus Management (Focus Boomerang)

When injecting text, focus must return to the original window. Pulsar uses the "Focus Boomerang" pattern:

1. **Capture**: `WindowService.SetPreviousWindow()` captures foreground window handle when radial menu is invoked
2. **Execute**: Plugin performs its action
3. **Hide**: Radial menu hides
4. **Return**: `SetForegroundWindow()` forcefully returns focus to captured window
5. **Buffer**: `await Task.Delay(100)` allows window to stabilize
6. **Inject**: Send keystrokes to target window

**Implementation**:
```csharp
// In RadialMenuViewModel.Show()
_windowService.SetPreviousWindow(WindowHelper.GetForegroundWindow());

// In PkiHandler.ExecuteAsync()
await _mainWindow.Dispatcher.InvokeAsync(() => _mainWindow.Hide());
var targetHandle = _windowService.GetPreviousWindow();
WindowHelper.SetForegroundWindow(targetHandle);
await Task.Delay(100);
SendKeys.SendWait(password);
```

---

## Related Documents

- [ARCHITECTURE.md](../../ARCHITECTURE.md) - Focus management details
- [Plugin System](./PLUGIN_SYSTEM.md) - Plugin architecture

---

**Change History**:
- v1.0.0 (2026-03-03): Initial extraction from AGENTS.md
