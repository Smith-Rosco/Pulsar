# Bookmarklet Runner Refactoring - Handover Document

## 1. Context & Goal
The objective is to optimize the **Bookmarklet Runner** plugin to execute JavaScript bookmarklets in a browser without "polluting" the user's clipboard.
- **Workflow**: User triggers plugin -> Plugin executes JS in browser -> Plugin restores original clipboard content.
- **Constraint**: The user must not lose their previous clipboard content (e.g., text they intended to paste into the bookmarklet's prompt).

## 2. Current Architecture (Status: Unstable)
The current implementation in `BookmarkletRunnerPlugin.cs` uses a **"Hybrid Input Strategy"**:
1.  **Backup**: Saves current clipboard using `System.Windows.Forms.Clipboard.GetDataObject()`.
2.  **Focus**: Sends `Ctrl+L`.
3.  **Type Prefix**: Uses `SendInput` (via `InputHelper`) to type `javascript:` instantly (bypassing browser paste security).
4.  **Paste Body**: Sets clipboard to script content and sends `Ctrl+V`.
5.  **Execute**: Sends `Enter`.
6.  **Restore**: Attempts to restore original clipboard data using a background retry loop marshaled back to the UI thread via `Dispatcher`.

## 3. The Problem (Critical Bug)
The current implementation throws `System.Runtime.InteropServices.ExternalException (0x800401D0)` ("Requested clipboard operation failed") during the **Restore** phase.

### Root Cause Analysis
1.  **Threading Model Mismatch**: The project is a WPF application (`net8.0-windows`), but the plugin is forcing usage of `System.Windows.Forms.Clipboard`.
2.  **COM Object Marshaling**: `IDataObject` retrieved from WinForms is being captured in a closure, passed to a `Task.Run` (MTA), and then marshaled back to the Main Thread (STA) via `Dispatcher`. This ping-pong likely invalidates the underlying COM pointer or violates STA threading rules for that specific COM proxy.
3.  **Browser Locking**: The browser (target window) locks the clipboard to read the pasted data. The plugin tries to restore it too quickly or aggressively, clashing with the browser's lock.

## 4. Recommended Solution Strategy (For Next Session)

**DO NOT simply patch the existing WinForms code.** The mixture of threading models is toxic.

### Step 1: Migrate to WPF Native Clipboard
Switch from `System.Windows.Forms.Clipboard` to `System.Windows.Clipboard`.
- The application is WPF. Using the native WPF API ensures better compatibility with the `Dispatcher` and existing message pump.
- **Caveat**: WPF's `Clipboard.GetDataObject()` return value might still be tied to the COM thread.

### Step 2: Deep Clone Data (The "Snapshot" Approach)
Instead of holding a reference to the live `IDataObject` (which can become invalid or lock the source application), try to **extract and cache** the actual data payloads for common formats (Text, HTML, Image).
- If perfect fidelity (all formats) is required, ensure the `IDataObject` is not accessed across threads. The restore logic should ideally happen **on the UI thread** using a `DispatcherTimer` instead of `Task.Run` + `Thread.Sleep`, to keep everything in the same STA context without blocking the UI.

### Step 3: Use `DispatcherTimer` for Retries
Replace the `Task.Run` + `Thread.Sleep` loop with a `DispatcherTimer`.
- **Why**: `Task.Run` moves execution to a worker thread (MTA). `DispatcherTimer` keeps execution on the UI thread (STA) but allows delays. This eliminates cross-thread marshaling of the `IDataObject`.

### Proposed Workflow (Pseudo-code)

```csharp
// 1. On UI Thread
var originalData = Clipboard.GetDataObject(); // WPF API

// 2. Execute Action
// ... SendInput / Paste logic ...

// 3. Queue Restore (Stay on UI Thread!)
var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
int retryCount = 0;

timer.Tick += (s, e) => {
    try {
        Clipboard.SetDataObject(originalData); // WPF API
        timer.Stop();
        Debug.WriteLine("Restored!");
    }
    catch (ExternalException) {
        retryCount++;
        if (retryCount > 10) timer.Stop(); // Give up
    }
};
timer.Start();
```

## 5. Files of Interest
- `Pulsar/Pulsar/Plugins/BookmarkletRunner/BookmarkletRunnerPlugin.cs`: Main logic to refactor.
- `Pulsar/Pulsar/Native/InputHelper.cs`: Low-level input handling (Working fine, keep as is).
