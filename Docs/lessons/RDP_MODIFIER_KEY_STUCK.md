# RDP Modifier Key Stuck Issue

**Status**: Resolved  
**Severity**: High  
**Affected Versions**: < v1.1.0  
**Fixed In**: v1.1.0  
**Date Discovered**: 2026-03-09  
**Root Cause**: RDP protocol state synchronization delay

---

## Problem Description

### Symptom

When using Pulsar in a Remote Desktop (RDP) session, modifier keys (especially Shift) appear "stuck" after being released:

1. User presses `Ctrl + Shift + Q` to open radial menu
2. User releases Shift key
3. Radial menu does not execute action (expects Shift to be released)
4. User must press and release Shift again to trigger execution

### Impact

- **Hotkey Detection**: Modifier release events not detected, blocking menu execution
- **User Experience**: Requires double key press, feels unresponsive
- **Scope**: Affects all RDP sessions (mstsc.exe, Remote Desktop Connection)

---

## Root Cause Analysis

### Technical Details

#### 1. RDP Protocol Behavior

Remote Desktop Protocol introduces latency in keyboard event transmission:

```
┌─────────────┐         ┌──────────────┐         ┌─────────────┐
│ Local Client│         │ RDP Protocol │         │ Remote Host │
│  (Physical) │         │    Layer     │         │  (Pulsar)   │
└─────────────┘         └──────────────┘         └─────────────┘
      │                        │                        │
      │ KeyDown(Shift)         │                        │
      ├───────────────────────>│                        │
      │                        ├───────────────────────>│
      │                        │                        │ GetKeyState() = 0x8000
      │                        │                        │
      │ KeyUp(Shift)           │                        │
      ├───────────────────────>│                        │
      │                        │  ⚠️ DELAYED/DROPPED   │
      │                        │                        │
      │                        │                        │ GetKeyState() = 0x8000 (STALE!)
      │                        │                        │
```

#### 2. Original Implementation (Flawed)

`GlobalKeyboardHook.cs` (before fix):

```csharp
// Line 99-102: Reads modifier state using GetKeyState()
bool isCtrl = (GetKeyState(VK_LCONTROL) & 0x8000) != 0 || (GetKeyState(VK_RCONTROL) & 0x8000) != 0;
bool isShift = (GetKeyState(VK_LSHIFT) & 0x8000) != 0 || (GetKeyState(VK_RSHIFT) & 0x8000) != 0;
bool isAlt = (GetKeyState(VK_LALT) & 0x8000) != 0 || (GetKeyState(VK_RALT) & 0x8000) != 0;
bool isWin = (GetKeyState(VK_LWIN) & 0x8000) != 0 || (GetKeyState(VK_RWIN) & 0x8000) != 0;
```

**Problem**: `GetKeyState()` queries the system's keyboard state table, which may be stale in RDP sessions due to protocol delays.

#### 3. Why GetKeyState() Fails in RDP

- **Local Environment**: `GetKeyState()` reads from kernel-mode keyboard state buffer (accurate)
- **RDP Environment**: Keyboard state buffer updated by RDP protocol layer (delayed)
- **Hook Events**: `WM_KEYUP` messages arrive at hook callback immediately (accurate)
- **State Query**: `GetKeyState()` called in hook callback reads stale buffer (inaccurate)

---

## Solution: Modifier State Tracker

### Design Principle

**"Trust the Hook, Verify the State"**

- Hook events (`WM_KEYDOWN`/`WM_KEYUP`) are the ground truth of user intent
- Maintain internal state based on Hook events, not `GetKeyState()`
- Provide fallback to `GetKeyState()` for backward compatibility (Legacy Mode)

### Implementation

#### 1. Internal State Tracker

Added to `GlobalKeyboardHook.cs`:

```csharp
// [RDP Fix] Internal modifier state tracker (Ground Truth)
private bool _trackedCtrlDown = false;
private bool _trackedShiftDown = false;
private bool _trackedAltDown = false;
private bool _trackedWinDown = false;

// [Configuration] Modifier state detection mode
private bool _useHybridMode = true; // Default: Hybrid (RDP-safe)
```

#### 2. State Update Logic

```csharp
private void UpdateModifierTracker(int vkCode, bool isKeyDown, bool isKeyUp)
{
    // Shift (both L/R variants + generic VK_SHIFT)
    if (vkCode == VK_LSHIFT || vkCode == VK_RSHIFT || vkCode == 0x10)
    {
        if (isKeyDown)
        {
            _trackedShiftDown = true;
        }
        else if (isKeyUp)
        {
            _trackedShiftDown = false; // ✅ Immediate update on KeyUp
        }
    }
    // ... similar for Ctrl, Alt, Win
}
```

#### 3. Hybrid Mode Selection

```csharp
if (_useHybridMode)
{
    // Hybrid Mode: Trust internal tracker (immune to RDP sync issues)
    isCtrl = _trackedCtrlDown;
    isShift = _trackedShiftDown;
    isAlt = _trackedAltDown;
    isWin = _trackedWinDown;
}
else
{
    // Legacy Mode: Use GetKeyState() for backward compatibility
    isCtrl = (GetKeyState(VK_LCONTROL) & 0x8000) != 0 || (GetKeyState(VK_RCONTROL) & 0x8000) != 0;
    isShift = (GetKeyState(VK_LSHIFT) & 0x8000) != 0 || (GetKeyState(VK_RSHIFT) & 0x8000) != 0;
    // ...
}
```

### Configuration

Users can switch modes in `Profiles.json`:

```json
{
  "Settings": {
    "Input": {
      "ModifierStateMode": "Hybrid",  // "Hybrid" (default) | "Legacy"
      "EnableModifierStateLogging": false
    }
  }
}
```

---

## Verification

### Test Scenarios

#### ✅ Scenario 1: Local Environment
- **Action**: Press `Ctrl + Shift + Q`, release Shift
- **Expected**: Menu executes immediately
- **Result**: PASS (both Hybrid and Legacy modes)

#### ✅ Scenario 2: RDP Session (Hybrid Mode)
- **Action**: Press `Ctrl + Shift + Q`, release Shift
- **Expected**: Menu executes immediately
- **Result**: PASS (Hybrid mode uses Hook events)

#### ❌ Scenario 3: RDP Session (Legacy Mode)
- **Action**: Press `Ctrl + Shift + Q`, release Shift
- **Expected**: Menu executes immediately
- **Result**: FAIL (GetKeyState() returns stale state)

### Performance Impact

- **Memory**: +4 bytes (4 bool fields)
- **CPU**: Negligible (boolean assignment in hook callback)
- **Latency**: No additional latency (state updated synchronously)

---

## Lessons Learned

### Architectural Insights

1. **Don't Trust System APIs in Virtualized Environments**
   - RDP, VMs, and sandboxes introduce state synchronization delays
   - Hook events are more reliable than state query APIs

2. **Maintain Ground Truth Internally**
   - External state (GetKeyState) can be stale
   - Internal state based on events is always current

3. **Provide Backward Compatibility**
   - Legacy Mode allows rollback if Hybrid Mode has issues
   - Configuration-driven behavior enables A/B testing

### Design Patterns Applied

- **State Machine**: Internal tracker maintains modifier key state
- **Strategy Pattern**: Hybrid vs Legacy mode selection
- **Fail-Safe**: ResetModifierState() for manual recovery
- **Observability**: VerifyStateConsistency() for diagnostics

---

## Related Issues

### Similar Problems in Other Applications

- **AutoHotkey**: Uses `GetKeyState()` with polling workaround
- **PowerToys**: Experienced similar RDP issues, fixed in v0.65.0
- **ShareX**: Documented RDP hotkey issues, no fix yet

### Alternative Solutions Considered

#### ❌ Option 1: Poll GetKeyState() Periodically
- **Pros**: Simple implementation
- **Cons**: High CPU usage, still subject to delays

#### ❌ Option 2: Detect RDP Session and Apply Special Logic
- **Pros**: Targeted fix for RDP only
- **Cons**: Requires session detection, not elegant, doesn't solve root cause

#### ✅ Option 3: Modifier State Tracker (Chosen)
- **Pros**: Elegant, zero overhead, solves root cause
- **Cons**: Requires careful state management

---

## References

### Microsoft Documentation

- [GetKeyState function](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getkeystate)
- [Low-Level Keyboard Hook](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc)
- [Remote Desktop Protocol](https://learn.microsoft.com/en-us/windows/win32/termserv/remote-desktop-protocol)

### Related Pulsar Documents

- [INPUT_INJECTION.md](../architecture/INPUT_INJECTION.md) - Input injection architecture
- [PLUGIN_SYSTEM.md](../architecture/PLUGIN_SYSTEM.md) - Plugin system architecture
- [AGENTS.md](../../AGENTS.md) - AI agent operational guide

---

## Code Changes

### Files Modified

1. **`Pulsar/Native/GlobalKeyboardHook.cs`**
   - Added internal state tracker fields
   - Added `UpdateModifierTracker()` method
   - Added `ResetModifierState()` public method
   - Added `VerifyStateConsistency()` diagnostic method
   - Modified `HookCallback()` to use Hybrid mode

2. **`Pulsar/Models/ProfilesConfig.cs`**
   - Added `InputSettings` class
   - Added `Input` property to `ProfileSettings`

3. **`Pulsar/App.xaml.cs`**
   - Added configuration application logic after Hotkey Service initialization

4. **`Docs/architecture/INPUT_INJECTION.md`**
   - Added "Modifier Key State Detection (RDP Fix)" section

5. **`Docs/lessons/RDP_MODIFIER_KEY_STUCK.md`** (this file)
   - Created comprehensive problem analysis and solution documentation

---

**Last Updated**: 2026-03-09  
**Author**: Pulsar Team  
**Reviewers**: AI Architecture Assistant
