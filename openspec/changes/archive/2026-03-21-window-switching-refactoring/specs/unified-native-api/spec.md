## ADDED Requirements

### Requirement: PulsarNative SHALL contain all Win32 API definitions

The file `Pulsar/Pulsar/Native/PulsarNative.cs` SHALL contain all P/Invoke declarations, constants, delegates, and structures required for window management operations. No other file in the project SHALL define duplicate Win32 API methods.

#### Scenario: No duplicate API definitions
- **WHEN** codebase is searched for Win32 API P/Invoke declarations
- **THEN** only `PulsarNative.cs` contains these definitions (excluding shell32 for icon extraction, gdi32, kernel32)

### Requirement: PulsarNative SHALL include foreground lock management

The `PulsarNative.SetForegroundWindow` method SHALL implement reference-counting logic to manage system foreground lock timeout. This ensures consistent behavior across all callers.

#### Scenario: Multiple SetForegroundWindow calls
- **WHEN** `SetForegroundWindow` is called multiple times before any timeout
- **THEN** foreground lock timeout remains disabled until all calls complete (reference count reaches zero)

### Requirement: PulsarNative SHALL expose required constants and types

The class SHALL expose all constants (GWL_EXSTYLE, WS_EX_TOOLWINDOW, etc.), delegates (EnumWindowsProc, WinEventDelegate), and structures (RECT, SHFILEINFO) needed by consuming services.

#### Scenario: Consumer accesses constants
- **WHEN** WindowService references `PulsarNative.GWL_EXSTYLE`
- **THEN** the constant value matches the original definition (-20)

### Requirement: PulsarNative SHALL NOT include icon extraction APIs

The SHGetFileInfo, SHGFI_* constants, and related icon extraction logic SHALL remain in WindowService as local code, not in PulsarNative.

#### Scenario: Icon extraction APIs location
- **WHEN** code searches for SHGetFileInfo declaration
- **THEN** it is found in WindowService, not in PulsarNative