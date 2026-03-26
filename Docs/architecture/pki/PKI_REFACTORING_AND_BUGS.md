# PKI Plugin Testability Refactoring & Bug Triage Report

## 1. Overview
The `PkiPlugin` was refactored to decouple it from direct Windows OS APIs (`SendKeys`, `UiaHelper`, `PulsarNative.SetForegroundWindow`). This change enabled headless unit testing via dependency injection. 

However, post-refactoring testing revealed critical edge cases regarding UI Automation (`UIA`) behavior across different applications (VS Code, Notepad, Excel). This document summarizes the initial architectural changes, the bugs discovered, their root causes, and the final solutions.

---

## 2. Architectural Refactoring

### Goal
Isolate side-effects to make `PkiPlugin` fully testable without requiring an active UI or OS environment.

### Implementation
- **Extracted Interfaces**: 
  - `IInputSimulator`: Abstracted text entry and keystroke simulation.
  - `IWindowFocusSimulator`: Abstracted window focus management.
- **Created OS Adapters**:
  - `WindowsInputSimulator`: Coordinates `IUiaTextWriter` and `ISendKeysWriter`.
  - `WindowsUiaTextWriter`: Thin wrapper around COM-based `UiaHelper`.
  - `WindowsSendKeysWriter`: Handles `SendKeys.SendWait` and custom escaping.
  - `WindowsFocusSimulator`: Wrapper around `PulsarNative.SetForegroundWindow`.
- **Refactored PkiPlugin**: Migrated from static API calls to dependency injection via `App.xaml.cs`.
- **Unit Tests Added**: Covered execution sequences and missing dependencies using `Moq`.

---

## 3. Discovered Bugs & Root Cause Analysis

After the initial refactoring, several injection bugs were identified across different applications.

### Bug 1: Password Injection Emits Empty Characters
**Symptom**: In some applications (like VS Code), the password field received nothing (blank).

**Root Cause**: 
`UiaHelper.TrySetFocusedElementText` relies on the UIA `ValuePattern.SetValue` method. For password fields (`IsPassword=true`) or custom rendering contexts (like Electron's `native-edit-context` in VS Code), `SetValue` would silently execute without throwing an exception, but no text was actually written. 
Furthermore, the validation step (`valuePattern.CurrentValue`) always returns an empty string for password fields due to OS security policies. The code incorrectly interpreted this empty string as a failure, triggering a fallback to `SendKeys`.

### Bug 2: EscapeSendKeys Double-Escaping
**Symptom**: Passwords containing special characters like `}` or `]` were injected incorrectly (e.g., `p}ass` became `p{}ass`).

**Root Cause**:
The `EscapeSendKeys` method used chained `.Replace()` calls.
```csharp
input.Replace("{", "{{}").Replace("}", "{}}")
```
When `{` was replaced with `{{}`, the subsequent replacement for `}` modified the newly generated `{` escape sequence, resulting in double-escaping and corrupted output.

### Bug 3: Username Overwritten by Password (The "NamePasswordPassword" Bug)
**Symptom**: 
- In VS Code: Worked correctly.
- In Notepad: The first line (username) was empty, and only the password appeared on the second line.
- In Excel: The focus moved to the right, but both cells remained empty (or the first cell was overwritten).

**Root Cause**:
This was the most complex issue, stemming from a fundamental misunderstanding of UIA `SetValue` semantics vs. `SendKeys` semantics.
1. **UIA `SetValue` replaces the entire content** of the focused element. It does *not* append.
2. The injection sequence was: `UIA(Account)` -> `SendKeys({TAB})` -> `UIA(Password)`.
3. `SendKeys` is asynchronous (posts to the message queue), while UIA operations are synchronous COM calls.
4. When `SendKeys({TAB})` was fired, the OS focus had not fully transitioned to the password field by the time the next `UIA(Password)` call was executed.
5. As a result, `UIA(Password)` grabbed the *old* focused element (the account field) and called `SetValue`, completely overwriting the previously injected username with the password.

**Why did it work in VS Code?**
VS Code's editor uses `native-edit-context`, which we explicitly blacklisted in `UiaHelper` because it doesn't support `ValuePattern` properly. Therefore, VS Code bypassed UIA entirely and fell back to `SendKeys` for both account and password. `SendKeys` acts like a real keyboard (appending text), which is why it worked flawlessly.

---

## 4. Final Solutions

### 1. SendKeys Escaping Fix
Rewrote `EscapeForSendKeys` in `WindowsSendKeysWriter` to process the string character-by-character using a `StringBuilder`, completely eliminating the double-escaping issue caused by chained replacements. Added 12 dedicated unit tests to verify all special characters.

### 2. UIA Validation Fix
Updated `UiaHelper` to explicitly check `focusedElement.CurrentIsPassword`. If the field is a password field, it skips the `CurrentValue` read-back validation (since it is guaranteed to be empty) and trusts the `SetValue` operation if no exception was thrown.

### 3. UIA Blacklist
Added `native-edit-context`, `Chrome_*`, and `MozillaWindowClass` to a blacklist in `UiaHelper`. For these known custom-rendered controls, UIA is bypassed immediately to save execution time and prevent silent failures.

### 4. Bypassing UIA for PKI Injection (The Core Fix)
To solve the focus timing and overwrite issues in native Win32 apps (Excel, Notepad), the `IInputSimulator` interface was expanded:
```csharp
Task SimulateTextForceSendKeysAsync(string text);
```
**Decision**: The `PkiPlugin` injection sequence was updated to **exclusively use `SimulateTextForceSendKeysAsync`**. 
UIA `SetValue` is fundamentally incompatible with rapid sequential multi-field injection (like Account -> TAB -> Password) because it relies on instantaneous, synchronous focus state which the OS cannot guarantee during a rapid macro execution. `SendKeys`, while older, queues events sequentially in the OS message pump, ensuring that the `TAB` key is processed *before* the subsequent password characters are typed, regardless of UI rendering lag.

---

## 5. Lessons Learned

1. **Test Boundaries Matter**: The initial unit tests passed because they mocked the `IInputSimulator` interface. They proved the *orchestration* was correct, but masked the fact that the underlying OS implementation (`UiaHelper` + `SendKeys`) was fundamentally flawed in its interaction with the OS focus model.
2. **UIA Semantics**: UIA `SetValue` is a "Replace All" operation, not a "Type Text" operation. It should only be used for single-field targeted injections, never in a rapid sequence involving focus changes (`TAB`).
3. **SendKeys Reliability**: Despite its reputation, `SendKeys` remains the most reliable method for cross-application, multi-field macro injection because it respects the OS input queue.

*Document updated: 2026-03-26*