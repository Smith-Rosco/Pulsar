## 1. Phase 0: Create PulsarNative

- [x] 1.1 Create `Pulsar/Pulsar/Native/PulsarNative.cs` with all constants (GWL_EXSTYLE, WS_EX_TOOLWINDOW, etc.)
- [x] 1.2 Add delegates (EnumWindowsProc, WinEventDelegate)
- [x] 1.3 Add structures (RECT, SHFILEINFO)
- [x] 1.4 Add all P/Invoke declarations for window management APIs
- [x] 1.5 Implement SetForegroundWindow with reference-counting logic
- [x] 1.6 Run `dotnet build` to verify compilation

## 2. Phase 1: Refactor WindowActivationMonitor

- [x] 2.1 Remove inline WinEventDelegate declaration
- [x] 2.2 Remove inline SetWinEventHook/UnhookWinEvent declarations
- [x] 2.3 Remove EVENT_SYSTEM_FOREGROUND and WINEVENT_OUTOFCONTEXT constants
- [x] 2.4 Replace all WinEvent calls with PulsarNative equivalents
- [x] 2.5 Run `dotnet build` to verify compilation

## 3. Phase 2: Refactor WindowService

- [x] 3.1 Phase 2A: Remove inline NativeMethods class (L1294-1333), replace calls with PulsarNative
- [x] 3.2 Phase 2B: Remove inline GetForegroundWindow and GetWindowThreadProcessId (L180-204)
- [x] 3.3 Phase 2C: Remove duplicate local constants (L1271-1277)
- [x] 3.4 Keep SHGetFileInfo and icon extraction APIs in WindowService (not in PulsarNative)
- [x] 3.5 Run `dotnet build` to verify compilation
- [x] 3.6 Run functional tests to verify all window switching scenarios work

## 4. Phase 3: Refactor PulsarContext

- [x] 4.1 Remove inline NativeMethods class (L200-204)
- [x] 4.2 Replace GetWindowThreadProcessId call with PulsarNative
- [x] 4.3 Run `dotnet build` to verify compilation

## 5. Phase 4: Add regions to WindowService

- [x] 5.1 Add #region Constructor & Fields
- [x] 5.2 Add #region Constructor
- [x] 5.3 Add #region Public API (IWindowService)
- [x] 5.4 Add #region Window Enumeration
- [x] 5.5 Add #region QuickSwitch State
- [x] 5.6 Add #region Icon Management
- [x] 5.7 Add #region Window Registry
- [x] 5.8 Add #region Internal Event Handlers
- [x] 5.9 Add #region Private Helpers

## 6. Phase 5: ProcessPageProvider Split

- [x] 6.1 Create `ViewModels/Strategies/ProcessWindowMatcher.cs` with MatchedWindowGroup and ProcessWindowMatcher classes
- [x] 6.2 Implement BuildSlotList method with config matching logic
- [x] 6.3 Update ProcessPageProvider to use ProcessWindowMatcher
- [x] 6.4 Run `dotnet build` to verify compilation

## 7. Cleanup

- [x] 7.1 Kept WindowHelper.cs as backward-compatible wrapper (marked obsolete, forwards to PulsarNative)
- [x] 7.2 Run final `dotnet build` to verify entire project compiles
- [x] 7.3 Run full functional test suite to verify all behaviors unchanged