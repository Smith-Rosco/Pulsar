# Fix Pulsar UI Issues & Refactor Execution Logic

## 1. Window Switcher - Selection Application (Pick Process)
**Problem**: The "Application Name" field doesn't update when picking a process because the ViewModel updates the dictionary directly, bypassing property change notifications.
**Fix**: Update `SettingsViewModel.PickProcess` to use the indexer assignment (`slot["app"] = ...`).

## 2. Bookmarklet - Script File Selection
**Problem A**: `OpenFileDialog` filter is restricted to `.js`.
**Fix**: Update filter to support `.txt` files (`*.js;*.txt`).
**Problem B**: Input box doesn't populate.
**Fix**: Update `SettingsViewModel.PickScriptFile` (and `PickVbaScriptFile`) to use indexer assignment only, removing redundant dictionary updates.

## 3. Bookmarklet Runner - IME Interference
**Problem**: `SendKeys("j")` is intercepted by IME, causing incorrect input.
**Fix**: Implement a "Paste-Only" strategy in `BookmarkletRunnerPlugin.cs`.
  - Focus Address Bar (Ctrl+L).
  - Copy "j" to clipboard -> Paste (Ctrl+V).
  - Copy "avascript:..." to clipboard -> Paste (Ctrl+V).
  - Enter.
  - This bypasses IME completely.

## 4. VBA Runner - Focus Restoration & Architecture
**Problem**: Focus is not reliably returned to the original window after script execution, requiring manual intervention. Also, redundant P/Invoke declarations.
**Fix**:
  - In `VbaRunnerPlugin.cs`, add a `finally` block to `RunScriptAsync` that calls `SetForegroundWindow(context.TargetWindowHandle)` to ensure focus restoration.
  - Refactor `ScriptEngine.cs` to remove duplicate native method declarations and use `Pulsar.Native.WindowHelper` where applicable.
