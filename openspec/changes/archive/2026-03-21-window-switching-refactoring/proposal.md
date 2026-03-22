## Why

The window switching system has accumulated significant technical debt through multiple iterations: Win32 API methods are duplicated across 4 locations (PulsarContext, WindowService, WindowActivationMonitor, WindowHelper), WindowService is a 1335-line class with 8 mixed responsibilities, and ProcessPageProvider conflates data loading with UI binding. This fragmentation creates maintenance risks, potential behavioral inconsistencies across Windows versions, and impedes testability. Consolidating these APIs into a single entry point will eliminate the "silent bomb" of duplicate definitions while making the codebase more maintainable.

## What Changes

- Create `PulsarNative.cs` as the single source of truth for all Win32/Windows API declarations
- Refactor `WindowActivationMonitor.cs` to use `PulsarNative` instead of inline P/Invoke declarations
- Refactor `WindowService.cs` to use `PulsarNative` and remove duplicate API definitions (2 inline NativeMethods classes)
- Refactor `PulsarContext.cs` to use `PulsarNative` instead of inline NativeMethods
- Organize `WindowService.cs` with `#region` blocks for better readability (no behavioral changes)
- Create `ProcessWindowMatcher.cs` to separate data loading logic from ProcessPageProvider
- Update `ProcessPageProvider.cs` to use the new matcher
- Delete `WindowHelper.cs` as it becomes obsolete after consolidation

## Capabilities

### New Capabilities

- `unified-native-api`: Consolidate all Win32 API definitions into a single `PulsarNative` class with proper constants, delegates, structures, and P/Invoke declarations
- `window-service-refactoring`: Restructure WindowService to use unified native API and organize code with regions
- `process-window-matcher`: Extract data matching logic into a separate testable class
- `window-activation-refactoring`: Update WindowActivationMonitor to use unified native API
- `context-native-refactoring`: Update PulsarContext to use unified native API

### Modified Capabilities

- None - this is a pure refactoring with no behavioral or API changes

## Impact

**Files Modified:**
- `Pulsar/Pulsar/Native/PulsarNative.cs` - New file
- `Pulsar/Pulsar/Services/WindowActivationMonitor.cs` - Replace inline P/Invoke with PulsarNative
- `Pulsar/Pulsar/Services/WindowService.cs` - Replace inline APIs, add regions
- `Pulsar/Pulsar/Core/Plugin/PulsarContext.cs` - Replace inline NativeMethods
- `Pulsar/Pulsar/ViewModels/Strategies/ProcessPageProvider.cs` - Use new matcher
- `Pulsar/Pulsar/ViewModels/Strategies/ProcessWindowMatcher.cs` - New file

**Files Deleted:**
- `Pulsar/Pulsar/Native/WindowHelper.cs` - Obsolete after consolidation

**Dependencies:** None - this is an internal refactoring with no external dependency changes